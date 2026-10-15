using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Senzy.Api.Features.Identity;
using Senzy.Api.Infrastructure.Persistence.Models;
using Senzy.Api.Features.Profiles;

namespace Senzy.Api.Infrastructure.Persistence;

public sealed class SenzyDbContext(DbContextOptions<SenzyDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<Profile> Profiles => Set<Profile>();
    public DbSet<LedgerAccount> LedgerAccounts => Set<LedgerAccount>();
    public DbSet<LedgerTransaction> LedgerTransactions => Set<LedgerTransaction>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentRequest> PaymentRequests => Set<PaymentRequest>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().HasIndex(user => user.NormalizedEmail).IsUnique();
        builder.Entity<Profile>(entity =>
        {
            entity.HasKey(profile => profile.UserId);
            entity.Property(profile => profile.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(profile => profile.Handle).HasMaxLength(20).IsRequired();
            entity.HasIndex(profile => profile.Handle).IsUnique();
            entity.HasOne<ApplicationUser>().WithOne().HasForeignKey<Profile>(profile => profile.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<LedgerAccount>(entity =>
        {
            entity.Property(account => account.Purpose).HasConversion<string>().HasMaxLength(32);
            entity.Property(account => account.Currency).HasMaxLength(3).IsFixedLength();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LedgerAccounts_Currency", "\"Currency\" = 'USD'");
                table.HasCheckConstraint("CK_LedgerAccounts_Purpose", "\"Purpose\" IN ('Wallet', 'SandboxIssuance')");
                table.HasCheckConstraint("CK_LedgerAccounts_Balance", "\"AllowNegative\" OR \"BalanceMinor\" >= 0");
            });
            entity.HasIndex(account => new { account.OwnerUserId, account.Purpose, account.Currency }).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(account => account.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<LedgerTransaction>(entity =>
        {
            entity.Property(transaction => transaction.Kind).HasConversion<string>().HasMaxLength(32);
            entity.Property(transaction => transaction.Currency).HasMaxLength(3).IsFixedLength();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LedgerTransactions_Currency", "\"Currency\" = 'USD'");
                table.HasCheckConstraint("CK_LedgerTransactions_Kind", "\"Kind\" IN ('SandboxOpeningBalance', 'Payment', 'Reversal')");
            });
            entity.HasIndex(transaction => new { transaction.PaymentId, transaction.Kind }).IsUnique();
            entity.HasMany(transaction => transaction.Entries).WithOne().HasForeignKey(entry => entry.LedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerTransaction>().WithMany().HasForeignKey(transaction => transaction.ReversesTransactionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<LedgerEntry>(entity =>
        {
            entity.Property(entry => entry.Direction).HasConversion<string>().HasMaxLength(8);
            entity.Property(entry => entry.Currency).HasMaxLength(3).IsFixedLength();
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_LedgerEntries_Amount", "\"AmountMinor\" > 0");
                table.HasCheckConstraint("CK_LedgerEntries_Currency", "\"Currency\" = 'USD'");
                table.HasCheckConstraint("CK_LedgerEntries_Direction", "\"Direction\" IN ('Debit', 'Credit')");
            });
            entity.HasIndex(entry => entry.LedgerAccountId);
            entity.HasOne<LedgerAccount>().WithMany().HasForeignKey(entry => entry.LedgerAccountId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<Payment>(entity =>
        {
            entity.Property(payment => payment.Kind).HasConversion<string>().HasMaxLength(16);
            entity.Property(payment => payment.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(payment => payment.Currency).HasMaxLength(3).IsFixedLength();
            entity.Property(payment => payment.Note).HasMaxLength(280);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_Payments_Amount", "\"AmountMinor\" > 0");
                table.HasCheckConstraint("CK_Payments_Currency", "\"Currency\" = 'USD'");
                table.HasCheckConstraint("CK_Payments_Kind", "\"Kind\" IN ('Send', 'Request')");
                table.HasCheckConstraint("CK_Payments_Status", "\"Status\" IN ('Pending', 'Posted', 'Failed', 'Reversed')");
                table.HasCheckConstraint("CK_Payments_DistinctUsers", "\"SenderUserId\" <> \"RecipientUserId\"");
            });
            entity.HasIndex(payment => payment.SenderUserId);
            entity.HasIndex(payment => payment.RecipientUserId);
            entity.HasIndex(payment => payment.PaymentRequestId).IsUnique();
            entity.HasIndex(payment => payment.PostedLedgerTransactionId).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.SenderUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(payment => payment.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<PaymentRequest>().WithMany().HasForeignKey(payment => payment.PaymentRequestId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerTransaction>().WithMany().HasForeignKey(payment => payment.PostedLedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<LedgerTransaction>().WithMany().HasForeignKey(payment => payment.ReversalLedgerTransactionId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<PaymentRequest>(entity =>
        {
            entity.Property(request => request.Status).HasConversion<string>().HasMaxLength(16);
            entity.Property(request => request.Currency).HasMaxLength(3).IsFixedLength();
            entity.Property(request => request.Note).HasMaxLength(280);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_PaymentRequests_Amount", "\"AmountMinor\" > 0");
                table.HasCheckConstraint("CK_PaymentRequests_Currency", "\"Currency\" = 'USD'");
                table.HasCheckConstraint("CK_PaymentRequests_Status", "\"Status\" IN ('Pending', 'Paid', 'Declined', 'Cancelled')");
                table.HasCheckConstraint("CK_PaymentRequests_DistinctUsers", "\"RequesterUserId\" <> \"PayerUserId\"");
            });
            entity.HasIndex(request => request.RequesterUserId);
            entity.HasIndex(request => request.PayerUserId);
            entity.HasIndex(request => request.PaymentId).IsUnique();
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(request => request.RequesterUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(request => request.PayerUserId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Payment>().WithMany().HasForeignKey(request => request.PaymentId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<IdempotencyRecord>(entity =>
        {
            entity.Property(record => record.Operation).HasMaxLength(80);
            entity.Property(record => record.Key).HasMaxLength(128);
            entity.Property(record => record.RequestHash).HasMaxLength(64).IsFixedLength();
            entity.Property(record => record.ResponseJson).HasColumnType("jsonb");
            entity.HasIndex(record => new { record.ActorUserId, record.Operation, record.Key }).IsUnique();
        });
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.Property(message => message.Type).HasMaxLength(80);
            entity.Property(message => message.PayloadJson).HasColumnType("jsonb");
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_OutboxMessages_Version", "\"Version\" > 0");
                table.HasCheckConstraint("CK_OutboxMessages_Attempts", "\"AttemptCount\" >= 0");
            });
            entity.HasIndex(message => new { message.DeliveredAt, message.CreatedAt });
        });
    }
}
