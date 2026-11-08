using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace FreelanceFlow.IntegrationTests.Portal;

/// <summary>Plan 12: the lock trigger lets the client statement columns change on a SENT quotation — and nothing else.</summary>
public class PortalClientStatementLockTests(AppFactory factory) : PortalTestBase(factory)
{
    private async Task<Guid> SentQuotationAsync()
    {
        await LoginAsync();
        await ConfigureBankAsync();
        var (clientId, _) = await CreateClientWithPortalLinkAsync();
        var projectId = await CreateProjectAsync(clientId, "Khoa " + Guid.NewGuid().ToString("N"));
        return await CreateSentQuotationAsync(projectId);
    }

    [Fact]
    public async Task Sent_quotation_accepts_client_statement_columns_but_still_locks_content()
    {
        var id = await SentQuotationAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Database.ExecuteSqlInterpolatedAsync($@"
            UPDATE quotations SET client_accepted_at = now(), client_payment_notice_purpose = 'deposit',
                   client_payment_notice_at = now(), client_payment_notice_note = 'đã chuyển' WHERE id = {id}");
        rows.Should().Be(1);

        var edit = () => db.Database.ExecuteSqlInterpolatedAsync($"UPDATE quotations SET title = 'khác' WHERE id = {id}");
        (await edit.Should().ThrowAsync<PostgresException>()).Which.MessageText.Should().Contain("quotation_locked");
    }

    [Fact]
    public async Task Payment_notice_purpose_and_time_are_constrained()
    {
        var id = await SentQuotationAsync();
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var badPurpose = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotations SET client_payment_notice_purpose = 'cash', client_payment_notice_at = now() WHERE id = {id}");
        var noTime = () => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE quotations SET client_payment_notice_purpose = 'deposit' WHERE id = {id}");

        (await badPurpose.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_quotations_payment_notice_purpose");
        (await noTime.Should().ThrowAsync<PostgresException>()).Which.ConstraintName.Should().Be("ck_quotations_payment_notice_at");
    }
}
