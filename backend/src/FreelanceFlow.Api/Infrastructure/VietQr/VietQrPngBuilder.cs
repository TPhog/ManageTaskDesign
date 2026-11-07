using QRCoder;

namespace FreelanceFlow.Api.Infrastructure.VietQr;

public static class VietQrPngBuilder
{
    public static byte[] BuildPng(string payload, int pixelsPerModule = 4)
    {
        var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrData);
        return qrCode.GetGraphic(pixelsPerModule, new byte[] { 0, 0, 0 }, new byte[] { 255, 255, 255 });
    }
}
