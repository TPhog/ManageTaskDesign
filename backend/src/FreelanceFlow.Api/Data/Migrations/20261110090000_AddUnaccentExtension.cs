using FreelanceFlow.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FreelanceFlow.Api.Data.Migrations
{
    /// <summary>
    /// Quick search (docs/08 §2.1) folds Vietnamese diacritics with unaccent(): "nguyen" finds "Nguyễn", "dang" finds "Đặng".
    /// SQL only — the EF model is unchanged, so there is no Designer file and the model snapshot stays as is.
    /// unaccent is a trusted contrib extension shipped with the official postgres:17 images.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261110090000_AddUnaccentExtension")]
    public partial class AddUnaccentExtension : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS unaccent;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP EXTENSION IF EXISTS unaccent;");
        }
    }
}
