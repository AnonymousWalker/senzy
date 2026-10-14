using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senzy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase2IntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReversalLedgerTransactionId",
                table: "Payments",
                column: "ReversalLedgerTransactionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Currency",
                table: "Payments",
                sql: "\"Currency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_DistinctUsers",
                table: "Payments",
                sql: "\"SenderUserId\" <> \"RecipientUserId\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Kind",
                table: "Payments",
                sql: "\"Kind\" IN ('Send', 'Request')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Payments_Status",
                table: "Payments",
                sql: "\"Status\" IN ('Pending', 'Posted', 'Failed', 'Reversed')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_Currency",
                table: "PaymentRequests",
                sql: "\"Currency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_DistinctUsers",
                table: "PaymentRequests",
                sql: "\"RequesterUserId\" <> \"PayerUserId\"");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests",
                sql: "\"Status\" IN ('Pending', 'Paid', 'Declined', 'Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_Attempts",
                table: "OutboxMessages",
                sql: "\"AttemptCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OutboxMessages_Version",
                table: "OutboxMessages",
                sql: "\"Version\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_LedgerTransactions_ReversesTransactionId",
                table: "LedgerTransactions",
                column: "ReversesTransactionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerTransactions_Currency",
                table: "LedgerTransactions",
                sql: "\"Currency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions",
                sql: "\"Kind\" IN ('SandboxOpeningBalance', 'Payment', 'Reversal')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Currency",
                table: "LedgerEntries",
                sql: "\"Currency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerEntries_Direction",
                table: "LedgerEntries",
                sql: "\"Direction\" IN ('Debit', 'Credit')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerAccounts_Balance",
                table: "LedgerAccounts",
                sql: "\"AllowNegative\" OR \"BalanceMinor\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerAccounts_Currency",
                table: "LedgerAccounts",
                sql: "\"Currency\" = 'USD'");

            migrationBuilder.AddCheckConstraint(
                name: "CK_LedgerAccounts_Purpose",
                table: "LedgerAccounts",
                sql: "\"Purpose\" IN ('Wallet', 'SandboxIssuance')");

            migrationBuilder.AddForeignKey(
                name: "FK_LedgerTransactions_LedgerTransactions_ReversesTransactionId",
                table: "LedgerTransactions",
                column: "ReversesTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentRequests_Payments_PaymentId",
                table: "PaymentRequests",
                column: "PaymentId",
                principalTable: "Payments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_LedgerTransactions_PostedLedgerTransactionId",
                table: "Payments",
                column: "PostedLedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_LedgerTransactions_ReversalLedgerTransactionId",
                table: "Payments",
                column: "ReversalLedgerTransactionId",
                principalTable: "LedgerTransactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_PaymentRequests_PaymentRequestId",
                table: "Payments",
                column: "PaymentRequestId",
                principalTable: "PaymentRequests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LedgerTransactions_LedgerTransactions_ReversesTransactionId",
                table: "LedgerTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentRequests_Payments_PaymentId",
                table: "PaymentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_LedgerTransactions_PostedLedgerTransactionId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_LedgerTransactions_ReversalLedgerTransactionId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_PaymentRequests_PaymentRequestId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Payments_ReversalLedgerTransactionId",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Currency",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_DistinctUsers",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Kind",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Payments_Status",
                table: "Payments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_Currency",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_DistinctUsers",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PaymentRequests_Status",
                table: "PaymentRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_Attempts",
                table: "OutboxMessages");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OutboxMessages_Version",
                table: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_LedgerTransactions_ReversesTransactionId",
                table: "LedgerTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerTransactions_Currency",
                table: "LedgerTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerTransactions_Kind",
                table: "LedgerTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Currency",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerEntries_Direction",
                table: "LedgerEntries");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerAccounts_Balance",
                table: "LedgerAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerAccounts_Currency",
                table: "LedgerAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_LedgerAccounts_Purpose",
                table: "LedgerAccounts");
        }
    }
}
