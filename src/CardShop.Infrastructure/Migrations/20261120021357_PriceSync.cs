using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CardShop.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PriceSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceSnapshots_CardId_CapturedAt",
                table: "PriceSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TcgPlayerId",
                table: "Cards");

            migrationBuilder.AlterColumn<decimal>(
                name: "Low",
                table: "PriceSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataDate",
                table: "PriceSnapshots",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1));

            migrationBuilder.AddColumn<decimal>(
                name: "High",
                table: "PriceSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Mid",
                table: "PriceSnapshots",
                type: "numeric(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubType",
                table: "PriceSnapshots",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "CategoryId",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GroupId",
                table: "Cards",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceSnapshots_CardId_DataDate",
                table: "PriceSnapshots",
                columns: new[] { "CardId", "DataDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TcgPlayerId_Condition_IsFoil",
                table: "Cards",
                columns: new[] { "TcgPlayerId", "Condition", "IsFoil" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PriceSnapshots_CardId_DataDate",
                table: "PriceSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Cards_TcgPlayerId_Condition_IsFoil",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "DataDate",
                table: "PriceSnapshots");

            migrationBuilder.DropColumn(
                name: "High",
                table: "PriceSnapshots");

            migrationBuilder.DropColumn(
                name: "Mid",
                table: "PriceSnapshots");

            migrationBuilder.DropColumn(
                name: "SubType",
                table: "PriceSnapshots");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Cards");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Cards");

            migrationBuilder.AlterColumn<decimal>(
                name: "Low",
                table: "PriceSnapshots",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(10,2)",
                oldPrecision: 10,
                oldScale: 2,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PriceSnapshots_CardId_CapturedAt",
                table: "PriceSnapshots",
                columns: new[] { "CardId", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_TcgPlayerId",
                table: "Cards",
                column: "TcgPlayerId",
                unique: true);
        }
    }
}
