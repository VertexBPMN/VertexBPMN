namespace VertexBPMN.Domain.Exceptions;

public class DmnParseException : Exception
{
	public DmnParseException(string m, Exception? i) : base(m, i) { }

	public DmnParseException()
	{
	}

	public DmnParseException(string? m) : base(m)
	{
	}
}
