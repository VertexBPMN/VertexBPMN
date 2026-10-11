using Microsoft.AspNetCore.Mvc;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace VertexBPMN.Api.Controllers;
/// <summary>
/// Load balancer controller for distributed process execution
/// Olympic-level feature: Enterprise Scalability - Load balancing
/// </summary>
[ApiController]
[Route("api/load-balancer")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public controller required for ASP.NET Core MVC discovery and activation.")]
public class LoadBalancerController : ControllerBase
{
	private readonly ILoadBalancingService _loadBalancer;
	private readonly ILogger<LoadBalancerController> _logger;
	public LoadBalancerController(IDistributedProcessEngine distributedEngine, ILoadBalancingService loadBalancer, ILogger<LoadBalancerController> logger)
	{
		ArgumentNullException.ThrowIfNull(distributedEngine);
		_loadBalancer = loadBalancer;
		_logger = logger;
	}

	/// <summary>
	/// Get current load balancing status
	/// </summary>
	[HttpGet("status")]
	public async Task<IActionResult> GetStatus()
	{
		var status = await _loadBalancer.GetStatusAsync();
		return Ok(status);
	}

	/// <summary>
	/// Register a new worker node
	/// </summary>
	[HttpPost("workers")]
	[Authorize(Policy = "AdminOnly")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public IActionResult RegisterWorker([FromBody] WorkerRegistrationRequest request)
	{
		var worker = new WorkerNode(request.WorkerId, request.HostName, request.Port, DateTime.UtcNow, request.SupportedNodeTypes, 0, request.MaxCapacity);
		_loadBalancer.RegisterWorker(worker);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Registered worker {WorkerId} at {HostName}:{Port}", worker.Id, worker.HostName, worker.Port);
		}
		return Ok(new { Message = "Worker registered successfully", WorkerId = worker.Id });
	}

	/// <summary>
	/// Unregister a worker node
	/// </summary>
	[HttpDelete("workers/{workerId}")]
	[Authorize(Policy = "AdminOnly")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public IActionResult UnregisterWorker(string workerId)
	{
		_loadBalancer.UnregisterWorker(workerId);
		if (_logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Information))
		{
			_logger.LogInformation("Unregistered worker {WorkerId}", workerId);
		}
		return Ok(new { Message = "Worker unregistered successfully" });
	}

	/// <summary>
	/// Update worker heartbeat
	/// </summary>
	[HttpPost("workers/{workerId}/heartbeat")]
	[Authorize(Policy = "AdminOnly")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public IActionResult UpdateHeartbeat(string workerId, [FromBody] WorkerHeartbeatRequest request)
	{
		_loadBalancer.UpdateWorkerHeartbeat(workerId, request.CurrentLoad);
		return Ok(new { Message = "Heartbeat updated", Timestamp = DateTime.UtcNow });
	}

	/// <summary>
	/// Get list of all workers
	/// </summary>
	[HttpGet("workers")]
	public async Task<IActionResult> GetWorkers()
	{
		var workers = await _loadBalancer.GetWorkersAsync();
		return Ok(workers);
	}

	/// <summary>
	/// Get worker health status
	/// </summary>
	[HttpGet("workers/{workerId}/health")]
	public async Task<IActionResult> GetWorkerHealth(string workerId)
	{
		var health = await _loadBalancer.GetWorkerHealthAsync(workerId);
		if (health == null)
		{
			return NotFound(new { Message = "Worker not found" });
		}

		return Ok(health);
	}

	/// <summary>
	/// Rebalance workload across workers
	/// </summary>
	[HttpPost("rebalance")]
	[Authorize(Policy = "AdminOnly")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public async Task<IActionResult> Rebalance()
	{
		var result = await _loadBalancer.RebalanceAsync();
		return Ok(result);
	}

	/// <summary>
	/// Get load balancing configuration
	/// </summary>
	[HttpGet("config")]
	[ProducesResponseType(typeof(LoadBalancingConfig), StatusCodes.Status200OK)]
	public IActionResult GetConfig()
	{
		var config = _loadBalancer.GetConfiguration();
		return Ok(config);
	}

	/// <summary>
	/// Update load balancing configuration
	/// </summary>
	[HttpPut("config")]
	[Authorize(Policy = "AdminOnly")]
	[ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
	public IActionResult UpdateConfig([FromBody] LoadBalancingConfig config)
	{
		_loadBalancer.UpdateConfiguration(config);
		return Ok(new { Message = "Configuration updated successfully" });
	}
}
