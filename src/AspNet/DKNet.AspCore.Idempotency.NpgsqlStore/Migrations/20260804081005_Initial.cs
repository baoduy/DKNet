using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DKNet.AspCore.Idempotency.NpgsqlStore.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IdempotencyKeys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Body = table.Column<string>(type: "text", maxLength: 1048576, nullable: true),
                    CompositeKey = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", unicode: false, maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IdempotentKey = table.Column<string>(type: "character varying(150)", unicode: false, maxLength: 150, nullable: false),
                    Method = table.Column<string>(type: "character varying(20)", unicode: false, maxLength: 20, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdempotencyKeys", x => x.Id);
                    table.CheckConstraint("CK_StatusCode_Valid", "\"StatusCode\" BETWEEN 100 AND 599");
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyKeys_ExpiresAt",
                table: "IdempotencyKeys",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "UX_CompositeKey",
                table: "IdempotencyKeys",
                column: "CompositeKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdempotencyKeys");
        }
    }
}
