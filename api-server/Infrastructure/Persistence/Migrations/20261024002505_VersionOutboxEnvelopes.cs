using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Senzy.Api.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VersionOutboxEnvelopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CausationId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CorrelationId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CausationId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "OutboxMessages");
        }
    }
}
