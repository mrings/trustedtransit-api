using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrustedTransit.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRideTrackingToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TrackingToken",
                table: "Rides",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rides_TrackingToken",
                table: "Rides",
                column: "TrackingToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Rides_TrackingToken",
                table: "Rides");

            migrationBuilder.DropColumn(
                name: "TrackingToken",
                table: "Rides");
        }
    }
}
