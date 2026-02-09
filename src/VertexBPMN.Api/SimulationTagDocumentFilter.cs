using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace VertexBPMN.Api
{
    public class SimulationTagDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Tags == null)
                swaggerDoc.Tags = new HashSet<OpenApiTag>();
            swaggerDoc.Tags.Add(new OpenApiTag
            {
                Name = "Simulation",
                Description = "BPMN process simulation endpoints"
            });
        }
    }
}
