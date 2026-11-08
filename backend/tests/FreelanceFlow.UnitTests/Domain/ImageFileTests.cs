using FluentAssertions;
using FreelanceFlow.Api.Domain.Common;
using FreelanceFlow.Api.Domain.Files;

namespace FreelanceFlow.UnitTests.Domain;

public class ImageFileTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 3, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProjectId = Guid.Parse("0193f000-0000-7000-8000-000000000001");
    private static readonly Guid OwnerId = Guid.Parse("0193f000-0000-7000-8000-000000000002");

    private static ProjectFile Create(string ownerType = ImageOwnerTypes.Task, string uploadedBy = "DESIGNER",
        string? name = "phac-thao.png", Guid? requestId = null) =>
        ProjectFile.CreateImage(ProjectId, ownerType, OwnerId, requestId, uploadedBy, visibleToClient: true, name,
            width: 3000, height: 2000, originalBytes: 500, previewBytes: 400, thumbBytes: 100, sha256: new byte[32], Now);

    [Fact]
    public void Image_sums_variant_sizes_and_keys_objects_under_the_project()
    {
        var f = Create();

        f.SizeBytes.Should().Be(1000);
        f.OwnerType.Should().Be("TASK");
        f.OwnerId.Should().Be(OwnerId);
        f.Width.Should().Be(3000);
        f.Height.Should().Be(2000);
        f.ContentType.Should().Be("image/webp");
        f.StorageKey.Should().Be($"projects/{ProjectId}/{f.Id}");
        f.VariantKey(ImageVariants.Thumb).Should().Be($"projects/{ProjectId}/{f.Id}/thumb.webp");
        f.VariantKeys().Should().Equal(
            $"projects/{ProjectId}/{f.Id}/original.webp",
            $"projects/{ProjectId}/{f.Id}/preview.webp",
            $"projects/{ProjectId}/{f.Id}/thumb.webp");
        f.CreatedAt.Should().Be(Now);
    }

    [Theory]
    [InlineData(ImageOwnerTypes.Task, "PREVIEW")]
    [InlineData(ImageOwnerTypes.ProgressUpdate, "PREVIEW")]
    [InlineData(ImageOwnerTypes.ClientRequest, "REFERENCE")]
    [InlineData(ImageOwnerTypes.Deliverable, "FINAL")]
    public void Kind_follows_the_owner(string ownerType, string kind) =>
        Create(ownerType).Kind.Should().Be(kind);

    [Theory]
    [InlineData(@"C:\fakepath\logo final.png", "logo final.png")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData("  \u0007anh\u0000.jpg ", "anh.jpg")]
    [InlineData("", "anh")]
    [InlineData(null, "anh")]
    public void Original_name_keeps_only_a_clean_file_name(string? raw, string expected) =>
        Create(name: raw).OriginalName.Should().Be(expected);

    [Fact]
    public void Original_name_is_cut_to_255_chars() =>
        Create(name: new string('a', 300) + ".png").OriginalName.Should().HaveLength(255);

    [Fact]
    public void Unknown_owner_uploader_empty_variant_or_bad_variant_name_is_refused()
    {
        var bad = () => Create(ownerType: "QUOTATION");
        bad.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        var who = () => Create(uploadedBy: "SYSTEM");
        who.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        var empty = () => ProjectFile.CreateImage(ProjectId, ImageOwnerTypes.Task, OwnerId, null, "DESIGNER", true, "a.png",
            1, 1, 0, 1, 1, new byte[32], Now);
        empty.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
        var variant = () => Create().VariantKey("raw");
        variant.Should().Throw<DomainException>().Which.Code.Should().Be("VALIDATION_FAILED");
    }

    [Fact]
    public void Soft_delete_keeps_the_first_timestamp()
    {
        var f = Create();
        f.SoftDelete(Now);
        f.SoftDelete(Now.AddDays(1));
        f.DeletedAt.Should().Be(Now);
    }

    [Fact]
    public void Visibility_changes_only_for_designer_task_images()
    {
        var task = Create();
        task.SetVisibleToClient(false);
        task.VisibleToClient.Should().BeFalse();

        var progress = () => Create(ImageOwnerTypes.ProgressUpdate).SetVisibleToClient(false);
        progress.Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
        var fromClient = () => Create(ImageOwnerTypes.ClientRequest, "CLIENT", requestId: OwnerId).SetVisibleToClient(true);
        fromClient.Should().Throw<DomainException>().Which.Code.Should().Be("BUSINESS_RULE");
    }
}
