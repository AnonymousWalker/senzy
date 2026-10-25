using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Wallets;
using Senzy.Api.Features.Funding;
using Senzy.Infrastructure.Banking;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Infrastructure.Outbox;
using Senzy.Modules.Ledger;
using Senzy.Modules.Wallets;
using Senzy.Modules.Payments;
using Senzy.Modules.Funding;
using System.Xml.Linq;
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
    var parameterTypes = typeof(WalletMovementCoordinator).GetConstructors().Single()
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
      typeof(IWalletBalanceReader),
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

  [Fact]
  public void LedgerPostingUsesPersistenceContract()
  {
    var parameterTypes = typeof(LedgerPostingService).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(ILedgerPostingStore), parameterTypes);
    Assert.DoesNotContain(typeof(Senzy.Api.Infrastructure.Persistence.SenzyDbContext), parameterTypes);
  }

  [Fact]
  public void WalletMovementUsesPersistenceContract()
  {
    var parameterTypes = typeof(WalletMovementCoordinator).GetConstructors().Single()
        .GetParameters().Select(parameter => parameter.ParameterType).ToList();

    Assert.Contains(typeof(IWalletMovementStore), parameterTypes);
    Assert.DoesNotContain(typeof(Senzy.Api.Infrastructure.Persistence.SenzyDbContext), parameterTypes);
  }

  [Fact]
  public void ModuleProjectsDoNotReferenceTheApiHost()
  {
    foreach (var project in ModuleProjects())
    {
      var document = XDocument.Load(project);
      var references = document.Descendants("ProjectReference")
          .Select(reference => reference.Attribute("Include")?.Value ?? "");

      Assert.DoesNotContain(references, reference =>
          reference.EndsWith("Senzy.Api.csproj", StringComparison.OrdinalIgnoreCase));
    }
  }

  [Fact]
  public void ModuleProjectReferencesAreAcyclic()
  {
    var projects = ModuleProjects().ToDictionary(
        path => Path.GetFileNameWithoutExtension(path),
        path => ProjectDependencies(path));

    var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var project in projects.Keys)
    {
      Assert.True(Visit(project), $"Circular module dependency detected at {project}.");
    }

    bool Visit(string project)
    {
      if (visiting.Contains(project))
      {
        return false;
      }

      if (!visited.Add(project))
      {
        return true;
      }

      visiting.Add(project);
      foreach (var dependency in projects[project].Where(projects.ContainsKey))
      {
        if (!Visit(dependency))
        {
          return false;
        }
      }

      visiting.Remove(project);
      return true;
    }
  }

  [Fact]
  public void FinancialWorkflowsAreOwnedByTheirModuleAssemblies()
  {
    Assert.Equal(typeof(PaymentStateMachine).Assembly, typeof(PaymentService).Assembly);
    Assert.Equal(typeof(FundingDirection).Assembly, typeof(FundingService).Assembly);
    Assert.DoesNotContain("Senzy.Api", typeof(PaymentService).Assembly.GetName().Name);
    Assert.DoesNotContain("Senzy.Api", typeof(FundingService).Assembly.GetName().Name);
  }

  [Fact]
  public void FinancialModulesDoNotWriteForeignFinancialRecords()
  {
    var moduleRoots = new[]
    {
      Path.Combine(RepositoryRoot(), "api-server/Modules/Payments"),
      Path.Combine(RepositoryRoot(), "api-server/Modules/Funding")
    };

    var forbidden = new[]
    {
      "LedgerAccounts",
      "LedgerTransactions",
      "LedgerEntries",
      "WalletControls",
      "WalletPolicyStore",
      "WalletMovementStore",
      "LedgerPostingStore"
    };

    foreach (var root in moduleRoots)
    {
      foreach (var source in Directory.EnumerateFiles(root, "*.cs"))
      {
        var text = File.ReadAllText(source);
        Assert.DoesNotContain(forbidden, token => text.Contains(token, StringComparison.Ordinal));
      }
    }
  }

  private static IReadOnlyList<string> ModuleProjects() =>
      Directory.GetFiles(
          Path.Combine(RepositoryRoot(), "api-server/Modules"),
          "*.csproj",
          SearchOption.AllDirectories);

  private static IReadOnlyList<string> ProjectDependencies(string project)
  {
    var directory = Path.GetDirectoryName(project)!;
    var document = XDocument.Load(project);
    return document.Descendants("ProjectReference")
        .Select(reference => reference.Attribute("Include")?.Value)
        .Where(value => value is not null)
        .Select(value => Path.GetFileNameWithoutExtension(Path.GetFullPath(Path.Combine(directory, value!))))
        .ToList();
  }

  private static string RepositoryRoot() =>
      Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

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
