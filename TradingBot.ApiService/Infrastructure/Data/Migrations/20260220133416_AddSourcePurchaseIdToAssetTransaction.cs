using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TradingBot.ApiService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSourcePurchaseIdToAssetTransaction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourcePurchaseId",
                table: "AssetTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssetTransactions_SourcePurchaseId",
                table: "AssetTransactions",
                column: "SourcePurchaseId",
                unique: true,
                filter: "\"SourcePurchaseId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetTransactions_SourcePurchaseId",
                table: "AssetTransactions");

            migrationBuilder.DropColumn(
                name: "SourcePurchaseId",
                table: "AssetTransactions");
        }
    }
}
