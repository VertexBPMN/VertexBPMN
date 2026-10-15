using System.Globalization;
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
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

/// <summary>
/// Durchgehender Datenfall (Plan Z.249): ein realer HTTP-Connector liest eine echte
/// (lokale Mock-)Kundenantwort, das Output-Mapping (vertex:connector.outputMapping,
/// Strang G) mappt Betrag/Kundengruppe auf Prozessvariablen, ein FEEL-Exclusive-Gateway
/// wertet den gemappten Betrag aus und der tatsächlich ausgeführte BPMN-Pfad wird anhand
/// einer Pfad-Markierung pro Zweig verifiziert. Keine vorab gesetzten Ergebnisvariablen:
/// alle Variablen entstehen aus der echten HTTP-Antwort.
/// </summary>
public sealed class PersistentEndToEndDmnDataFlowTests
{
	[Theory]
	[InlineData(1500, "GOLD", "gold")]    // premium Kunde -> Gold-Pfad (Betrag > 1000)
	[InlineData(300, "DEFAULT", "standard")] // Standard Kunde -> Default-Pfad
	public async Task ConnectorResponse_MappedVariables_DriveDmnGatewayPath(int amount, string expectedBranch, string customerGroup)
	{
		var cancellationToken = TestContext.Current.CancellationToken;

		// 1) Echter lokaler Mock-HTTP-Server, der pro Pfad eine Kunden-/Zweig-Antwort liefert.
		await using var server = new MockHttpServer(path => path switch
		{
			"/customer" => (200, JsonSerializer.Serialize(new { amount, customerGroup })),
			"/gold" => (200, JsonSerializer.Serialize(new { branch = "GOLD" })),
			"/default" => (200, JsonSerializer.Serialize(new { branch = "DEFAULT" })),
			_ => (404, "{}")
		});

		// 2+3) Echter Connector-Runtime + Service-Task-Registry über den Mock-Endpoint.
		using var handler = new HttpClientHandler
		{
			AllowAutoRedirect = false,
			CheckCertificateRevocationList = true,
		};
		using var httpClient = new HttpClient(handler);
		var taskRegistry = BuildConnectorRegistry(httpClient);

		// 4) In-Memory-SQLite + deployed Definition mit Connector, Gateway und Pfad-Markern.
		await using var connection = new SqliteConnection("Data Source=:memory:");
		await connection.OpenAsync(cancellationToken);
		await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
			.UseSqlite(connection).Options);
		await db.Database.EnsureCreatedAsync(cancellationToken);
		var definition = CreateDefinition(db, server.BaseUrl);
		await db.SaveChangesAsync(cancellationToken);

		var processRuntime = new PersistentProcessExecutionRuntime(
			db,
			taskRegistry.Object,
			Mock.Of<IDecisionService>(),
			NullLogger<PersistentProcessExecutionRuntime>.Instance);

		var instance = await processRuntime.StartAsync(
			definition,
			variables: null,
			businessKey: null,
			tenantId: null,
			cancellationToken: cancellationToken);

