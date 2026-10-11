using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKNet.AspCore.Idempotency.MsSqlStore.Migrations
{
    /// <inheritdoc />
    public partial class CapIdempotentKeyAt500 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Delete the keys the narrowed column cannot hold first, or the ALTER fails on them.
            // DATALENGTH, not LEN: LEN ignores trailing spaces, and varchar(500) is 500 bytes, which DATALENGTH counts.
            // Deleted rows are not restored by Down.
            migrationBuilder.Sql("DELETE FROM [IdempotencyKeys] WHERE DATALENGTH([IdempotentKey]) > 500");

            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "varchar(500)",
                unicode: false,
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(max)",
                oldUnicode: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "IdempotentKey",
                table: "IdempotencyKeys",
                type: "varchar(max)",
                unicode: false,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(500)",
                oldUnicode: false,
                oldMaxLength: 500);
        }
    }
}
