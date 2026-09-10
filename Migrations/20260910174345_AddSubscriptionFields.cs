using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriptionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SubscriptionRenewsAt",
                table: "Facilities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TrialEndsAt",
                table: "Facilities",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriptionRenewsAt",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "TrialEndsAt",
                table: "Facilities");
        }
    }
}
