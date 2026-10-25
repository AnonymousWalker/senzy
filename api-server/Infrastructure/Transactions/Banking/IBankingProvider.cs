namespace Senzy.Infrastructure.Banking;

public record BankingInstitution(string Id, string Name, string? LogoUrl);

public record BankLinkSession(
    string Provider,
    bool Sandbox,
    string LinkToken,
    DateTimeOffset ExpiresAt);

public record ExternalBankAccount(
    Guid Id,
    string Provider,
    bool Sandbox,
    string InstitutionId,
    string InstitutionName,
    string DisplayName,
    string Mask,
    string AccountKind,
    string Status,
    DateTimeOffset CreatedAt);

public interface IBankingProvider
{
  IReadOnlyList<BankingInstitution> ListInstitutions();
  BankLinkSession CreateLinkSession(Guid userId);
  ExternalBankAccount ConnectAccount(
      Guid userId,
      string linkToken,
      string institutionId,
      string displayName,
      string accountKind);
  IReadOnlyList<ExternalBankAccount> ListAccounts(Guid userId);
  ExternalBankAccount? FindAccount(Guid userId, Guid accountId);
}

public sealed class BankingProviderException(string message) : Exception(message);
