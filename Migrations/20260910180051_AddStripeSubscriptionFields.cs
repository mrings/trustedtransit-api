using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStripeSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CancelAtPeriodEnd",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StripeSubscriptionId",
                table: "Facilities",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancelAtPeriodEnd",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "StripeSubscriptionId",
                table: "Facilities");
        }
    }
}
