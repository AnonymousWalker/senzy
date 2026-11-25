using System.Collections.Concurrent;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Senzy.Infrastructure.Banking;

namespace Senzy.Infrastructure.Banking;

public sealed class StubBankingProvider : IBankingProvider
{
  private static readonly BankingInstitution[] Institutions =
  [
    new("stub-first-national", "First National Sandbox", null),
    new("stub-community-credit", "Community Credit Sandbox", null),
    new("stub-river-bank", "River Bank Sandbox", null),
    new("stub-wells-fargo-bank", "Wells Fargo Sandbox", null)
  ];

  private readonly ConcurrentDictionary<string, LinkSession> _sessions = new();
  private readonly ConcurrentDictionary<Guid, ConcurrentQueue<ExternalBankAccount>> _accounts = new();
  private readonly ConcurrentDictionary<string, FundingProviderResult> _funding = new();
  private readonly StubBankingOptions _options;

  public StubBankingProvider(IOptions<StubBankingOptions> options)
  {
    _options = options.Value;
  }

  public IReadOnlyList<BankingInstitution> ListInstitutions() => Institutions;

  public BankLinkSession CreateLinkSession(Guid userId)
  {
    var token = $"stub_link_{Guid.NewGuid()}";
    var expiresAt = DateTimeOffset.UtcNow.AddMinutes(15);
    _sessions[token] = new LinkSession(userId, expiresAt);
    return new("stub-banking", true, token, expiresAt);
  }

  public ExternalBankAccount ConnectAccount(
      Guid userId,
      string linkToken,
      string institutionId,
      string displayName,
      string accountKind)
  {
    var institution = Institutions.FirstOrDefault(item => item.Id == institutionId)
        ?? throw new BankingProviderException("Unknown sandbox institution.");

    if (!_sessions.TryGetValue(linkToken, out var session) || session.UserId != userId)
    {
      throw new BankingProviderException("Invalid bank link session.");
    }

    lock (session)
    {
      if (session.Used)
      {
        throw new BankingProviderException("Bank link session has already been used.");
      }

      if (session.ExpiresAt <= DateTimeOffset.UtcNow)
      {
        throw new BankingProviderException("Bank link session has expired.");
      }

      session.Used = true;
    }

    var account = new ExternalBankAccount(
        Guid.NewGuid(),
        "stub-banking",
        true,
        institution.Id,
        institution.Name,
        displayName.Trim(),
        RandomNumberGenerator.GetInt32(0, 10_000).ToString("D4"),
        accountKind,
        "linked",
        DateTimeOffset.UtcNow);

    _accounts.GetOrAdd(userId, _ => new ConcurrentQueue<ExternalBankAccount>()).Enqueue(account);
    return account;
  }

  public IReadOnlyList<ExternalBankAccount> ListAccounts(Guid userId) =>
      _accounts.TryGetValue(userId, out var accounts) ? accounts.ToArray() : [];

  public ExternalBankAccount? FindAccount(Guid userId, Guid accountId) =>
      ListAccounts(userId).SingleOrDefault(account => account.Id == accountId);

  public Task<FundingProviderResult> CreateFundingAsync(
      FundingProviderRequest request,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    var result = _funding.GetOrAdd(request.OperationId, _ =>
    {
      var outcome = _options.FundingOutcome switch
      {
        StubFundingOutcome.Failed => FundingProviderOutcome.Failed,
        StubFundingOutcome.Unknown => FundingProviderOutcome.Unknown,
        _ => FundingProviderOutcome.Succeeded
      };

      return new FundingProviderResult(
          request.OperationId,
          outcome == FundingProviderOutcome.Failed ? null : $"stub_transfer_{request.OperationId}",
          outcome);
    });

    return Task.FromResult(result);
  }

  public Task<FundingProviderResult> GetFundingAsync(
      string operationId,
      CancellationToken cancellationToken)
  {
    cancellationToken.ThrowIfCancellationRequested();
    if (!_funding.TryGetValue(operationId, out var result))
    {
      return Task.FromResult(new FundingProviderResult(
          operationId,
          null,
          FundingProviderOutcome.Unknown));
    }

    if (result.Outcome == FundingProviderOutcome.Unknown
        && _options.FundingOutcome == StubFundingOutcome.Succeeded)
    {
      result = result with
      {
        TransferId = $"stub_transfer_{operationId}",
        Outcome = FundingProviderOutcome.Succeeded
      };
      _funding[operationId] = result;
    }

    return Task.FromResult(result);
  }

  private sealed class LinkSession(Guid userId, DateTimeOffset expiresAt)
  {
    public Guid UserId { get; } = userId;
    public DateTimeOffset ExpiresAt { get; } = expiresAt;
    public bool Used { get; set; }
  }
}

public enum StubFundingOutcome
{
  Succeeded,
  Failed,
  Unknown
}

public sealed class StubBankingOptions
{
  public StubFundingOutcome FundingOutcome { get; set; } = StubFundingOutcome.Succeeded;
}
