using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Application.Import;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// LC02-Abnahme-Nachweis (Plan §5 LC02 / LC-AC-01): ein über OpenAPI-Import entstandenes
/// Connector-Binding führt mit genau den konfigurierten Werten aus. Der Test importiert
/// eine echte OpenAPI-Spec, leitet das Binding direkt aus dem Import-Ergebnis ab (derselbe
/// vertex:connector.*-Vertrag wie Parser/Handler/Executor), deployt es in einem BPMN-Diagramm
/// und prüft den TATSÄCHLICHEN HTTP-Request des realen Connector-Aufrufs gegen einen lokalen
/// Server: Methode, URL (inkl. aufgelöster Pfadvariable) und Header. Nur so ist der importierte
/// Vertrag bewiesen — kein vorab gesetztes Ergebnis, keine Test-Double-Antwort.
/// </summary>
public sealed class ImportedConnectorBindingExecutionTests
{
	[Fact]
	public async Task ImportedBinding_ExecutesRealRequest_WithConfiguredMethodUrlHeader()
	{
		var cancellationToken = TestContext.Current.CancellationToken;
		var recorded = new ConcurrentBag<RecordedRequest>();
		await using var server = new RecordingServer(recorded);

		// 1) Reale OpenAPI-Import -> ConnectorTemplate (der "getCustomer"-Operation), direkt
		//    gegen den lokalen Server gerichtet, mit Path- und Header-Parameter.
		var importer = new OpenApiConnectorTemplateImporter();
		var importResult = importer.Import(OpenApiSpec(server.BaseUrl), "tenant-a");
		var template = importResult.Templates.Single(t => t.Name == "getCustomer");
		Assert.NotNull(template.Properties.Single(p => p.Key == "vertex:connector.bindingVersion").DefaultValue);

		// 2) Binding EINS-zu-EINS aus dem Import-Ergebnis ableiten: Importer/Studio/Runtime
		//    teilen denselben vertex:connector.*-Vertrag (kein Umbau, keine Umschreibung).
		IDictionary<string, string> binding = BindingFromImport(template);

		// 3) Echter Connector-Runtime über den (per Import gesetzten) lokalen Endpoint.
		using var handler = new HttpClientHandler
		{
			AllowAutoRedirect = false,
			CheckCertificateRevocationList = true,
		};
		using var httpClient = new HttpClient(handler);
		var taskRegistry = BuildConnectorRegistry(httpClient);

		// 4) Deploy + Start über die echte PersistentProcessExecutionRuntime.
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(cancellationToken);
		await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
			.UseSqlite(connection).Options);
		await db.Database.EnsureCreatedAsync(cancellationToken);
		var definition = CreateDefinition(db, binding);
		await db.SaveChangesAsync(cancellationToken);
		var processRuntime = new PersistentProcessExecutionRuntime(
			db,
			taskRegistry.Object,
			Mock.Of<IDecisionService>(),
			NullLogger<PersistentProcessExecutionRuntime>.Instance);

		var instance = await processRuntime.StartAsync(
			definition,
			variables: new Dictionary<string, object>(StringComparer.Ordinal) { ["customerId"] = "42", ["tenant"] = "t1" },
			businessKey: null,
			tenantId: "tenant-a",
			cancellationToken: cancellationToken);

