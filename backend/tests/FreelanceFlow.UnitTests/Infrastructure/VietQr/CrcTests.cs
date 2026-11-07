using FreelanceFlow.Api.Infrastructure.VietQr;

namespace FreelanceFlow.UnitTests.Infrastructure.VietQr;

public class CrcTests
{
    [Fact]
    public void Standard_check_value() =>
        Assert.Equal("29B1", Crc16Ccitt.Compute("123456789"));
}
