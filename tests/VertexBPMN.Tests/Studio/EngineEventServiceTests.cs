using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Studio;

public sealed class EngineEventServiceTests
{
	[Fact]
	public void Disposal_DetachesFromEngine_AndStopsForwarding()
	{
		var engine = new Mock<IBpmnEngineService>();
		var service = new EngineEventService(engine.Object);
		var received = new List<string>();
		service.OnEventEmitted += (_, args) => received.Add(args.Message);

		engine.Raise(value => value.OnEventEmitted += null, engine.Object, new EngineMessageEventArgs("deployed"));
		service.Dispose();
		engine.Raise(value => value.OnEventEmitted += null, engine.Object, new EngineMessageEventArgs("after-dispose"));

		Assert.Equal(["deployed"], received);
		engine.VerifyRemove(value => value.OnEventEmitted -= It.IsAny<EventHandler<EngineMessageEventArgs>>(), Times.Once);
	}
}
