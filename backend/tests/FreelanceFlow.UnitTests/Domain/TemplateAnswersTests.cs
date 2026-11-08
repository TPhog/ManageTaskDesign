using FluentAssertions;
using FreelanceFlow.Api.Domain.RequestTemplates;

namespace FreelanceFlow.UnitTests.Domain;

public class TemplateAnswersTests
{
    private static readonly DateOnly Today = new(2026, 11, 13);

    private static readonly IReadOnlyList<TemplateField> Fields = TemplateFieldRules.Normalize([
        new("headline", "Tiêu đề chính", TemplateFieldTypes.ShortText, true, null, []),
        new("details", "Nội dung chi tiết", TemplateFieldTypes.LongText, false, null, []),
        new("print", "Hình thức", TemplateFieldTypes.SingleChoice, false, null, ["In ấn", "Đăng online"]),
        new("info", "Thông tin bắt buộc có", TemplateFieldTypes.MultiChoice, false, null, ["Giá", "Địa chỉ", "Hotline"]),
        new("size", "Kích thước", TemplateFieldTypes.Size, true, null, ["A3 (297 × 420 mm)"]),
        new("deadline", "Hạn mong muốn", TemplateFieldTypes.Date, false, null, []),
        new("budget", "Ngân sách", TemplateFieldTypes.Number, false, null, []),
        new("references", "Ảnh tham khảo", TemplateFieldTypes.Images, false, null, []),
    ]);

    private static TemplateAnswerResult Eval(string json, int images = 0) => TemplateAnswers.Evaluate(Fields, json, images, Today);

    [Fact]
    public void Valid_answers_become_display_strings_in_field_order()   // BR-131
    {
        var r = Eval("""
            {"budget":1500000,"headline":"  Khai trương  ","info":["Hotline","Giá"],"size":{"preset":"A3 (297 × 420 mm)"},
             "deadline":"2026-11-20","print":"In ấn","unknown":"bỏ qua"}
            """, images: 2);

        r.IsValid.Should().BeTrue();
        r.Answers.Select(a => (a.Key, a.Display)).Should().Equal(
            ("headline", "Khai trương"), ("print", "In ấn"), ("info", "Giá, Hotline"), ("size", "A3 (297 × 420 mm)"),
            ("deadline", "20/11/2026"), ("budget", "1.500.000"), ("references", "2 ảnh đính kèm"));
        r.Answers[0].Label.Should().Be("Tiêu đề chính");
    }

    [Fact]
    public void Custom_size_uses_a_decimal_comma_and_a_known_unit()
    {
        Eval("""{"headline":"x","size":{"width":29.7,"height":42,"unit":"cm"}}""").Answers.Single(a => a.Key == "size")
            .Display.Should().Be("29,7 × 42 cm (tuỳ chỉnh)");
        Eval("""{"headline":"x","size":{"width":60,"height":160,"unit":"inch"}}""").Errors["size"].Should().Be("Đơn vị không hợp lệ");
        Eval("""{"headline":"x","size":{"width":0,"height":160,"unit":"cm"}}""").Errors.Should().ContainKey("size");
        Eval("""{"headline":"x","size":{"width":"60","height":160,"unit":"cm"}}""").Errors.Should().ContainKey("size");
    }

    [Fact]
    public void Required_missing_and_wrong_values_are_reported_per_field()   // BR-131
    {
        var r = Eval("""{"headline":"   ","print":"Tờ rơi","info":["Giá","Zalo"],"deadline":"2026-11-12","budget":-1}""");

        r.Errors.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            ["headline"] = "Bắt buộc",
            ["print"] = "Lựa chọn không hợp lệ",
            ["info"] = "Lựa chọn không hợp lệ",
            ["size"] = "Bắt buộc",
            ["deadline"] = "Chọn ngày từ hôm nay trở đi",
            ["budget"] = "Nhập số nguyên từ 0 đến 1.000.000.000.000",
        });
    }

    [Fact]
    public void Text_longer_than_the_limit_is_refused() =>
        Eval($$$"""{"headline":"{{{new string('a', 201)}}}","size":{"preset":"A3 (297 × 420 mm)"}}""")
            .Errors["headline"].Should().Be("Tối đa 200 ký tự");

    [Fact]
    public void Required_image_field_needs_at_least_one_file()
    {
        var fields = TemplateFieldRules.Normalize([new TemplateField("refs", "Ảnh", TemplateFieldTypes.Images, true, null, [])]);

        TemplateAnswers.Evaluate(fields, "{}", 0, Today).Errors["refs"].Should().Be("Đính kèm ít nhất 1 ảnh");
        TemplateAnswers.Evaluate(fields, null, 1, Today).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("[1,2]")]
    [InlineData("{not json")]
    public void Malformed_json_is_one_form_error(string json) =>
        Eval(json).Errors.Should().ContainKey(TemplateAnswers.FormErrorKey);

    [Fact]
    public void Renderer_writes_a_readable_body()   // BR-132
    {
        var r = Eval("""{"headline":"Khai trương","details":"Giảm 20%\nTặng ly","size":{"preset":"A3 (297 × 420 mm)"}}""", images: 2);

        TemplateRenderer.Render("Poster", r.Answers, "  Gửi trước thứ 6  ").Should().Be(
            "Mẫu: Poster\n- Tiêu đề chính: Khai trương\n- Nội dung chi tiết: Giảm 20%\n  Tặng ly\n- Kích thước: A3 (297 × 420 mm)\n" +
            "- Ảnh tham khảo: 2 ảnh đính kèm\n\nGhi chú thêm:\nGửi trước thứ 6");
        TemplateRenderer.Render(null, [], "Chỉ có lời nhắn").Should().Be("Chỉ có lời nhắn");
    }

    [Fact]
    public void Answers_round_trip_through_json()
    {
        var answers = Eval("""{"headline":"Khai trương","size":{"preset":"A3 (297 × 420 mm)"}}""").Answers;

        TemplateAnswers.ParseAnswers(TemplateAnswers.SerializeAnswers(answers)).Should().BeEquivalentTo(answers);
    }
}
