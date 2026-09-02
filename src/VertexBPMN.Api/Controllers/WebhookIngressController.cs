using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Api.Controllers;

/// <summary>Public ingress for BPMN-declared vertex:webhook start events.</summary>
[ApiController]
[Route("api/webhooks")]
public sealed class WebhookIngressController(IWorkflowTriggerService triggerService) : ControllerBase
{
    [AcceptVerbs("GET", "POST", "PUT", "PATCH", "DELETE")]
    [AllowAnonymous]
    [Route("{**path}")]
    public async Task<ActionResult> Invoke(
        string? path,
        [FromHeader(Name = "X-VertexBPMN-Trigger-Secret")] string? triggerSecret,
        [FromHeader(Name = "X-VertexBPMN-Signature")] string? signature,
        CancellationToken cancellationToken)
    {
        await using var content = new MemoryStream();
        await Request.Body.CopyToAsync(content, cancellationToken);
        var result = await triggerService.InvokeWebhookAsync("/" + (path ?? string.Empty), Request.Method, triggerSecret, signature, content.ToArray(), cancellationToken);
        return result.Status switch
        {
            WorkflowTriggerInvocationStatus.Started => Created($"/api/runtime/{result.ProcessInstance!.Id}", result.ProcessInstance),
            WorkflowTriggerInvocationStatus.InvalidSecret => Unauthorized(),
            WorkflowTriggerInvocationStatus.InvalidPayload => BadRequest(new ProblemDetails { Title = "Webhook payload does not match its declared schema." }),
            WorkflowTriggerInvocationStatus.NotFound or WorkflowTriggerInvocationStatus.Disabled => NotFound(),
            WorkflowTriggerInvocationStatus.ProcessDefinitionNotFound => UnprocessableEntity(new ProblemDetails { Title = "Process definition not found" }),
            _ => Problem("The webhook could not be invoked.")
        };
    }
}
