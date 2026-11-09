using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;

namespace FreelanceFlow.UnitTests.Domain;

public class IntakeImageFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid Submission = Guid.NewGuid();

    private static ProjectFile Image(Guid? projectId, string ownerType = ImageOwnerTypes.IntakeSubmission, Guid? requestId = null) =>
        ProjectFile.CreateImage(projectId, ownerType, Submission, requestId, "CLIENT", false, "menu-cu.jpg",
            100, 80, 10, 10, 5, [1, 2, 3], Now);

    [Fact]
    public void Intake_image_is_a_reference_without_project_keyed_by_submission()   // BR-135
    {
        var image = Image(null);

        image.ProjectId.Should().BeNull();
        image.Kind.Should().Be("REFERENCE");
        image.StorageKey.Should().Be($"intake/{Submission}/{image.Id}");
        image.VariantKey(ImageVariants.Thumb).Should().Be($"intake/{Submission}/{image.Id}/thumb.webp");
    }

    [Fact]
    public void Project_is_required_exactly_for_non_intake_images()
    {
        FluentActions.Invoking(() => Image(null, ImageOwnerTypes.Task)).Should().Throw<DomainException>();
        FluentActions.Invoking(() => Image(Guid.NewGuid())).Should().Throw<DomainException>();
        FluentActions.Invoking(() => Image(null, requestId: Guid.NewGuid())).Should().Throw<DomainException>();
    }

    [Fact]
    public void Attach_to_project_once_and_only_for_intake_images()
    {
        var image = Image(null);
        var project = Guid.NewGuid();

        image.AttachToProject(project);

        image.ProjectId.Should().Be(project);
        image.StorageKey.Should().StartWith("intake/");   // objects never move
        FluentActions.Invoking(() => image.AttachToProject(Guid.NewGuid())).Should().Throw<DomainException>();
        FluentActions.Invoking(() => Image(Guid.NewGuid(), ImageOwnerTypes.Task).AttachToProject(project)).Should().Throw<DomainException>();
    }

    [Fact]
    public void Intake_image_visibility_cannot_change() =>
        FluentActions.Invoking(() => Image(null).SetVisibleToClient(true))
            .Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
}
