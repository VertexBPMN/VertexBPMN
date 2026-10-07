using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using VertexBPMN.Domain.Exceptions;

namespace VertexBPMN.Application.Connectors;

public sealed class NamedDatabaseConnectorExecutor(string connectorType) : DatabaseConnectorExecutor
{
	public override string Type => connectorType;
}
