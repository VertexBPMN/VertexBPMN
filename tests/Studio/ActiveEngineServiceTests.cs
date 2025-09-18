using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Shouldly;
using Xunit;
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
            svc.CurrentUserRole.ShouldBe("Admin");
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
        public void Setting_UserRole_Should_Raise_Events()
        {
            var svc = new ActiveEngineService();
            string? raisedRole = null;
            bool propertyChanged = false;
            bool genericChanged = false;

            svc.OnUserRoleChanged += role => raisedRole = role;
            svc.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ActiveEngineService.CurrentUserRole)) propertyChanged = true; };
            svc.OnChange += () => genericChanged = true;

            svc.CurrentUserRole = "Reader";

            raisedRole.ShouldBe("Reader");
            propertyChanged.ShouldBeTrue();
            genericChanged.ShouldBeTrue();
        }

        [Fact]
        public async Task Setting_Same_Value_Should_Not_Raise_Events()
        {
            var svc = new ActiveEngineService();
            int propertyChangedCount = 0;
            int onChangeCount = 0;

            svc.PropertyChanged += (_, __) => propertyChangedCount++;
            svc.OnChange += () => onChangeCount++;

            // assign same value
            svc.ActiveEngineId = svc.ActiveEngineId;
            svc.CurrentUserRole = svc.CurrentUserRole;

            // allow potential async actions to run
            await Task.Delay(150);

            propertyChangedCount.ShouldBe(0);
            onChangeCount.ShouldBe(0);
        }

        [Fact]
        public async Task Connection_Check_Should_Update_State()
        {
            var svc = new ActiveEngineService();
            svc.ActiveEngineId = "engine-live"; // triggers async check
            await Task.Delay(150); // wait for CheckConnectionAsync
            svc.IsConnected.ShouldBeTrue();
            svc.LastConnectionCheck.ShouldBeGreaterThan(DateTime.MinValue);
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
