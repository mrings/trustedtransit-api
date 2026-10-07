using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddTransportCompanies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "TransportCompanyId",
                table: "Users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TransportCompanyId",
                table: "Drivers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TransportCompanies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Address = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    State = table.Column<string>(type: "text", nullable: false),
                    Zip = table.Column<string>(type: "text", nullable: false),
                    Phone = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ContactUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportCompanies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportCompanies_Users_ContactUserId",
                        column: x => x.ContactUserId,
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "TransportCompanyDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TransportCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    AddedByEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransportCompanyDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TransportCompanyDomains_TransportCompanies_TransportCompany~",
                        column: x => x.TransportCompanyId,
                        principalTable: "TransportCompanies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_TransportCompanyId",
                table: "Users",
                column: "TransportCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Drivers_TransportCompanyId",
                table: "Drivers",
                column: "TransportCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportCompanies_ContactUserId",
                table: "TransportCompanies",
                column: "ContactUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TransportCompanyDomains_Domain",
                table: "TransportCompanyDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TransportCompanyDomains_TransportCompanyId",
                table: "TransportCompanyDomains",
                column: "TransportCompanyId");

            migrationBuilder.AddForeignKey(
                name: "FK_Drivers_TransportCompanies_TransportCompanyId",
                table: "Drivers",
                column: "TransportCompanyId",
                principalTable: "TransportCompanies",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_TransportCompanies_TransportCompanyId",
                table: "Users",
                column: "TransportCompanyId",
                principalTable: "TransportCompanies",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Drivers_TransportCompanies_TransportCompanyId",
                table: "Drivers");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_TransportCompanies_TransportCompanyId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "TransportCompanyDomains");

            migrationBuilder.DropTable(
                name: "TransportCompanies");

            migrationBuilder.DropIndex(
                name: "IX_Users_TransportCompanyId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Drivers_TransportCompanyId",
                table: "Drivers");

            migrationBuilder.DropColumn(
                name: "TransportCompanyId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TransportCompanyId",
                table: "Drivers");
        }
    }
}
