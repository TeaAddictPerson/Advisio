using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddTypeToPlace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Places",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                table: "Places");
        }
    }
}
