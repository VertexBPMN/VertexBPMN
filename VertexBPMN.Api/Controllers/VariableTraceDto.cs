namespace VertexBPMN.Api.Controllers
{
    public class VariableTraceDto
    {
        public int StepNumber { get; set; }
        public string? ActivityId { get; set; }
        public object? Value { get; set; }
    }
}
