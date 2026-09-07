using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    [ApiExplorerSettings(GroupName = "Diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly ISemanticValidationService _validationService;
        public DiagnosticsController(ISemanticValidationService validationService)
        {
            _validationService = validationService;
        }

        [HttpPost("bpmn")]
        public async Task<ActionResult<SemanticValidationResult>> ValidateBpmn([FromBody] string bpmnXml)
        {
            var result = await _validationService.ValidateBpmnAsync(bpmnXml, HttpContext.RequestAborted);
            return Ok(result);
        }

        [HttpPost("dmn")]
        public ActionResult<SemanticValidationResult> ValidateDmn([FromBody] string dmnXml)
        {
            var result = _validationService.ValidateDmn(dmnXml);
            return Ok(result);
        }
    }
}
