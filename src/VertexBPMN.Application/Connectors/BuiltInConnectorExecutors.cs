using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public class HttpConnectorExecutor(HttpClient client) : IConnectorExecutor
{
    public virtual string Type => "http";

    public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        if (context.Endpoint is null || (context.Endpoint.Scheme != Uri.UriSchemeHttp && context.Endpoint.Scheme != Uri.UriSchemeHttps))
            throw new ServiceTaskExecutionException($"{Type} connector requires an absolute HTTP(S) endpoint.");

        var method = context.Attributes.TryGetValue("vertex:connector.method", out var configured) ? configured : HttpMethod.Post.Method;
        using var request = new HttpRequestMessage(new HttpMethod(method), context.Endpoint);
        if (context.Attributes.TryGetValue("vertex:connector.body", out var body))
            request.Content = new StringContent(body, Encoding.UTF8, context.Attributes.TryGetValue("vertex:connector.contentType", out var contentType) ? contentType : "application/json");
        if (!string.IsNullOrEmpty(context.CredentialSecret))
        {
            var scheme = context.Attributes.TryGetValue("vertex:connector.authScheme", out var configuredScheme) ? configuredScheme : "Bearer";
            request.Headers.Authorization = new AuthenticationHeaderValue(scheme, context.CredentialSecret);
        }

        using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        return new ConnectorExecutionResult(
            response.IsSuccessStatusCode,
            (int)response.StatusCode,
            new Dictionary<string, object> { ["httpStatus"] = (int)response.StatusCode },
            response.IsSuccessStatusCode ? null : MapHttpError(response.StatusCode));
    }

    private static string MapHttpError(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "authentication_error",
        HttpStatusCode.TooManyRequests => "rate_limited",
        >= HttpStatusCode.InternalServerError => "remote_server_error",
        _ => "http_error"
    };
}

public sealed class NamedHttpConnectorExecutor(HttpClient client, string connectorType) : HttpConnectorExecutor(client)
{
    public override string Type => connectorType;
}

public sealed class DelayConnectorExecutor : IConnectorExecutor
{
    public string Type => "delay";
    public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        var milliseconds = context.Attributes.TryGetValue("vertex:connector.delayMs", out var value) && int.TryParse(value, out var parsed) ? Math.Clamp(parsed, 0, 86_400_000) : 0;
        await Task.Delay(milliseconds, cancellationToken);
        return new ConnectorExecutionResult(true, null, new Dictionary<string, object> { ["delayedMs"] = milliseconds });
    }
}

public class EmailConnectorExecutor : IConnectorExecutor
{
    public virtual string Type => "email";
    public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        var host = Require(context, "vertex:connector.smtpHost");
        var to = Require(context, "vertex:connector.to");
        var from = Require(context, "vertex:connector.from");
        var port = context.Attributes.TryGetValue("vertex:connector.smtpPort", out var rawPort) && int.TryParse(rawPort, out var configuredPort) ? configuredPort : 587;
        using var client = new SmtpClient(host, port)
        {
            EnableSsl = !context.Attributes.TryGetValue("vertex:connector.ssl", out var ssl) || !string.Equals(ssl, "false", StringComparison.OrdinalIgnoreCase)
        };
        if (!string.IsNullOrEmpty(context.CredentialSecret))
        {
            var username = Require(context, "vertex:connector.username");
            client.Credentials = new NetworkCredential(username, context.CredentialSecret);
        }
        using var message = new MailMessage(from, to)
        {
            Subject = context.Attributes.TryGetValue("vertex:connector.subject", out var subject) ? subject : string.Empty,
            Body = context.Attributes.TryGetValue("vertex:connector.body", out var body) ? body : string.Empty
        };
        await client.SendMailAsync(message, cancellationToken);
        return new ConnectorExecutionResult(true, null, new Dictionary<string, object> { ["delivered"] = true });
    }
    private static string Require(ConnectorExecutionContext context, string key) => context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ServiceTaskExecutionException($"Email connector requires '{key}'.");
}

public class DatabaseConnectorExecutor : IConnectorExecutor
{
    public virtual string Type => "database";
    public async Task<ConnectorExecutionResult> ExecuteAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
    {
        var provider = Required(context, "vertex:connector.provider");
        var commandText = Required(context, "vertex:connector.commandText");
        if (string.IsNullOrEmpty(context.CredentialSecret)) throw new ServiceTaskExecutionException("Database connector requires a credential containing the connection string.");
        var factory = DbProviderFactories.GetFactory(provider);
        await using var connection = factory.CreateConnection() ?? throw new ServiceTaskExecutionException("Database provider did not create a connection.");
        connection.ConnectionString = context.CredentialSecret;
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.CommandTimeout = context.Attributes.TryGetValue("vertex:connector.commandTimeoutSeconds", out var rawTimeout) && int.TryParse(rawTimeout, out var timeout) ? Math.Clamp(timeout, 1, 300) : 30;
        AddParameters(command, context.Variables);
        var affected = await command.ExecuteNonQueryAsync(cancellationToken);
        return new ConnectorExecutionResult(true, null, new Dictionary<string, object> { ["affectedRows"] = affected });
    }

    private static void AddParameters(DbCommand command, IDictionary<string, object> variables)
    {
        foreach (var pair in variables.Where(pair => pair.Key.StartsWith("db.", StringComparison.Ordinal)))
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@" + pair.Key[3..];
            parameter.Value = pair.Value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
    private static string Required(ConnectorExecutionContext context, string key) => context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new ServiceTaskExecutionException($"Database connector requires '{key}'.");
}

public sealed class NamedEmailConnectorExecutor(string connectorType) : EmailConnectorExecutor
{
    public override string Type => connectorType;
}

public sealed class NamedDatabaseConnectorExecutor(string connectorType) : DatabaseConnectorExecutor
{
    public override string Type => connectorType;
}
