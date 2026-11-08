using System.Collections;
using FluentAssertions;
using FreelanceFlow.Api.Domain.Quotations;

namespace FreelanceFlow.UnitTests.Infrastructure;

/// <summary>
/// Plan 12: AddPortalClientStatements rewrites fn_quotation_locked; Down must put the previous body back exactly.
/// Migration operations are read by reflection: the test projects compile against an older EF Relational than the Api
/// (CS1705), so they must not bind to Migration / SqlOperation types directly.
/// </summary>
public class QuotationLockMigrationTests
{
    private static string LockFunction(string migration, string operations)
    {
        var type = typeof(Quotation).Assembly.GetType($"FreelanceFlow.Api.Data.Migrations.{migration}", throwOnError: true)!;
        var ops = (IEnumerable)type.GetProperty(operations)!.GetValue(Activator.CreateInstance(type))!;
        return ops.Cast<object>()
            .Select(o => o.GetType().GetProperty("Sql")?.GetValue(o) as string)
            .Single(sql => sql != null && sql.Contains("CREATE OR REPLACE FUNCTION fn_quotation_locked()"))!;
    }

    private static string Previous => LockFunction("FixQuotationLocksAndLiveIndex", "UpOperations");

    [Fact]
    public void Down_restores_the_body_of_20261108100000_byte_for_byte() =>
        LockFunction("AddPortalClientStatements", "DownOperations").Should().Be(Previous);

    [Fact]
    public void Up_only_adds_the_client_statement_columns_to_the_whitelist()
    {
        const string added =
            ",\n                                  'client_accepted_at','client_payment_notice_purpose','client_payment_notice_at','client_payment_notice_note'";
        var up = LockFunction("AddPortalClientStatements", "UpOperations").ReplaceLineEndings("\n");

        up.Should().Contain(added);
        up.Replace(added, "").Should().Be(Previous.ReplaceLineEndings("\n"));
    }
}
