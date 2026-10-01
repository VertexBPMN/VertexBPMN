using Shouldly;
using System.Net;
using Moq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Studio
{
    public class ActiveEngineServiceTests
    {
        [Fact]
        public void Initial_State_Should_Have_Defaults()
        {
            var svc = new ActiveEngineService();
            svc.ActiveEngineId.ShouldBe("engine1");
            svc.IsConnected.ShouldBeFalse();
            svc.LastConnectionCheck.ShouldBe(DateTime.MinValue);
        }

        [Fact]
        public void Setting_EngineId_Should_Raise_Events()
        {
            var svc = new ActiveEngineService();
            string? raisedEngineId = null;
            bool propertyChanged = false;
            bool genericChanged = false;

            svc.OnEngineChanged += id => raisedEngineId = id;
            svc.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ActiveEngineService.ActiveEngineId)) propertyChanged = true; };
            svc.OnChange += () => genericChanged = true;

            svc.ActiveEngineId = "engine42";

            raisedEngineId.ShouldBe("engine42");
            propertyChanged.ShouldBeTrue();
            genericChanged.ShouldBeTrue();
        }

        [Fact]
        public void Setting_Same_Value_Should_Not_Raise_Events()
        {
            var svc = new ActiveEngineService();
            int propertyChangedCount = 0;
            int onChangeCount = 0;

            svc.PropertyChanged += (_, __) => propertyChangedCount++;
            svc.OnChange += () => onChangeCount++;

            // assign same value
            svc.ActiveEngineId = svc.ActiveEngineId;

            propertyChangedCount.ShouldBe(0);
            onChangeCount.ShouldBe(0);
        }

        [Fact]
        public async Task Engine_Name_Alone_Must_Not_Report_Connected()
        {
            var svc = new ActiveEngineService();
            svc.ActiveEngineId = "engine-live";
            svc.IsConnected.ShouldBeFalse();
            await svc.CheckConnectionAsync(TestContext.Current.CancellationToken);
            svc.IsConnected.ShouldBeFalse();
            svc.ConnectionStatus.ShouldBe("Disconnected");
            svc.LastConnectionCheck.ShouldBeGreaterThan(DateTime.MinValue);
        }

        [Theory]
        [InlineData(HttpStatusCode.OK, true, "Connected")]
        [InlineData(HttpStatusCode.ServiceUnavailable, false, "Not ready")]
        [InlineData(HttpStatusCode.Unauthorized, false, "Unauthorized")]
        [InlineData(HttpStatusCode.Forbidden, false, "Unauthorized")]
        public async Task Connection_State_Comes_From_Real_Readiness_Response(HttpStatusCode status, bool connected, string label)
        {
            var factory = new Mock<IHttpClientFactory>();
            var handler = new ReadinessHandler(_ => new HttpResponseMessage(status));
            factory.Setup(value => value.CreateClient("VertexBPMN.Api"))
                .Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test") });
            using var svc = new ActiveEngineService(factory.Object);
            await svc.CheckConnectionAsync(TestContext.Current.CancellationToken);
            svc.IsConnected.ShouldBe(connected);
            svc.ConnectionStatus.ShouldBe(label);
            svc.IsChecking.ShouldBeFalse();
            handler.RequestUri.ShouldBe(new Uri("http://api.test/api/ready"));
        }

        [Fact]
        public async Task Network_Failure_Clears_Old_Success_And_Refresh_Can_Recover()
        {
            var fail = false;
            var factory = new Mock<IHttpClientFactory>();
            using var handler = new ReadinessHandler(_ => fail ? throw new HttpRequestException("offline") : new HttpResponseMessage(HttpStatusCode.OK));
            factory.Setup(value => value.CreateClient("VertexBPMN.Api"))
                .Returns(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test") });
            using var svc = new ActiveEngineService(factory.Object);
            await svc.CheckConnectionAsync(TestContext.Current.CancellationToken);
            svc.IsConnected.ShouldBeTrue();
            fail = true;
            await svc.CheckConnectionAsync(TestContext.Current.CancellationToken);
            svc.IsConnected.ShouldBeFalse();
            svc.ConnectionStatus.ShouldBe("Disconnected");
            fail = false;
            await svc.CheckConnectionAsync(TestContext.Current.CancellationToken);
            svc.IsConnected.ShouldBeTrue();
        }

        private sealed class ReadinessHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
        {
            public Uri? RequestUri { get; private set; }
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestUri = request.RequestUri;
                return Task.FromResult(response(request));
            }
        }

        [Fact]
        public void Dispose_Should_Clear_Handlers()
        {
            var svc = new ActiveEngineService();
            bool changeCalled = false;
            svc.OnChange += () => changeCalled = true;
            svc.Dispose();
            svc.ActiveEngineId = "another"; // would invoke if not cleared
            changeCalled.ShouldBeFalse();
        }
    }
}
