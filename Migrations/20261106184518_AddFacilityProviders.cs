using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityProviders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Lat",
                table: "TransportCompanies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Lng",
                table: "TransportCompanies",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Lat",
                table: "Facilities",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Lng",
                table: "Facilities",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FacilityProviders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    DriverId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransportCompanyId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityProviders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityProviders_Drivers_DriverId",
                        column: x => x.DriverId,
                        principalTable: "Drivers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityProviders_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FacilityProviders_TransportCompanies_TransportCompanyId",
                        column: x => x.TransportCompanyId,
                        principalTable: "TransportCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FacilityProviders_DriverId",
                table: "FacilityProviders",
                column: "DriverId");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityProviders_FacilityId_DriverId",
                table: "FacilityProviders",
                columns: new[] { "FacilityId", "DriverId" },
                unique: true,
                filter: "\"DriverId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityProviders_FacilityId_TransportCompanyId",
                table: "FacilityProviders",
                columns: new[] { "FacilityId", "TransportCompanyId" },
                unique: true,
                filter: "\"TransportCompanyId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityProviders_TransportCompanyId",
                table: "FacilityProviders",
                column: "TransportCompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityProviders");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "TransportCompanies");

            migrationBuilder.DropColumn(
                name: "Lng",
                table: "TransportCompanies");

            migrationBuilder.DropColumn(
                name: "Lat",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "Lng",
                table: "Facilities");
        }
    }
}
