namespace VertexBPMN.Core.Exceptions;

public class DmnEvaluationException : Exception { public DmnEvaluationException(string m, Exception? i = null) : base(m, i) { } }