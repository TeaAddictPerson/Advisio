using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisio.Web.Migrations
{
    /// <summary>Идентификатор события PredictHQ — уникальный, чтобы не создавать дубликаты при параллельных импортах.</summary>
    [Migration("20260405195808_AddPredictHqEventIdToPlace")]
    public partial class AddPredictHqEventIdToPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PredictHqEventId",
                table: "Places",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_PredictHqEventId",
                table: "Places",
                column: "PredictHqEventId",
                unique: true,
                filter: "[PredictHqEventId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Places_PredictHqEventId",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "PredictHqEventId",
                table: "Places");
        }
    }
}
