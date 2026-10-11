using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using VertexBPMN.Api.Controllers;
using VertexBPMN.Api.Dto;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Api;

public sealed class SimulationTenantContextTests
{
	[Fact]
	public async Task ListingWithoutTenantClaimDoesNotQueryGlobalScenarios()
	{
		var service = new Mock<ISimulationScenarioService>(MockBehavior.Strict);
		var controller = new SimulationScenarioController(service.Object)
		{
			ControllerContext = MissingTenantContext()
		};

		Assert.IsType<ForbidResult>((await controller.GetAll()).Result);
		service.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task CreatingWithoutTenantClaimDoesNotPersistCallerSuppliedTenant()
	{
		var service = new Mock<ISimulationScenarioService>(MockBehavior.Strict);
		var controller = new SimulationScenarioController(service.Object)
		{
			ControllerContext = MissingTenantContext()
		};

		var response = await controller.Create(new SimulationScenarioDto { TenantId = "tenant-b" });

		Assert.IsType<ForbidResult>(response.Result);
		service.VerifyNoOtherCalls();
	}

	[Fact]
	public async Task SimulatingWithoutTenantClaimDoesNotValidateOrExecuteCallerSuppliedTenant()
	{
		var simulation = new Mock<ISimulationService>(MockBehavior.Strict);
		var scenarios = new Mock<ISimulationScenarioService>(MockBehavior.Strict);
		var validation = new Mock<ISemanticValidationService>(MockBehavior.Strict);
		var controller = new SimulationController(simulation.Object, scenarios.Object, validation.Object)
		{
			ControllerContext = MissingTenantContext()
		};

		var response = await controller.Simulate(new SimulationRequestDto { TenantId = "tenant-b" });

		Assert.IsType<ForbidResult>(response.Result);
		simulation.VerifyNoOtherCalls();
		scenarios.VerifyNoOtherCalls();
		validation.VerifyNoOtherCalls();
	}

	private static ControllerContext MissingTenantContext() => new()
	{
		HttpContext = new DefaultHttpContext
		{
			User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "test-user")], "test"))
		}
	};
}
