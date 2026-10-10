using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Migrations
{
    /// <inheritdoc />
    public partial class UnboundIdempotentKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "text",
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(150)",
                oldUnicode: false,
                oldMaxLength: 150);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "character varying(150)",
                unicode: false,
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldUnicode: false);
        }
    }
}
