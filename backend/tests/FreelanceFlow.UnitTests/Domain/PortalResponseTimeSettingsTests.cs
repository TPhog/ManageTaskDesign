using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Settings;

namespace FreelanceFlow.UnitTests.Domain;

public class PortalResponseTimeSettingsTests
{
    [Fact]
    public void Response_time_defaults_trims_resets_and_is_capped()
    {
        var s = new Settings();
        s.PortalResponseTime.Should().Be("trong 24 giờ");

        s.SetPortalResponseTime("  trong 2 giờ làm việc ");
        s.PortalResponseTime.Should().Be("trong 2 giờ làm việc");
        s.SetPortalResponseTime(null);
        s.PortalResponseTime.Should().Be("trong 2 giờ làm việc", "null keeps the current value");
        s.SetPortalResponseTime("   ");
        s.PortalResponseTime.Should().Be(Settings.DefaultPortalResponseTime);

        var act = () => s.SetPortalResponseTime(new string('x', 61));
        act.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }
}
