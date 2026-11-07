namespace FreelanceFlow.Api.Infrastructure.VietQr;

public static class Crc16Ccitt
{
    public static string Compute(string data)
    {
        ushort crc = 0xFFFF;
        foreach (byte b in System.Text.Encoding.ASCII.GetBytes(data))
        {
            crc ^= (ushort)(b << 8);
            for (int i = 0; i < 8; i++)
                crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
        }
        return crc.ToString("X4");
    }
}
