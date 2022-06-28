using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BlazorWebAssemblyApp.Server.Migrations
{
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Prices",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Open = table.Column<double>(type: "REAL", nullable: true),
                    High = table.Column<double>(type: "REAL", nullable: true),
                    Low = table.Column<double>(type: "REAL", nullable: true),
                    Close = table.Column<double>(type: "REAL", nullable: true),
                    AdjustedClose = table.Column<double>(type: "REAL", nullable: false),
                    Volume = table.Column<double>(type: "REAL", nullable: true),
                    CurrentHoldings = table.Column<double>(type: "REAL", nullable: true),
                    Symbol = table.Column<string>(type: "TEXT", nullable: true),
                    Pct_Change = table.Column<double>(type: "REAL", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Prices_Date",
                table: "Prices",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_Prices_Symbol",
                table: "Prices",
                column: "Symbol");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Prices");
        }
    }
}
