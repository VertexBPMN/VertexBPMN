using System.Collections.Generic;
namespace VertexBPMN.Core.Domain
{
    public class SemanticValidationResult
    {
        public bool IsValid { get; set; }
        public List<string>? Errors { get; set; }
        public List<string>? Warnings { get; set; }
        public List<string>? Suggestions { get; set; }
    }
}
