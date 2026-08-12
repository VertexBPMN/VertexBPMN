namespace VertexBPMN.Domain.Exceptions;

public class DmnEvaluationException : Exception { public DmnEvaluationException(string m, Exception? i = null) : base(m, i) { } }