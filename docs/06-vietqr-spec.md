# 06 — VietQR Specification

App tự sinh **payload VietQR** (chuẩn EMVCo MPM do NAPAS áp dụng) ở backend. Không gọi API bên thứ ba,
không qua cổng thanh toán. Khách quét bằng bất kỳ app ngân hàng/ví hỗ trợ VietQR → app ngân hàng điền sẵn
người nhận, số tiền, nội dung chuyển khoản.

> App **không** xác nhận được tiền đã về. Designer tự kiểm tra trên app ngân hàng và đánh dấu
> `payment_status` (02-business-rules §8.4).

## 1. Cấu trúc TLV

Mỗi trường = `ID (2 ký tự)` + `LENGTH (2 chữ số, số ký tự của VALUE)` + `VALUE`.

| ID | Tên | Giá trị |
|---|---|---|
| `00` | Payload Format Indicator | `01` |
| `01` | Point of Initiation Method | `12` = QR động (có số tiền) · `11` = QR tĩnh |
| `38` | Merchant Account Information (VietQR) | Template con, xem dưới |
| `53` | Transaction Currency | `704` (VND) |
| `54` | Transaction Amount | Số nguyên, không dấu phân cách, VD `1200000` |
| `58` | Country Code | `VN` |
| `62` | Additional Data Field Template | Template con: `08` = Purpose of Transaction (nội dung CK) |
| `63` | CRC | 4 ký tự HEX in hoa |

Template con của `38`:

| ID | Tên | Giá trị |
|---|---|---|
| `00` | GUID | `A000000727` |
| `01` | Beneficiary Organization | Template con: `00` = BIN ngân hàng (6 số), `01` = số tài khoản |
| `02` | Service Code | `QRIBFTTA` (chuyển tới **tài khoản**) |

Thứ tự trường: `00, 01, 38, 53, 54, 58, 62, 63`.

## 2. CRC

- Thuật toán **CRC-16/CCITT-FALSE**: poly `0x1021`, init `0xFFFF`, không reflect, xorout `0x0000`.
- Tính trên toàn bộ chuỗi **bao gồm** `6304` (ID + length của trường CRC), rồi nối 4 ký tự HEX in hoa.
- Kiểm tra thuật toán: `CRC("123456789") = 29B1`.

```csharp
public static string Crc16Ccitt(string data)
{
    ushort crc = 0xFFFF;
    foreach (byte b in Encoding.ASCII.GetBytes(data))
    {
        crc ^= (ushort)(b << 8);
        for (int i = 0; i < 8; i++)
            crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
    }
    return crc.ToString("X4");
}
```

## 3. Builder

```csharp
public sealed record VietQrRequest(string Bin, string AccountNo, long? Amount, string? Memo);

public static class VietQrPayloadBuilder
{
    static string Tlv(string id, string value)
    {
        if (value.Length > 99) throw new ArgumentException($"TLV {id} too long");
        return id + value.Length.ToString("D2") + value;
    }

    public static string Build(VietQrRequest r)
    {
        var beneficiary = Tlv("00", r.Bin) + Tlv("01", r.AccountNo);
        var merchant    = Tlv("00", "A000000727") + Tlv("01", beneficiary) + Tlv("02", "QRIBFTTA");
        var sb = new StringBuilder()
            .Append(Tlv("00", "01"))
            .Append(Tlv("01", r.Amount is > 0 ? "12" : "11"))
            .Append(Tlv("38", merchant))
            .Append(Tlv("53", "704"));
        if (r.Amount is > 0) sb.Append(Tlv("54", r.Amount.Value.ToString(CultureInfo.InvariantCulture)));
        sb.Append(Tlv("58", "VN"));
        if (!string.IsNullOrEmpty(r.Memo)) sb.Append(Tlv("62", Tlv("08", r.Memo)));
        sb.Append("6304");
        var s = sb.ToString();
        return s + Crc16Ccitt(s);
    }
}
```

## 4. Nội dung chuyển khoản (memo)

Format: `{NUMBER_NO_DASH} {SUFFIX}`

| purpose | SUFFIX | Ví dụ |
|---|---|---|
| `deposit` | `COC` | `BG20260007 COC` |
| `remaining` | `CONLAI` | `BG20260007 CONLAI` |
| `full` | `TT` | `BG20260007 TT` |