		// 5) Abnahmenachweis: der eine reale Request entspricht dem importierten Binding.
		VerifyCompleted(db, instance, recorded, definition.BpmnXml);
	}

	private static void VerifyCompleted(BpmnDbContext db, ProcessInstance instance, IEnumerable<RecordedRequest> recorded, string bpmnXml)
	{
		// Der Prozess muss über den Connector hinaus bis zum Ende laufen (Fachfolgeschritt).
		if (instance.Status != ProcessInstanceStatus.Completed)
		{
			var incidents = db.Set<Incident>()
				.Where(i => i.ProcessInstanceId == instance.Id)
				.Select(i => i.Message)
				.ToList();
			throw new Xunit.Sdk.XunitException(
				$"Status={instance.Status}; incidents=[{string.Join(" | ", incidents)}]; BpmnXml=[{bpmnXml}]");
		}

		var request = Assert.Single(recorded);
		Assert.Equal("GET", request.Method);
		Assert.Contains("/customers/42", request.Path, StringComparison.Ordinal); // Path-Variable aus Prozesskontext aufgelöst
		Assert.Contains("X-Tenant", request.Headers, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("X-Tenant: t1", request.Headers, StringComparison.OrdinalIgnoreCase); // Header aus {{tenant}} aufgelöst
	}

	/// <summary>
	/// Überträgt die importierten ConnectorTemplateProperty-Einträge unverändert auf den
	/// Handler-Vertrag (vertex:connector.* mit DefaultValue, inkl. bindingVersion=2), damit der
	/// Test genau das Binding abnimmt, das der Importer erzeugt und der Runtime übergibt.
	/// </summary>
	private static Dictionary<string, string> BindingFromImport(ConnectorTemplateWriteRequest template)
	{
		var binding = new Dictionary<string, string>(StringComparer.Ordinal)
		{
			["vertex:connector.type"] = "http",
			["vertex:connector.operationId"] = "http.request",
		};

		foreach (var property in template.Properties)
		{
			if (!property.Key.StartsWith("vertex:connector.", StringComparison.Ordinal) || property.DefaultValue is null)
			{
				continue; // path param (endpoint placeholder) und optionale Felder ohne Default
			}

			// Als String normalisiert, damit der XML-Attributwert den Handler-Vertrag trifft.
			binding[property.Key] = property.DefaultValue;
		}

		// Der Header-Parameter hat keinen Import-Default: er wird hier an eine Prozessvariable
		// gebunden ({{tenant}}), deren Auflösung der Request-Nachweis unten prüft.
		binding["vertex:connector.header.X-Tenant"] = "{{tenant}}";
		return binding;
	}

	private static Mock<IServiceTaskRegistry> BuildConnectorRegistry(HttpClient httpClient)
	{
		var redaction = new ConnectorRedactionPolicy();
		var http = new HttpConnectorExecutor(httpClient, new HttpDataResponseReader(64 * 1024, redaction));
		IConnectorRegistry connectorRegistry = new ConnectorRegistry([http]);
		var connectorRuntime = new ConnectorRuntime(
			connectorRegistry,
			new ConnectorRateLimitPolicy(),
			redaction,
			NullLogger<ConnectorRuntime>.Instance);

		var scopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
		var configuration = new ConfigurationBuilder().Build();
		IServiceTaskHandler connectorTaskHandler = new VertexConnectorServiceTaskHandler(connectorRuntime, scopeFactory, configuration);
		var resolvedHandler = connectorTaskHandler;

		var taskRegistry = new Mock<IServiceTaskRegistry>();
		taskRegistry.Setup(candidate => candidate.TryResolve("vertex:connector", out resolvedHandler)).Returns(true);
		return taskRegistry;
	}

	private static ProcessDefinition CreateDefinition(BpmnDbContext db, IDictionary<string, string> binding)
	{
		var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "lc02-binding" };
		var definition = new ProcessDefinition
		{
			Id = Guid.NewGuid(),
			Key = "lc02-binding",
			Name = "LC02 imported binding",
			Version = 1,
			TenantId = "tenant-a",
			Deployment = deployment,
			DeploymentId = deployment.Id,
			CreatedAt = DateTime.UtcNow,
			BpmnXml = ProcessXml(binding)
		};
		db.ProcessDefinitions.Add(definition);
		return definition;
	}

	private static string ProcessXml(IDictionary<string, string> binding)
	{
		// Die Bindung nutzt kanonische vertex:connector.*-Keys. Als XML-Attribut des
		// <vertex:connector>-Elements wird nur der Namensrest ohne Präfix geschrieben
		// (type, method, .../header.X-Tenant) — der Parser ergänzt das Präfix beim Flatten.
		// Einen Re-Import ausgeführt, liest die Runtime exakt dieselben Keys, die der
		// Importer/Handler-Vertrag erwartet (derselbe Contract, kein doppeltes Präfix).
		var connectorAttrs = string.Join(" ", binding
			.Where(kv => kv.Key.StartsWith("vertex:connector.", StringComparison.Ordinal))
			.Select(kv => $"{kv.Key["vertex:connector.".Length..]}='{kv.Value}'"));

		return $$"""
			<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
			             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
			             xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
			  <process id="lc02-binding" isExecutable="true">
			    <startEvent id="start" />
			    <sequenceFlow id="sf-start-conn" sourceRef="start" targetRef="apiConn" />
			    <serviceTask id="apiConn">
			      <extensionElements>
			        <vertex:connector {{connectorAttrs}} />
			      </extensionElements>
			    </serviceTask>
			    <sequenceFlow id="sf-conn-end" sourceRef="apiConn" targetRef="end" />
			    <endEvent id="end" />
			  </process>
			</definitions>
			""";
	}

	private static string OpenApiSpec(string serverBaseUrl)
	{
		var spec = $$"""
			{
			  "openapi": "3.0.1",
			  "info": { "title": "Customers", "version": "1.0.0" },
			  "servers": [ { "url": "{{serverBaseUrl}}" } ],
			  "paths": {
			    "/customers/{customerId}": {
			      "get": {
			        "operationId": "getCustomer",
			        "parameters": [
			          { "name": "customerId", "in": "path", "required": true, "schema": { "type": "string" } },
			          { "name": "X-Tenant", "in": "header", "required": true, "schema": { "type": "string" } }
			        ],
			        "responses": { "200": { "description": "ok" } }
			      }
			    }
			  }
			}
			""";
		return spec;
	}

	private sealed record RecordedRequest(string Method, string Path, string Headers);

	/// <summary>Echter HTTP/1.1-Server, der Methode, Pfad und Header des Requests festhält.</summary>
	private sealed class RecordingServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly ConcurrentBag<RecordedRequest> _recorded;
		private readonly CancellationTokenSource _cts = new();
		private readonly Task _loop;

		public string BaseUrl { get; }

		public RecordingServer(ConcurrentBag<RecordedRequest> recorded)
		{
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
			BaseUrl = $"http://127.0.0.1:{port}";
			_recorded = recorded;
			_loop = Task.Run(AcceptLoopAsync, _cts.Token);
		}

		private async Task AcceptLoopAsync()
		{
			try
			{
				while (!_cts.IsCancellationRequested)
				{
					var client = await _listener.AcceptTcpClientAsync(_cts.Token);
					_ = Task.Run(() => HandleAsync(client), _cts.Token);
				}
			}
			catch (OperationCanceledException)
			{
				// normal shutdown
			}
		}

		private async Task HandleAsync(TcpClient client)
		{
			using var c = client;
			await using var stream = c.GetStream();
			try
			{
				var header = await ReadHeaderAsync(stream);
				var lines = header.Split("\r\n");
				var requestLine = lines[0]; // z. B. "GET /customers/42 HTTP/1.1"
				var parts = requestLine.Split(' ');
				var method = parts.Length > 0 ? parts[0] : string.Empty;
				var path = parts.Length > 1 ? parts[1] : "/";
				var headers = string.Join("\r\n", lines.Skip(1));
				_recorded.Add(new RecordedRequest(method, path, headers));

				var body = Encoding.UTF8.GetBytes("{\"id\":42}");
				var head = Encoding.ASCII.GetBytes(
					"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
				await stream.WriteAsync(head, _cts.Token);
				await stream.WriteAsync(body, _cts.Token);
				await stream.FlushAsync(_cts.Token);
			}
			catch (OperationCanceledException)
			{
				// client disconnected / shutdown
			}
		}

		private async Task<string> ReadHeaderAsync(NetworkStream stream)
		{
			var buffer = new byte[1024];
			var received = new List<byte>();
			int read;
			while ((read = await stream.ReadAsync(buffer.AsMemory(), _cts.Token)) > 0)
			{
				received.AddRange(buffer.AsSpan(0, read).ToArray());
				if (received.Count >= 4 && received.TakeLast(4).SequenceEqual(new byte[] { 13, 10, 13, 10 }))
				{
					break;
				}
			}
			return Encoding.UTF8.GetString(received.ToArray());
		}

		public async ValueTask DisposeAsync()
		{
			await _cts.CancelAsync();
			_listener.Stop();
			await Task.WhenAny(_loop, Task.Delay(500, _cts.Token));
			_listener.Dispose();
			_cts.Dispose();
			GC.SuppressFinalize(this);
		}
	}
}
