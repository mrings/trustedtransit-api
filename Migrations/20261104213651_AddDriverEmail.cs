using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDriverEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "Drivers",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "Drivers");
        }
    }
}
