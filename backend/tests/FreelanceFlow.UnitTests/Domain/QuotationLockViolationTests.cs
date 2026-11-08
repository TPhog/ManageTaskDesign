using FluentAssertions;
using FreelanceFlow.Api.Features.Quotations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreelanceFlow.UnitTests.Domain;

public class QuotationLockViolationTests
{
    private static DbUpdateException Wrap(string message, string? constraint) =>
        new("save failed", new PostgresException(message, "ERROR", "ERROR", PostgresErrorCodes.CheckViolation,
            constraintName: constraint));

    [Theory]
    [InlineData("quotation_locked: content of quotation x (status SENT) cannot be modified")]
    [InlineData("quotation_items_locked: cannot modify items of quotation x (status=SENT)")]
    public void Lock_trigger_errors_are_lock_violations(string message) =>
        QuotationReads.IsLockViolation(Wrap(message, null)).Should().BeTrue();

    [Fact]
    public void Ordinary_check_constraint_is_not_a_lock_violation() =>
        QuotationReads.IsLockViolation(
                Wrap("new row for relation \"quotations\" violates check constraint", "ck_quotations_expiry"))
            .Should().BeFalse();
}
