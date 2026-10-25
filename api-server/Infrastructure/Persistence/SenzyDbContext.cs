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
  public DbSet<FundingTransfer> FundingTransfers => Set<FundingTransfer>();
  public DbSet<SandboxProviderTransfer> SandboxProviderTransfers => Set<SandboxProviderTransfer>();
  public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
  public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
  public DbSet<OutboxConsumer> OutboxConsumers => Set<OutboxConsumer>();
  public DbSet<OutboxDelivery> OutboxDeliveries => Set<OutboxDelivery>();
  public DbSet<Notification> Notifications => Set<Notification>();
  public DbSet<WalletControl> WalletControls => Set<WalletControl>();
  public DbSet<ReconciliationRun> ReconciliationRuns => Set<ReconciliationRun>();

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
      entity.HasOne<ApplicationUser>()
          .WithOne()
          .HasForeignKey<Profile>(profile => profile.UserId)
          .OnDelete(DeleteBehavior.Cascade);
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
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(account => account.OwnerUserId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    builder.Entity<LedgerTransaction>(entity =>
    {
      entity.Property(transaction => transaction.Kind).HasConversion<string>().HasMaxLength(32);
      entity.Property(transaction => transaction.Currency).HasMaxLength(3).IsFixedLength();
      entity.ToTable(table =>
      {
        table.HasCheckConstraint("CK_LedgerTransactions_Currency", "\"Currency\" = 'USD'");
        table.HasCheckConstraint(
            "CK_LedgerTransactions_Kind",
            "\"Kind\" IN ('SandboxOpeningBalance', 'Payment', 'Reversal', 'FundingIn', 'FundingOut')");
      });
      entity.HasIndex(transaction => new { transaction.PaymentId, transaction.Kind }).IsUnique();
      entity.HasIndex(transaction => transaction.FundingTransferId).IsUnique();
      entity.HasMany(transaction => transaction.Entries)
          .WithOne()
          .HasForeignKey(entry => entry.LedgerTransactionId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<LedgerTransaction>()
          .WithMany()
          .HasForeignKey(transaction => transaction.ReversesTransactionId)
          .OnDelete(DeleteBehavior.Restrict);
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
      entity.HasOne<LedgerAccount>()
          .WithMany()
          .HasForeignKey(entry => entry.LedgerAccountId)
          .OnDelete(DeleteBehavior.Restrict);
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
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(payment => payment.SenderUserId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(payment => payment.RecipientUserId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<PaymentRequest>()
          .WithMany()
          .HasForeignKey(payment => payment.PaymentRequestId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<LedgerTransaction>()
          .WithMany()
          .HasForeignKey(payment => payment.PostedLedgerTransactionId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<LedgerTransaction>()
          .WithMany()
          .HasForeignKey(payment => payment.ReversalLedgerTransactionId)
          .OnDelete(DeleteBehavior.Restrict);
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
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(request => request.RequesterUserId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(request => request.PayerUserId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<Payment>()
          .WithMany()
          .HasForeignKey(request => request.PaymentId)
          .OnDelete(DeleteBehavior.Restrict);
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
      entity.Property(message => message.LastError).HasMaxLength(500);
      entity.ToTable(table =>
      {
        table.HasCheckConstraint("CK_OutboxMessages_Version", "\"Version\" > 0");
        table.HasCheckConstraint("CK_OutboxMessages_Attempts", "\"AttemptCount\" >= 0");
      });
      entity.HasIndex(message => new { message.DeliveredAt, message.NextAttemptAt, message.CreatedAt });
    });

    builder.Entity<OutboxConsumer>(entity =>
    {
      entity.HasKey(consumer => consumer.Name);
      entity.Property(consumer => consumer.Name).HasMaxLength(80);
    });

    builder.Entity<OutboxDelivery>(entity =>
    {
      entity.Property(delivery => delivery.ConsumerName).HasMaxLength(80);
      entity.Property(delivery => delivery.LastError).HasMaxLength(500);
      entity.HasIndex(delivery => new { delivery.OutboxMessageId, delivery.ConsumerName }).IsUnique();
      entity.HasIndex(delivery => new { delivery.ConsumerName, delivery.DeliveredAt, delivery.NextAttemptAt });
      entity.HasOne<OutboxMessage>()
          .WithMany()
          .HasForeignKey(delivery => delivery.OutboxMessageId)
          .OnDelete(DeleteBehavior.Cascade);
      entity.HasOne<OutboxConsumer>()
          .WithMany()
          .HasForeignKey(delivery => delivery.ConsumerName)
          .OnDelete(DeleteBehavior.Cascade);
      entity.ToTable(table => table.HasCheckConstraint("CK_OutboxDeliveries_Attempts", "\"AttemptCount\" >= 0"));
    });

    builder.Entity<FundingTransfer>(entity =>
    {
      entity.Property(transfer => transfer.Provider).HasMaxLength(40);
      entity.Property(transfer => transfer.ProviderTransferId).HasMaxLength(100);
      entity.Property(transfer => transfer.Direction).HasConversion<string>().HasMaxLength(16);
      entity.Property(transfer => transfer.Status).HasConversion<string>().HasMaxLength(16);
      entity.Property(transfer => transfer.Currency).HasMaxLength(3).IsFixedLength();
      entity.ToTable(table =>
      {
        table.HasCheckConstraint("CK_FundingTransfers_Amount", "\"AmountMinor\" > 0");
        table.HasCheckConstraint("CK_FundingTransfers_Currency", "\"Currency\" = 'USD'");
        table.HasCheckConstraint("CK_FundingTransfers_Direction", "\"Direction\" IN ('CashIn', 'CashOut')");
        table.HasCheckConstraint("CK_FundingTransfers_Status", "\"Status\" = 'Posted'");
      });
      entity.HasIndex(transfer => transfer.ProviderTransferId).IsUnique();
      entity.HasIndex(transfer => transfer.LedgerTransactionId).IsUnique();
      entity.HasIndex(transfer => new { transfer.UserId, transfer.CreatedAt });
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(transfer => transfer.UserId)
          .OnDelete(DeleteBehavior.Restrict);
      entity.HasOne<LedgerTransaction>()
          .WithMany()
          .HasForeignKey(transfer => transfer.LedgerTransactionId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    builder.Entity<SandboxProviderTransfer>(entity =>
    {
      entity.Property(transfer => transfer.ProviderTransferId).HasMaxLength(100);
      entity.Property(transfer => transfer.Direction).HasConversion<string>().HasMaxLength(16);
      entity.Property(transfer => transfer.Currency).HasMaxLength(3).IsFixedLength();
      entity.Property(transfer => transfer.Status).HasMaxLength(16);
      entity.ToTable(table =>
      {
        table.HasCheckConstraint("CK_SandboxProviderTransfers_Amount", "\"AmountMinor\" > 0");
        table.HasCheckConstraint("CK_SandboxProviderTransfers_Currency", "\"Currency\" = 'USD'");
        table.HasCheckConstraint("CK_SandboxProviderTransfers_Status", "\"Status\" = 'posted'");
      });
      entity.HasIndex(transfer => transfer.FundingTransferId).IsUnique();
      entity.HasIndex(transfer => transfer.ProviderTransferId).IsUnique();
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(transfer => transfer.UserId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    builder.Entity<Notification>(entity =>
    {
      entity.Property(notification => notification.Kind).HasMaxLength(40);
      entity.Property(notification => notification.Title).HasMaxLength(120);
      entity.Property(notification => notification.Body).HasMaxLength(280);
      entity.HasIndex(notification => new { notification.UserId, notification.SourceOutboxMessageId }).IsUnique();
      entity.HasIndex(notification => new { notification.UserId, notification.CreatedAt });
      entity.HasOne<ApplicationUser>()
          .WithMany()
          .HasForeignKey(notification => notification.UserId)
          .OnDelete(DeleteBehavior.Cascade);
      entity.HasOne<OutboxMessage>()
          .WithMany()
          .HasForeignKey(notification => notification.SourceOutboxMessageId)
          .OnDelete(DeleteBehavior.Restrict);
    });

    builder.Entity<WalletControl>(entity =>
    {
      entity.HasKey(control => control.UserId);
      entity.HasOne<ApplicationUser>()
          .WithOne()
          .HasForeignKey<WalletControl>(control => control.UserId)
          .OnDelete(DeleteBehavior.Cascade);
    });

    builder.Entity<ReconciliationRun>(entity =>
    {
      entity.Property(run => run.Status).HasMaxLength(24);
      entity.HasIndex(run => run.CompletedAt);
    });
  }
}
