using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameEmailToUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "email",
                table: "designer_accounts",
                newName: "username");

            migrationBuilder.RenameIndex(
                name: "IX_designer_accounts_email",
                table: "designer_accounts",
                newName: "IX_designer_accounts_username");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "username",
                table: "designer_accounts",
                newName: "email");

            migrationBuilder.RenameIndex(
                name: "IX_designer_accounts_username",
                table: "designer_accounts",
                newName: "IX_designer_accounts_email");
        }
    }
}
