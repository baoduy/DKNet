using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Migrations
{
    /// <inheritdoc />
    public partial class CapIdempotentKeyAt500 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete the keys the narrowed column cannot hold first, or the ALTER fails on them.
            // Deleted rows are not restored by Down.
            migrationBuilder.Sql("DELETE FROM \"IdempotencyKeys\" WHERE char_length(\"IdempotentKey\") > 500");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "character varying(500)",
                unicode: false,
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldUnicode: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "text",
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldUnicode: false,
                oldMaxLength: 500);
        }
    }
}