Ràng buộc (validate trước khi build):
- Chỉ `A–Z`, `0–9`, khoảng trắng; **không dấu tiếng Việt**, không ký tự đặc biệt (nhiều ngân hàng cắt/lỗi).
- Tối đa **25 ký tự** (giới hạn phổ biến của trường 62.08).
- Không dùng version trong memo — mã báo giá là đủ để đối soát.

## 5. Dữ liệu nguồn

- `Bin`, `AccountNo` lấy từ **`quotation.snapshot.bank`** (không lấy từ settings hiện tại) → QR của báo giá
  đã gửi không đổi khi designer đổi tài khoản.
- `Amount` theo bảng BR-75 (02-business-rules §8.5).
- Nếu settings chưa có tài khoản ngân hàng → không cho `send` báo giá (`422 BUSINESS_RULE`, "Chưa cấu hình tài khoản nhận tiền").
- Validate `AccountNo`: `^[0-9A-Za-z]{4,19}$`; `Bin`: 6 chữ số và tồn tại trong bảng `banks`.

## 6. Render

- API trả `payload` (string) — frontend render bằng thư viện `qrcode`, error correction **M**, kích thước ≥ 240px trên mobile.
- API PNG (`.../qr/{purpose}.png`) dùng QRCoder, 512×512, quiet zone 4 module — để khách tải về/chia sẻ.
- Dưới QR luôn hiển thị dạng text: ngân hàng, số TK (nút copy), tên chủ TK, số tiền (định dạng `1.200.000 ₫`),
  nội dung CK (nút copy) — cho trường hợp khách không quét được.
- Hiển thị logo VietQR/NAPAS là **tùy chọn**; nếu dùng phải tuân thủ guideline thương hiệu của NAPAS.

### 6.1 Portal (Plan 12)

Payload và nội dung CK ở §2–§4 **không đổi**. Màn báo giá trên Portal thêm:

- Khối "Bây giờ cần chuyển" nêu khoản đang cần trả (cọc / còn lại / toàn bộ theo BR-75); khi chọn được giữa cọc và toàn bộ thì là nhóm radio (một điểm Tab, phím mũi tên đổi lựa chọn). Báo giá đã hết hạn **không** hiện số tiền cần chuyển, QR hay nút "Tôi đã chuyển khoản" - chỉ có lời nhắn liên hệ nhà thiết kế.
- Nút sao chép cho 5 dòng: ngân hàng, số TK, tên chủ TK, số tiền, nội dung CK.
- Nút "Lưu ảnh QR" (`.../qr/{purpose}.png`) kèm hướng dẫn quét bằng ảnh đã lưu ngay trên điện thoại (mở app ngân hàng → quét từ ảnh).
- Giải thích nội dung CK bằng lời: `COC` = cọc, `CONLAI` = còn lại, `TT` = thanh toán (giúp nhà thiết kế nhận ra khoản tiền).
- Nút "Tôi đã chuyển khoản" (BR-77) chỉ báo cho nhà thiết kế; app không xử lý hay xác minh tiền.

## 7. Test vectors (đã tính và kiểm CRC)

BIN `970436`, số TK `0123456789`:

| # | Amount | Memo | Payload |
|---|---|---|---|
| 1 | 1200000 | `BG20260007 COC` | `00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA5303704540712000005802VN62180814BG20260007 COC6304FCA9` |
| 2 | 2400000 | `BG20260007 TT` | `00020101021238540010A00000072701240006970436011001234567890208QRIBFTTA5303704540724000005802VN62170813BG20260007 TT63041421` |
| 3 | — (QR tĩnh) | — | `00020101021138540010A00000072701240006970436011001234567890208QRIBFTTA53037045802VN63046A15` |

Unit test bắt buộc:
1. `Crc16Ccitt("123456789") == "29B1"`.
2. 3 vector trên khớp từng ký tự.
3. Memo có dấu / > 25 ký tự / ký tự đặc biệt → ném `ArgumentException`.
4. Amount ≤ 0 → QR tĩnh (`01` = `11`, không có trường `54`).

**Kiểm thử thủ công trước go-live (bắt buộc):** quét QR thật bằng ít nhất 3 app ngân hàng khác nhau
(VD Vietcombank, MB, Techcombank) với tài khoản thật của designer; xác nhận tên người nhận, số tiền,
nội dung hiển thị đúng; chuyển thử 1 khoản nhỏ.
