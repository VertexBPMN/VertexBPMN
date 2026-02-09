namespace VertexBPMN.Domain.Exceptions;

public class DmnParseException : Exception { public DmnParseException(string m, Exception? i = null) : base(m, i) { } }