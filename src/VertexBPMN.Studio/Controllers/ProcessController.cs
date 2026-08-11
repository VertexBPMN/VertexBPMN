using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Studio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProcessController : ControllerBase
    {
        private readonly IRepositoryService _repositoryService;

        public ProcessController(IRepositoryService repositoryService)
        {
            _repositoryService = repositoryService;
        }

        [HttpGet("definitions")]
        public async Task<IActionResult> GetProcessDefinitions()
        {
            var definitions = await _repositoryService.GetProcessDefinitionsAsync();
            return Ok(definitions);
        }
    }
}
