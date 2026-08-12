namespace VertexBPMN.Domain.Entities
{
    public class ProcessMigrationResult
    {
        public bool Success { get; set; }
        public List<string>? MigratedInstanceIds { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Warnings { get; set; }
    }
}
