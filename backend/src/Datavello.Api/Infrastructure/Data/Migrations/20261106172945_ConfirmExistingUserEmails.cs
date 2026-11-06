using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Datavello.Api.Infrastructure.Data.Migrations
{
    /// <summary>
    /// Sign-in now requires a confirmed e-mail. Accounts created before that rule existed are
    /// treated as confirmed, so nobody is locked out by the upgrade. No schema change.
    /// </summary>
    public partial class ConfirmExistingUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "AspNetUsers" SET "EmailConfirmed" = TRUE WHERE "EmailConfirmed" = FALSE;""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Not reversible: which accounts were unconfirmed before is not recorded (and does not matter).
        }
    }
}
