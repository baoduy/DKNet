using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CS8981,CA1861,IDE0300,MA0051

namespace SlimBus.Infra.Migrations
{
    /// <inheritdoc />
    public partial class initdb : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "pro");

            migrationBuilder.EnsureSchema(
                name: "seq");

            migrationBuilder.CreateSequence<int>(
                name: "Sequence_Membership",
                schema: "seq",
                maxValue: 99999L,
                cyclic: true);

            migrationBuilder.CreateSequence<int>(
                name: "Sequence_None",
                schema: "seq",
                cyclic: true);

            migrationBuilder.CreateTable(
                name: "CustomerProfiles",
                schema: "pro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    Avatar = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BirthDay = table.Column<DateTime>(type: "Date", nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    MembershipNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    OwnedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomerProfiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "pro",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CreatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    UpdatedOn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: true),
                    ProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    OwnedBy = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Employees_CustomerProfiles_ProfileId",
                        column: x => x.ProfileId,
                        principalSchema: "pro",
                        principalTable: "CustomerProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "pro",
                table: "CustomerProfiles",
                columns: new[] { "Id", "Avatar", "BirthDay", "CreatedBy", "CreatedOn", "Email", "MembershipNo", "Name", "OwnedBy", "Phone", "UpdatedBy", "UpdatedOn" },
                values: new object[] { new Guid("a6b50327-160e-423c-9c0b-c125588e6025"), null, null, "System", new DateTimeOffset(new DateTime(2025, 1, 24, 8, 26, 11, 710, DateTimeKind.Unspecified).AddTicks(7990), new TimeSpan(0, 0, 0, 0, 0)), "abc@gmail.com", "MS12345", "Steven Hoang", null, "123456789", "System", new DateTimeOffset(new DateTime(2025, 1, 24, 8, 26, 11, 711, DateTimeKind.Unspecified).AddTicks(2760), new TimeSpan(0, 0, 0, 0, 0)) });

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_CreatedBy",
                schema: "pro",
                table: "CustomerProfiles",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_CreatedOn",
                schema: "pro",
                table: "CustomerProfiles",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_Email",
                schema: "pro",
                table: "CustomerProfiles",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_MembershipNo",
                schema: "pro",
                table: "CustomerProfiles",
                column: "MembershipNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_UpdatedBy",
                schema: "pro",
                table: "CustomerProfiles",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_CustomerProfiles_UpdatedOn",
                schema: "pro",
                table: "CustomerProfiles",
                column: "UpdatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CreatedBy",
                schema: "pro",
                table: "Employees",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CreatedOn",
                schema: "pro",
                table: "Employees",
                column: "CreatedOn");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_ProfileId",
                schema: "pro",
                table: "Employees",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UpdatedBy",
                schema: "pro",
                table: "Employees",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Employees_UpdatedOn",
                schema: "pro",
                table: "Employees",
                column: "UpdatedOn");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Employees",
                schema: "pro");

            migrationBuilder.DropTable(
                name: "CustomerProfiles",
                schema: "pro");

            migrationBuilder.DropSequence(
                name: "Sequence_Membership",
                schema: "seq");

            migrationBuilder.DropSequence(
                name: "Sequence_None",
                schema: "seq");
        }
    }
}
