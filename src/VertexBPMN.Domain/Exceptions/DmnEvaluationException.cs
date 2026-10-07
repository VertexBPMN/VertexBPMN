namespace VertexBPMN.Domain.Exceptions;

public class DmnEvaluationException : Exception
{
	public DmnEvaluationException(string m, Exception? i) : base(m, i) { }

	public DmnEvaluationException()
	{
	}

	public DmnEvaluationException(string? m) : base(m)
	{
	}
}
