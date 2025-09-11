using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Core.Services;
using VertexBPMN.Core.Domain;

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
        public ActionResult<SemanticValidationResult> ValidateBpmn([FromBody] string bpmnXml)
        {
            var result = _validationService.ValidateBpmn(bpmnXml);
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
