using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace VertexBPMN.Api;
internal class SimulationTagDocumentFilter : IDocumentFilter
{
	public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
	{
		swaggerDoc.Tags ??= new HashSet<OpenApiTag>();

		swaggerDoc.Tags.Add(new OpenApiTag { Name = "Simulation", Description = "BPMN process simulation endpoints" });
	}
}
