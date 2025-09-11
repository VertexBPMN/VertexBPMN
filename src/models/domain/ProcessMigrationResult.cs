using System.Collections.Generic;

namespace VertexBPMN.Domain
{
    public class ProcessMigrationResult
    {
        public bool Success { get; set; }
        public List<string>? MigratedInstanceIds { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Warnings { get; set; }
    }
}
