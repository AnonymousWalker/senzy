using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senzy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase3FundingOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_DeliveredAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions");

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "OutboxMessages",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LockId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LockedUntil",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "NextAttemptAt",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingTransferId",
                table: "LedgerTransactions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FundingTransferId",
                table: "IdempotencyRecords",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FundingTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Provider = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ProviderTransferId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    LedgerTransactionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FundingTransfers", x => x.Id);
                    table.CheckConstraint("CK_FundingTransfers_Amount", "\"AmountMinor\" > 0");
                    table.CheckConstraint("CK_FundingTransfers_Currency", "\"Currency\" = 'USD'");
                    table.CheckConstraint("CK_FundingTransfers_Direction", "\"Direction\" IN ('CashIn', 'CashOut')");
                    table.CheckConstraint("CK_FundingTransfers_Status", "\"Status\" = 'Posted'");
                    table.ForeignKey(
                        name: "FK_FundingTransfers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FundingTransfers_LedgerTransactions_LedgerTransactionId",
                        column: x => x.LedgerTransactionId,
                        principalTable: "LedgerTransactions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceOutboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Title = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Body = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_OutboxMessages_SourceOutboxMessageId",
                        column: x => x.SourceOutboxMessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    UnbalancedTransactions = table.Column<int>(type: "integer", nullable: false),
                    BalanceMismatches = table.Column<int>(type: "integer", nullable: false),
                    PaymentMismatches = table.Column<int>(type: "integer", nullable: false),
                    FundingMismatches = table.Column<int>(type: "integer", nullable: false),
                    UnexplainedVarianceMinor = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SandboxProviderTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FundingTransferId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderTransferId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AmountMinor = table.Column<long>(type: "bigint", nullable: false),
                    Currency = table.Column<string>(type: "character(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SandboxProviderTransfers", x => x.Id);
                    table.CheckConstraint("CK_SandboxProviderTransfers_Amount", "\"AmountMinor\" > 0");
                    table.CheckConstraint("CK_SandboxProviderTransfers_Currency", "\"Currency\" = 'USD'");
                    table.CheckConstraint("CK_SandboxProviderTransfers_Status", "\"Status\" = 'posted'");
                    table.ForeignKey(
                        name: "FK_SandboxProviderTransfers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletControls",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsFrozen = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletControls", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_WalletControls_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DeliveredAt_NextAttemptAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "DeliveredAt", "NextAttemptAt", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_FundingTransferId",
                table: "LedgerTransactions",
                column: "FundingTransferId",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions",
                sql: "\"Kind\" IN ('SandboxOpeningBalance', 'Payment', 'Reversal', 'FundingIn', 'FundingOut')");

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransfers_LedgerTransactionId",
                table: "FundingTransfers",
                column: "LedgerTransactionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransfers_ProviderTransferId",
                table: "FundingTransfers",
                column: "ProviderTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FundingTransfers_UserId_CreatedAt",
                table: "FundingTransfers",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceOutboxMessageId",
                table: "Notifications",
                column: "SourceOutboxMessageId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_SourceOutboxMessageId",
                table: "Notifications",
                columns: new[] { "UserId", "SourceOutboxMessageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRuns_CompletedAt",
                table: "ReconciliationRuns",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SandboxProviderTransfers_FundingTransferId",
                table: "SandboxProviderTransfers",
                column: "FundingTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxProviderTransfers_ProviderTransferId",
                table: "SandboxProviderTransfers",
                column: "ProviderTransferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SandboxProviderTransfers_UserId",
                table: "SandboxProviderTransfers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FundingTransfers");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ReconciliationRuns");

            migrationBuilder.DropTable(
                name: "SandboxProviderTransfers");

            migrationBuilder.DropTable(
                name: "WalletControls");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_DeliveredAt_NextAttemptAt_CreatedAt",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_FundingTransferId",
                table: "LedgerTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LockedUntil",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "NextAttemptAt",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "FundingTransferId",
                table: "LedgerTransactions");

            migrationBuilder.DropColumn(
                name: "FundingTransferId",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_DeliveredAt_CreatedAt",
                table: "OutboxMessages",
                columns: new[] { "DeliveredAt", "CreatedAt" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions",
                sql: "\"Kind\" IN ('SandboxOpeningBalance', 'Payment', 'Reversal')");
        }
    }
}
