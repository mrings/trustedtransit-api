using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FamilyPhone",
                table: "Residents",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "NotificationsEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateTable(
                name: "RideNotifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RideId = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Event = table.Column<string>(type: "text", nullable: false),
                    Channel = table.Column<string>(type: "text", nullable: false),
                    Recipient = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    Error = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideNotifications_Rides_RideId",
                        column: x => x.RideId,
                        principalTable: "Rides",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RideNotifications_RideId",
                table: "RideNotifications",
                column: "RideId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RideNotifications");

            migrationBuilder.DropColumn(
                name: "FamilyPhone",
                table: "Residents");

            migrationBuilder.DropColumn(
                name: "NotificationsEnabled",
                table: "Facilities");
        }
    }
}
