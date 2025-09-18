using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Studio.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProcessController : ControllerBase
    {
        private readonly IBpmnEngineService _bpmnEngineService;

        public ProcessController(IBpmnEngineService bpmnEngineService)
        {
            _bpmnEngineService = bpmnEngineService;
        }

        [HttpGet("definitions")]
        public async Task<IActionResult> GetProcessDefinitions()
        {
            var definitions = await _bpmnEngineService.GetProcessDefinitionsAsync();
            return Ok(definitions);
        }
    }
}
