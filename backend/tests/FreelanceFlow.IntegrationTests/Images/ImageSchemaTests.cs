using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreelanceFlow.IntegrationTests.Images;

public class ImageSchemaTests(AppFactory factory) : ImageTestBase(factory)
{
    [Fact]
    public async Task Database_refuses_an_aliased_preview_on_a_designer_image_and_indexes_project_hashes()
    {
        await LoginAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Dự án schema " + Guid.NewGuid());
        var taskId = await CreateTaskAsync(projectId);
        var fillerId = await InsertFillerImageAsync(projectId, taskId, 1000);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Files.AsNoTracking().SingleAsync(f => f.Id == fillerId);
        row.PreviewIsOriginal.Should().BeFalse();
        row.OriginalDeletedAt.Should().BeNull();

        var alias = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE files SET preview_is_original = true WHERE id = {fillerId}");
        (await alias.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be("23514");

        var index = await db.Database
            .SqlQueryRaw<string>("SELECT indexdef AS \"Value\" FROM pg_indexes WHERE indexname = 'ix_files_project_sha256'")
            .ToListAsync();
        index.Should().ContainSingle().Which.Should().Contain("(project_id, sha256)").And.Contain("WHERE (deleted_at IS NULL)");
    }
}
