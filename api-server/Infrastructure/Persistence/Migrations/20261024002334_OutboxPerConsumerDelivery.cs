using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senzy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OutboxPerConsumerDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OutboxConsumers",
                columns: table => new
                {
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxConsumers", x => x.Name);
                });

            migrationBuilder.CreateTable(
                name: "OutboxDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OutboxMessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    DeliveredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockId = table.Column<Guid>(type: "uuid", nullable: true),
                    LockedUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LastError = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxDeliveries", x => x.Id);
                    table.CheckConstraint("CK_OutboxDeliveries_Attempts", "\"AttemptCount\" >= 0");
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_OutboxConsumers_ConsumerName",
                        column: x => x.ConsumerName,
                        principalTable: "OutboxConsumers",
                        principalColumn: "Name",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_OutboxDeliveries_OutboxMessages_OutboxMessageId",
                        column: x => x.OutboxMessageId,
                        principalTable: "OutboxMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_ConsumerName_DeliveredAt_NextAttemptAt",
                table: "OutboxDeliveries",
                columns: new[] { "ConsumerName", "DeliveredAt", "NextAttemptAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxDeliveries_OutboxMessageId_ConsumerName",
                table: "OutboxDeliveries",
                columns: new[] { "OutboxMessageId", "ConsumerName" },
                unique: true);

            migrationBuilder.Sql("""
                INSERT INTO "OutboxConsumers" ("Name", "CreatedAt")
                VALUES ('notifications', CURRENT_TIMESTAMP)
                ON CONFLICT ("Name") DO NOTHING;

                INSERT INTO "OutboxDeliveries" (
                    "Id", "OutboxMessageId", "ConsumerName", "DeliveredAt", "AttemptCount",
                    "NextAttemptAt", "LockId", "LockedUntil", "LastError")
                SELECT gen_random_uuid(), message."Id", 'notifications', message."DeliveredAt", message."AttemptCount",
                       message."NextAttemptAt", message."LockId", message."LockedUntil", message."LastError"
                FROM "OutboxMessages" message
                ON CONFLICT ("OutboxMessageId", "ConsumerName") DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxDeliveries");

            migrationBuilder.DropTable(
                name: "OutboxConsumers");
        }
    }
}
