using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Advisio.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddPasswordHashToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
            
            // Обновляем существующие записи, устанавливая пустой пароль
            migrationBuilder.Sql("UPDATE Users SET PasswordHash = '' WHERE PasswordHash IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "Users");
        }
    }
}

