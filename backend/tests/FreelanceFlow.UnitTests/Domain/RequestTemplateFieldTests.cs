using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.RequestTemplates;

namespace FreelanceFlow.UnitTests.Domain;

public class RequestTemplateFieldTests
{
    private static TemplateField F(string key, string type, bool required = false, params string[] options) =>
        new(key, "Câu " + key, type, required, null, options);

    [Fact]
    public void Normalize_trims_and_keeps_the_order()   // BR-130
    {
        var fields = TemplateFieldRules.Normalize([
            new TemplateField(" headline ", "  Tiêu đề chính ", TemplateFieldTypes.ShortText, true, "  ", []),
            F("style", TemplateFieldTypes.SingleChoice, false, " Hiện đại ", "Sang trọng"),
        ]);

        fields.Select(f => f.Key).Should().Equal("headline", "style");
        fields[0].Label.Should().Be("Tiêu đề chính");
        fields[0].Help.Should().BeNull();
        fields[1].Options.Should().Equal("Hiện đại", "Sang trọng");
    }

    [Theory]
    [InlineData("Tiêu Đề")]
    [InlineData("")]
    public void Key_must_be_lowercase_ascii(string key) =>
        Invoking(() => TemplateFieldRules.Normalize([F(key, TemplateFieldTypes.ShortText)])).Code.Should().Be("VALIDATION_FAILED");

    [Fact]
    public void Duplicate_keys_choice_without_options_text_with_options_and_two_image_fields_are_refused()
    {
        Invoking(() => TemplateFieldRules.Normalize([F("a", TemplateFieldTypes.ShortText), F("a", TemplateFieldTypes.LongText)]))
            .Detail.Should().Contain("Câu hỏi 2");
        Invoking(() => TemplateFieldRules.Normalize([F("a", TemplateFieldTypes.SingleChoice)])).Detail.Should().Contain("lựa chọn");
        Invoking(() => TemplateFieldRules.Normalize([F("a", TemplateFieldTypes.ShortText, false, "x")])).Detail.Should().Contain("không có lựa chọn");
        Invoking(() => TemplateFieldRules.Normalize([F("a", TemplateFieldTypes.MultiChoice, false, "Giá", "giá")])).Detail.Should().Contain("trùng");
        Invoking(() => TemplateFieldRules.Normalize([F("a", TemplateFieldTypes.Images), F("b", TemplateFieldTypes.Images)]))
            .Detail.Should().Contain("ảnh");
        Invoking(() => TemplateFieldRules.Normalize(
                Enumerable.Range(0, 31).Select(i => (TemplateField?)F($"f{i}", TemplateFieldTypes.ShortText)).ToList()))
            .Detail.Should().Contain("30");
    }

    [Fact]
    public void Size_may_have_no_presets_because_custom_is_always_offered() =>
        TemplateFieldRules.Normalize([F("size", TemplateFieldTypes.Size)]).Single().Options.Should().BeEmpty();

    [Fact]
    public void Fields_round_trip_through_json()
    {
        var t = RequestTemplate.Create("Poster", "In hoặc online", "POSTER", null, true,
            [F("size", TemplateFieldTypes.Size, true, "A3 (297 × 420 mm)")], sortOrder: 70);

        t.Fields().Should().BeEquivalentTo([new TemplateField("size", "Câu size", "SIZE", true, null, ["A3 (297 × 420 mm)"])]);
        t.FieldsJson.Should().Contain("\"key\":\"size\"").And.Contain("A3 (297 × 420 mm)");   // camelCase, readable UTF-8
        t.SortOrder.Should().Be(70);
    }

    [Theory]
    [InlineData("", "POSTER")]
    [InlineData("Poster", "FLYER")]
    public void Template_needs_a_name_and_a_project_design_type(string name, string designType) =>
        Invoking(() => RequestTemplate.Create(name, null, designType, null, true, [], 10)).Code.Should().Be("VALIDATION_FAILED");

    [Fact]
    public void Every_preset_is_valid_and_matches_decision_1()
    {
        RequestTemplatePresets.All.Select(p => p.Name).Should().Equal(
            "Banner quảng cáo", "Poster", "Menu cửa hàng", "Banner sự kiện / chương trình", "Bài đăng mạng xã hội", "Logo");
        foreach (var p in RequestTemplatePresets.All)
        {
            var fields = TemplateFieldRules.Normalize(p.Fields.Cast<TemplateField?>().ToList());
            fields.Should().Contain(f => f.Type == TemplateFieldTypes.Images, p.Name);
            fields.Should().Contain(f => f.Type == TemplateFieldTypes.Date, p.Name);
            fields.Select(f => f.Key).Should().NotContain("notes");   // PortalIsolationTests forbids the JSON token "notes"
        }
        RequestTemplatePresets.All[0].Fields.Single(f => f.Key == "size").Options.Should().Contain(
            ["1080 × 1080 px (vuông)", "1200 × 628 px (quảng cáo Facebook)", "1080 × 1920 px (story/reels)", "60 × 160 cm (standee cuốn)"]);
        RequestTemplatePresets.All[1].Fields.Single(f => f.Key == "size").Options.Should().Contain(["A4 (210 × 297 mm)", "A3 (297 × 420 mm)"]);
    }

    [Fact]
    public void Seed_sql_inserts_six_rows_with_escaped_literals()
    {
        var sql = RequestTemplatePresets.SeedSql();

        sql.Split("INSERT INTO request_templates").Length.Should().Be(7);
        sql.Should().Contain("'Menu cửa hàng'").And.Contain("::jsonb").And.Contain(", 60, now(), now());");
    }

    private static DomainException Invoking(Action act) =>
        FluentActions.Invoking(act).Should().Throw<DomainException>().Which;
}
