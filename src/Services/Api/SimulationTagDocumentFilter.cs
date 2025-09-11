using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Generic;

namespace VertexBPMN.Api
{
    public class SimulationTagDocumentFilter : IDocumentFilter
    {
        public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
        {
            if (swaggerDoc.Tags == null)
                swaggerDoc.Tags = new List<OpenApiTag>();
            swaggerDoc.Tags.Add(new OpenApiTag
            {
                Name = "Simulation",
                Description = "BPMN process simulation endpoints"
            });
        }
    }
}
