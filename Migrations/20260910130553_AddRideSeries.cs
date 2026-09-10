using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRideSeries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "RideSeriesId",
                table: "Rides",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RideSeries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    PickupAddress = table.Column<string>(type: "text", nullable: false),
                    DestinationAddress = table.Column<string>(type: "text", nullable: false),
                    AppointmentType = table.Column<string>(type: "text", nullable: false),
                    DaysOfWeek = table.Column<string>(type: "text", nullable: false),
                    PickupTime = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Active = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RideSeries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RideSeries_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_RideSeries_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RideSeries_Residents_ResidentId",
                        column: x => x.ResidentId,
                        principalTable: "Residents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Rides_RideSeriesId",
                table: "Rides",
                column: "RideSeriesId");

            migrationBuilder.CreateIndex(
                name: "IX_RideSeries_DriverId",
                table: "RideSeries",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_RideSeries_FacilityId_Active",
                table: "RideSeries",
                columns: new[] { "FacilityId", "Active" });

            migrationBuilder.CreateIndex(
                name: "IX_RideSeries_ResidentId",
                table: "RideSeries",
                column: "ResidentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Rides_RideSeries_RideSeriesId",
                table: "Rides",
                column: "RideSeriesId",
                principalTable: "RideSeries",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Rides_RideSeries_RideSeriesId",
                table: "Rides");

            migrationBuilder.DropTable(
                name: "RideSeries");

            migrationBuilder.DropIndex(
                name: "IX_Rides_RideSeriesId",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "RideSeriesId",
                table: "Rides");
        }
    }
}
