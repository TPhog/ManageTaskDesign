using System.Text.Json;
using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreelanceFlow.IntegrationTests.Quotations;

// docs/09 §5 + BR-35: the database itself refuses to change a sent quotation, whatever the API does.
public class QuotationDbInvariantTests(AppFactory factory) : IntegrationTestBase(factory)
{
    private const string Locked = "23514";          // check_violation raised by the lock triggers
    private const string UniqueViolation = "23505";

    [Fact]
    public async Task Sent_quotation_content_cannot_be_updated()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        var act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotations SET total = 1 WHERE id = {id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
    }

    [Fact]
    public async Task Sent_quotation_cannot_return_to_draft()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        var act = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotations SET status = 'DRAFT' WHERE id = {id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
    }

    [Fact]
    public async Task Sent_quotation_status_and_payment_fields_stay_writable()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        (await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotations SET status = 'ACCEPTED', accepted_at = DATE '2026-11-09', accepted_note = 'Zalo' WHERE id = {id}"))
            .Should().Be(1);
        (await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotations SET payment_status = 'DEPOSIT_PAID', deposit_paid_at = DATE '2026-11-10' WHERE id = {id}"))
            .Should().Be(1);
        (await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotations SET internal_notes = 'ghi chú nội bộ', updated_at = now() WHERE id = {id}"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Sent_quotation_can_be_superseded()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        (await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotations SET status = 'SUPERSEDED' WHERE id = {id}"))
            .Should().Be(1);
    }

    [Fact]
    public async Task Sent_quotation_cannot_be_deleted()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        var act = () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM quotations WHERE id = {id}");

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
    }

    [Fact]
    public async Task Items_of_sent_quotation_cannot_be_inserted_updated_or_deleted()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, itemId) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, id, NewNumber());

        var insert = () => db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO quotation_items (id, quotation_id, description, quantity, unit_price, amount)
            VALUES ({Guid.NewGuid()}, {id}, 'Thêm lén', 1, 1, 1)");
        var update = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotation_items SET description = 'Sửa lén' WHERE id = {itemId}");
        var delete = () => db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM quotation_items WHERE id = {itemId}");

        (await insert.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
        (await update.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
        (await delete.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(Locked);
    }

    [Fact]
    public async Task Draft_item_delete_really_removes_the_row()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, itemId) = await InsertDraftAsync(db, projectId);

        (await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM quotation_items WHERE id = {itemId}")).Should().Be(1);
        (await db.QuotationItems.CountAsync(i => i.QuotationId == id)).Should().Be(0);
    }

    [Fact]
    public async Task Draft_quotation_with_items_can_be_deleted()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (id, _) = await InsertDraftAsync(db, projectId);

        (await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM quotations WHERE id = {id}")).Should().Be(1);
        (await db.QuotationItems.CountAsync(i => i.QuotationId == id)).Should().Be(0);
    }

    [Fact]
    public async Task Two_live_versions_of_one_number_violate_the_unique_index()
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var number = NewNumber();
        var (first, _) = await InsertDraftAsync(db, projectId);
        var (second, _) = await InsertDraftAsync(db, projectId, version: 2);
        await SendRawAsync(db, first, number);

        var act = () => SendRawAsync(db, second, number);

        (await act.Should().ThrowAsync<PostgresException>()).Which.SqlState.Should().Be(UniqueViolation);
    }

    [Fact]
    public async Task One_project_may_hold_several_live_quotations()   // D1 + ADDITIONAL
    {
        var projectId = await CreateProjectAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var (first, _) = await InsertDraftAsync(db, projectId);
        var (second, _) = await InsertDraftAsync(db, projectId);
        await SendRawAsync(db, first, NewNumber());

        var act = () => SendRawAsync(db, second, NewNumber());

        await act.Should().NotThrowAsync();
    }

    private static string NewNumber() => "TEST-" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();

    private async Task<Guid> CreateProjectAsync()
    {
        await LoginAsync();
        var c = await Client.PostAsJsonAsync("/api/admin/clients", new { name = "DB Invariant Client " + Guid.NewGuid() });
        c.EnsureSuccessStatusCode();
        var clientId = (await c.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var p = await Client.PostAsJsonAsync("/api/admin/projects", new { clientId, name = "DB Invariant Project", designType = "LOGO" });
        p.EnsureSuccessStatusCode();
        return (await p.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private static async Task<(Guid QuotationId, Guid ItemId)> InsertDraftAsync(AppDbContext db, Guid projectId, int version = 1)
    {
        var quotationId = Guid.NewGuid();
        var itemId = Guid.NewGuid();
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO quotations (id, project_id, kind, version, title, validity_days, deposit_percent, created_at, updated_at)
            VALUES ({quotationId}, {projectId}, 'INITIAL', {version}, 'DB invariant', 7, 50, now(), now())");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO quotation_items (id, quotation_id, description, quantity, unit_price, amount)
            VALUES ({itemId}, {quotationId}, 'Logo', 1, 1000000, 1000000)");
        return (quotationId, itemId);
    }

    private static Task<int> SendRawAsync(AppDbContext db, Guid quotationId, string number) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE quotations
               SET status = 'SENT', number = {number}, snapshot = '{{}}'::jsonb,
                   issue_date = DATE '2026-11-08', expiry_date = DATE '2026-11-15',
                   subtotal = 1000000, total = 1000000, deposit_amount = 500000, sent_at = now()
             WHERE id = {quotationId}");
}
