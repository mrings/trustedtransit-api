using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddFacilityDomains : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FacilityDomains",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FacilityId = table.Column<Guid>(type: "uuid", nullable: false),
                    Domain = table.Column<string>(type: "text", nullable: false),
                    AddedByEmail = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FacilityDomains", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FacilityDomains_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Carry forward any facility that already had a single EmailDomain set, before
            // dropping that column. md5(random())::uuid avoids depending on pgcrypto.
            migrationBuilder.Sql(@"
                INSERT INTO ""FacilityDomains"" (""Id"", ""FacilityId"", ""Domain"", ""AddedByEmail"", ""CreatedAt"")
                SELECT md5(random()::text || clock_timestamp()::text)::uuid, ""Id"", lower(""EmailDomain""), '', ""CreatedAt""
                FROM ""Facilities""
                WHERE ""EmailDomain"" IS NOT NULL AND ""EmailDomain"" <> '';
            ");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_EmailDomain",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "EmailDomain",
                table: "Facilities");

            migrationBuilder.CreateIndex(
                name: "IX_FacilityDomains_Domain",
                table: "FacilityDomains",
                column: "Domain",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FacilityDomains_FacilityId",
                table: "FacilityDomains",
                column: "FacilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FacilityDomains");

            migrationBuilder.AddColumn<string>(
                name: "EmailDomain",
                table: "Facilities",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_EmailDomain",
                table: "Facilities",
                column: "EmailDomain",
                unique: true);
        }
    }
}
