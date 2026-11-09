using System.Text.Json;
using FreelanceFlow.Api.Data;
using FreelanceFlow.Api.Domain.Files;
using FreelanceFlow.Api.Domain.Intake;
using FreelanceFlow.Api.Domain.Requests;
using FreelanceFlow.Api.Domain.RequestTemplates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreelanceFlow.IntegrationTests.RequestTemplates;

public class RequestTemplateSchemaTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private AppDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<AppDbContext>();

    [Fact]
    public async Task Migration_seeds_the_six_presets()   // quyết định #1, AC-TPL1
    {
        using var scope = Factory.Services.CreateScope();
        // other tests of this class add templates with sort_order ≥ 500
        var rows = await Db(scope).RequestTemplates.AsNoTracking().Where(t => t.SortOrder < 500).OrderBy(t => t.SortOrder).ToListAsync();

        rows.Select(r => r.Name).Should().Equal(RequestTemplatePresets.All.Select(p => p.Name));
        rows.Select(r => r.SortOrder).Should().Equal(10, 20, 30, 40, 50, 60);
        rows.Should().OnlyContain(r => r.Active && r.ServiceId == null);
        rows[2].DesignType.Should().Be("PRINT");
        rows[0].Fields().Single(f => f.Key == "size").Options.Should().Contain("60 × 160 cm (standee cuốn)");
        var settings = await Db(scope).Settings.AsNoTracking().FirstAsync();
        (settings.IntakeEnabled, settings.IntakeSlug).Should().Be((false, (string?)null));   // BR-133: off until the designer turns it on
    }

    [Fact]
    public async Task Inactive_template_stays_inactive()   // no EF bool-default sentinel on request_templates.active
    {
        var t = RequestTemplate.Create("Ẩn", null, "OTHER", null, active: false, [], 999);
        using (var scope = Factory.Services.CreateScope())
        {
            Db(scope).RequestTemplates.Add(t);
            await Db(scope).SaveChangesAsync();
        }
        using var read = Factory.Services.CreateScope();
        (await Db(read).RequestTemplates.AsNoTracking().SingleAsync(x => x.Id == t.Id)).Active.Should().BeFalse();
    }

    [Fact]
    public async Task Intake_images_have_no_project_and_other_images_must_have_one()   // ck_files_image_project
    {
        using var scope = Factory.Services.CreateScope();
        var db = Db(scope);
        var sub = IntakeSubmission.Create("Lan", "0909123456", null, null, null, [], "Cần thiết kế menu");
        db.IntakeSubmissions.Add(sub);
        var image = ProjectFile.CreateImage(null, ImageOwnerTypes.IntakeSubmission, sub.Id, null, "CLIENT", false, "a.png",
            10, 10, 1, 1, 1, [1], DateTimeOffset.UtcNow);
        db.Files.Add(image);
        await db.SaveChangesAsync();

        var act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE files SET owner_type = 'TASK' WHERE id = {image.Id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_files_image_project");
    }

    [Fact]
    public async Task Request_keeps_its_snapshot_when_the_template_is_deleted()   // BR-132
    {
        await LoginAsync();
        var clientId = (await (await Client.PostAsJsonAsync("/api/admin/clients", new { name = "Schema " + Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var projectId = (await (await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name = "P", designType = "POSTER" }))
            .Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var template = RequestTemplate.Create("Tạm", null, "OTHER", null, true, [], 500);
        var answer = new TemplateAnswer("a", "A", TemplateFieldTypes.ShortText, "x");
        Guid withTemplate, legacy;
        using (var scope = Factory.Services.CreateScope())
        {
            var db = Db(scope);
            db.RequestTemplates.Add(template);
            var r = ClientRequest.CreateByClient(projectId, "DRAFT", null, "Mẫu: Tạm\n- A: x", 0, template.Id, template.Name, [answer]);
            var old = ClientRequest.CreateByClient(projectId, "DRAFT", null, "Tự viết", 0);
            db.ClientRequests.AddRange(r, old);
            await db.SaveChangesAsync();
            (withTemplate, legacy) = (r.Id, old.Id);
        }
        using (var scope = Factory.Services.CreateScope())
        {
            await Db(scope).RequestTemplates.Where(t => t.Id == template.Id).ExecuteDeleteAsync();
        }

        using var read = Factory.Services.CreateScope();
        var saved = await Db(read).ClientRequests.AsNoTracking().SingleAsync(x => x.Id == withTemplate);
        (saved.TemplateId, saved.TemplateName).Should().Be(((Guid?)null, "Tạm"));
        saved.Answers().Should().Equal(answer);
        (await Db(read).ClientRequests.AsNoTracking().SingleAsync(x => x.Id == legacy)).AnswersJson.Should().Be("[]");
    }

    [Theory]
    [InlineData("UPDATE settings SET intake_slug = 'bad'", "ck_settings_intake_slug")]
    [InlineData("UPDATE settings SET intake_enabled = true, intake_slug = NULL", "ck_settings_intake_enabled")]
    public async Task Intake_settings_are_guarded_by_the_database(string sql, string constraint)
    {
        using var scope = Factory.Services.CreateScope();
        var act = () => Db(scope).Database.ExecuteSqlRawAsync(sql);

        (await act.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be(constraint);
    }
}
