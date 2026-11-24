using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senzy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DurableFundingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_SandboxProviderTransfers_Status",
                table: "SandboxProviderTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_FundingTransfers_Status",
                table: "FundingTransfers");

            migrationBuilder.AlterColumn<Guid>(
                name: "LedgerTransactionId",
                table: "FundingTransfers",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "ProviderOperationId",
                table: "FundingTransfers",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ProviderOutcome",
                table: "FundingTransfers",
                type: "character varying(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FundingTransfers_Status",
                table: "FundingTransfers",
                sql: "\"Status\" IN ('Pending', 'Posted', 'Failed', 'Unknown')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_FundingTransfers_Status",
                table: "FundingTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderOperationId",
                table: "FundingTransfers");

            migrationBuilder.DropColumn(
                name: "ProviderOutcome",
                table: "FundingTransfers");

            migrationBuilder.AlterColumn<Guid>(
                name: "LedgerTransactionId",
                table: "FundingTransfers",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_SandboxProviderTransfers_Status",
                table: "SandboxProviderTransfers",
                sql: "\"Status\" = 'posted'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_FundingTransfers_Status",
                table: "FundingTransfers",
                sql: "\"Status\" = 'Posted'");
        }
    }
}
