using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Wallets;
using Senzy.Api.Features.Funding;
using Senzy.Api.Features.Banking;
using Senzy.Api.Features.Payments;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Infrastructure.Outbox;
using Senzy.Modules.Ledger;
using Senzy.Modules.Wallets;
using System.Collections;
using System.Reflection;
using System.Linq;
using Xunit;

namespace Senzy.Api.Tests;

public sealed class ArchitectureTests
{
  [Fact]
  public void PaymentWorkflowUsesModuleContractsForMovementAndRecipients()
  {
    var parameterTypes = typeof(PaymentService).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(IWalletMovementService), parameterTypes);
    Assert.DoesNotContain(typeof(LedgerPostingService), parameterTypes);
    Assert.DoesNotContain(typeof(WalletPolicy), parameterTypes);
  }

  [Fact]
  public void FundingWorkflowUsesWalletMovementContract()
  {
    var parameterTypes = typeof(FundingService).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(IWalletMovementService), parameterTypes);
    Assert.DoesNotContain(typeof(LedgerPostingService), parameterTypes);
  }

  [Fact]
  public void WalletMovementUsesLedgerContract()
  {
    var parameterTypes = typeof(WalletMovementService).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(ILedgerPostingService), parameterTypes);
    Assert.DoesNotContain(typeof(LedgerPostingService), parameterTypes);
  }

  [Fact]
  public void OutboxInfrastructureDelegatesBusinessHandlingToNotifications()
  {
    var parameterTypes = typeof(OutboxDispatcher).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(Senzy.Api.Features.Notifications.NotificationEventHandler), parameterTypes);
  }

  [Fact]
  public void ScaleCriticalContractsDoNotExposeInfrastructureOrTransportTypes()
  {
    var contracts = new[]
    {
      typeof(IWalletPolicy),
      typeof(IWalletMovementService),
      typeof(ILedgerPostingService),
      typeof(IBankingProvider)
    };

    var exposedTypes = contracts
        .SelectMany(contract => contract.GetMethods())
        .SelectMany(method => method.GetParameters()
            .Select(parameter => parameter.ParameterType)
            .Append(method.ReturnType))
        .SelectMany(FlattenTypes)
        .ToList();

    Assert.DoesNotContain(exposedTypes, type =>
        type == typeof(Senzy.Api.Infrastructure.Persistence.SenzyDbContext)
        || type.Namespace?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) == true
        || type.Namespace?.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) == true
        || type == typeof(IQueryable));
  }

  [Fact]
  public void ScaleCriticalWorkflowsDoNotDependOnConcreteFinancialImplementations()
  {
    var paymentAndFundingParameters = new[]
    {
      typeof(PaymentService),
      typeof(FundingService)
    }
        .SelectMany(service => service.GetConstructors().Single().GetParameters())
        .Select(parameter => parameter.ParameterType)
        .ToList();

    var walletParameters = typeof(WalletMovementService).GetConstructors().Single()
        .GetParameters()
        .Select(parameter => parameter.ParameterType)
        .ToList();

    Assert.DoesNotContain(typeof(LedgerPostingService), paymentAndFundingParameters);
    Assert.DoesNotContain(typeof(WalletPolicy), paymentAndFundingParameters);
    Assert.DoesNotContain(typeof(LedgerPostingService), walletParameters);
  }

  [Fact]
  public void WalletPolicyUsesPersistenceContract()
  {
    var parameterTypes = typeof(WalletPolicy).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(IWalletPolicyStore), parameterTypes);
    Assert.DoesNotContain(typeof(Senzy.Api.Infrastructure.Persistence.SenzyDbContext), parameterTypes);
  }

  private static IEnumerable<Type> FlattenTypes(Type type)
  {
    if (type.IsByRef)
    {
      type = type.GetElementType()!;
    }

    yield return type;

    if (type.IsGenericType)
    {
      foreach (var argument in type.GetGenericArguments().SelectMany(FlattenTypes))
      {
        yield return argument;
      }
    }
  }
}