		// 5) Verifikation: Ausführenden BPMN-Pfad anhand der Pfad-Markierung prüfen
		//    (nur der gewählte Zweig hat seinen Marker-Connector ausgeführt).
		AssertCompletedPath(db, instance, amount, expectedBranch, customerGroup);
		taskRegistry.VerifyAll();
	}

	private static void AssertCompletedPath(BpmnDbContext db, ProcessInstance instance, int amount, string expectedBranch, string customerGroup)
	{
		if (instance.Status != ProcessInstanceStatus.Completed)
		{
			var incidents = db.Set<Incident>()
				.Where(i => i.ProcessInstanceId == instance.Id)
				.Select(i => i.Message)
				.ToList();
			throw new Xunit.Sdk.XunitException(
				$"Status={instance.Status}; incidents=[{string.Join(" | ", incidents)}]");
		}
		Assert.NotNull(instance.Variables);
		Assert.True(instance.Variables.ContainsKey("connector.output.branch"), "Kein Zweig-Marker gesetzt.");

		var branch = Convert.ToString(instance.Variables["connector.output.branch"]);
		Assert.Equal(expectedBranch, branch);

		// Die gemappte Kundengruppe (aus der echten Antwort, transformiert mit ToText).
		var group = Convert.ToString(instance.Variables["connector.output.kundengruppe"]);
		Assert.Equal(customerGroup, group);

		// Der gemappte Betrag (ToNumber) liegt als Prozessvariable vor und trieb das Gateway.
		Assert.True(instance.Variables.ContainsKey("connector.output.betrag"), "Betrag wurde nicht als Variable gemappt.");
		Assert.True(NumericEquals(instance.Variables["connector.output.betrag"], amount), "Betrag stimmt nicht mit der Antwort überein.");
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

	private static ProcessDefinition CreateDefinition(BpmnDbContext db, string baseUrl)
	{
		var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "dmn-dataflow" };
		var definition = new ProcessDefinition
		{
			Id = Guid.NewGuid(),
			Key = "dmn-dataflow",
			Name = "DMN data flow",
			Version = 1,
			Deployment = deployment,
			DeploymentId = deployment.Id,
			CreatedAt = DateTime.UtcNow,
			BpmnXml = ProcessXml(baseUrl)
		};
		db.ProcessDefinitions.Add(definition);
		return definition;
	}

	private static bool NumericEquals(object? value, decimal expected)
	{
		if (value is JsonElement json)
		{
			value = json.ValueKind == JsonValueKind.Number ? json.GetDecimal() : null;
		}
		return value switch
		{
			decimal d => d == expected,
			int i => i == expected,
			long l => l == expected,
			float f => (decimal)f == expected,
			double dd => (decimal)dd == expected,
			_ => false
		};
	}

	private static string ProcessXml(string baseUrl) => $$"""
		<definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
		             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
		             xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
		  <process id="dmn-dataflow" isExecutable="true">
		    <startEvent id="start" />

		    <sequenceFlow id="sf-start-conn" sourceRef="start" targetRef="customerConn" />

		    <serviceTask id="customerConn">
		      <extensionElements>
		        <vertex:connector type="http" operationId="http.request" method="GET"
		                          endpoint="{{baseUrl}}/customer"
		                          outputMapping='[{"sourcePath":"amount","targetKey":"betrag","transform":"ToNumber"},{"sourcePath":"customerGroup","targetKey":"kundengruppe","transform":"ToText"}]' />
		      </extensionElements>
		    </serviceTask>

		    <sequenceFlow id="sf-conn-gw" sourceRef="customerConn" targetRef="gw" />

		    <exclusiveGateway id="gw" default="sf-default">
		      <incoming>sf-conn-gw</incoming>
		      <outgoing>sf-gold</outgoing>
		      <outgoing>sf-default</outgoing>
		    </exclusiveGateway>

		    <sequenceFlow id="sf-gold" sourceRef="gw" targetRef="goldConn">
		      <conditionExpression xsi:type="bpmn:tFormalExpression" language="feel">connector.output.betrag &gt; 1000</conditionExpression>
		    </sequenceFlow>

		    <sequenceFlow id="sf-default" sourceRef="gw" targetRef="defaultConn" />

		    <serviceTask id="goldConn">
		      <extensionElements>
		        <vertex:connector type="http" operationId="http.request" method="GET"
		                          endpoint="{{baseUrl}}/gold"
		                          outputMapping='[{"sourcePath":"branch","targetKey":"branch","transform":"ToText"}]' />
		      </extensionElements>
		    </serviceTask>

		    <serviceTask id="defaultConn">
		      <extensionElements>
		        <vertex:connector type="http" operationId="http.request" method="GET"
		                          endpoint="{{baseUrl}}/default"
		                          outputMapping='[{"sourcePath":"branch","targetKey":"branch","transform":"ToText"}]' />
		      </extensionElements>
		    </serviceTask>

		    <sequenceFlow id="sf-gold-end" sourceRef="goldConn" targetRef="goldEnd" />
		    <sequenceFlow id="sf-default-end" sourceRef="defaultConn" targetRef="defaultEnd" />
		    <endEvent id="goldEnd" />
		    <endEvent id="defaultEnd" />
		  </process>
		</definitions>
		""";

	/// <summary>
	/// Minimaler echter HTTP/1.1-Server (TcpListener) zur lokalen Belieferung des Connectors —
	/// das ist eine echte Netzwerk-Antwort, keine Test-Double-Antwort des Executors.
	/// </summary>
	private sealed class MockHttpServer : IAsyncDisposable
	{
		private readonly TcpListener _listener;
		private readonly Func<string, (int Status, string Json)> _router;
		private readonly CancellationTokenSource _cts = new();
		private readonly Task _loop;

		public string BaseUrl { get; }

		public MockHttpServer(Func<string, (int, string)> router)
		{
			_listener = new TcpListener(IPAddress.Loopback, 0);
			_listener.Start();
			var port = ((IPEndPoint)_listener.LocalEndpoint).Port;
			BaseUrl = $"http://127.0.0.1:{port}";
			_router = router;
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
				var requestLine = header.Split("\r\n", 2)[0];
				var path = "/";
				var parts = requestLine.Split(' ');
				if (parts.Length > 1)
				{
					path = parts[1];
				}

				var (status, json) = _router(path);
				var body = Encoding.UTF8.GetBytes(json);
				var reason = status == 200 ? "OK" : "Not Found";
				var head = Encoding.ASCII.GetBytes(
					"HTTP/1.1 " + status + " " + reason + "\r\n" +
					"Content-Type: application/json\r\n" +
					"Content-Length: " + body.Length + "\r\n" +
					"Connection: close\r\n\r\n");
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
