using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.RequestTemplates;

namespace FreelanceFlow.UnitTests.Domain;

public class IntakeSubmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
    private static readonly TemplateAnswer Headline = new("headline", "Tiêu đề chính", TemplateFieldTypes.ShortText, "Khai trương");

    private static IntakeSubmission NewOne() =>
        IntakeSubmission.Create("Lan", "0909123456", null, Guid.NewGuid(), "Poster", [Headline], null);

    [Fact]
    public void New_submission_renders_the_body_and_keeps_the_snapshot()   // BR-134
    {
        var s = IntakeSubmission.Create("  Lan Anh ", " 0909.123.456 ", " lan@example.com ", Guid.NewGuid(), "Poster", [Headline], " Gấp ");

        s.Status.Should().Be(IntakeStatuses.New);
        s.ContactName.Should().Be("Lan Anh");
        s.ContactPhone.Should().Be("0909.123.456");
        s.ContactEmail.Should().Be("lan@example.com");
        s.Note.Should().Be("Gấp");
        s.Body.Should().Be("Mẫu: Poster\n- Tiêu đề chính: Khai trương\n\nGhi chú thêm:\nGấp");
        s.Answers().Should().Equal(Headline);
    }

    [Theory]
    [InlineData("", "0909123456", null, "contactName")]
    [InlineData("Lan", "12345", null, "contactPhone")]
    [InlineData("Lan", "0909abc123", null, "contactPhone")]
    [InlineData("Lan", "0909123456", "lan@", "contactEmail")]
    public void Contact_is_validated_per_field(string name, string phone, string? email, string field) =>
        IntakeSubmission.ValidateContact(name, phone, email).Keys.Should().Equal(field);

    [Fact]
    public void Free_text_needs_a_note_and_an_empty_email_becomes_null()
    {
        FluentActions.Invoking(() => IntakeSubmission.Create("Lan", "0909123456", null, null, null, [], "   "))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        IntakeSubmission.Create("Lan", "0909123456", "  ", null, null, [], "Cần menu").ContactEmail.Should().BeNull();
    }

    [Fact]
    public void A_template_submission_needs_an_answer_or_a_note()   // review P13-BE #10
    {
        FluentActions.Invoking(() => IntakeSubmission.Create("Lan", "0909123456", null, Guid.NewGuid(), "Poster", [], "  "))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        IntakeSubmission.Create("Lan", "0909123456", null, Guid.NewGuid(), "Poster", [], "Chỉ ghi chú").Body
            .Should().Be("Mẫu: Poster\n\nGhi chú thêm:\nChỉ ghi chú");
    }

    [Fact]
    public void Phone_digits_ignore_formatting() =>
        IntakeSubmission.PhoneDigits("+84 909.123-456").Should().Be("84909123456");

    [Fact]
    public void Only_a_new_submission_converts_or_is_rejected()   // BR-136
    {
        var s = NewOne();
        var (client, project) = (Guid.NewGuid(), Guid.NewGuid());

        s.Convert(client, project, Now);

        s.Status.Should().Be(IntakeStatuses.Converted);
        (s.ConvertedClientId, s.ConvertedProjectId, s.ConvertedAt).Should().Be(((Guid?)client, (Guid?)project, (DateTimeOffset?)Now));
        FluentActions.Invoking(() => s.Reject(null, Now)).Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
        FluentActions.Invoking(() => s.Convert(client, project, Now)).Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
        FluentActions.Invoking(s.EnsureDeletable).Should().Throw<DomainException>().Which.Code.Should().Be("INVALID_TRANSITION");
    }

    [Fact]
    public void Reject_keeps_an_optional_internal_reason()
    {
        var blank = NewOne();
        blank.Reject("   ", Now);
        (blank.Status, blank.RejectReason, blank.RejectedAt).Should().Be((IntakeStatuses.Rejected, (string?)null, (DateTimeOffset?)Now));
        blank.EnsureDeletable();   // rejected spam may still be deleted

        FluentActions.Invoking(() => NewOne().Reject(new string('x', 501), Now))
            .Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }
}
