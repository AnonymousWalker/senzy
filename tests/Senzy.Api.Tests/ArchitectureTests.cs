using Senzy.Api.Features.Funding;
using Senzy.Api.Features.Ledger;
using Senzy.Api.Features.Payments;
using Senzy.Api.Features.Profiles;
using Senzy.Api.Features.Wallets;
using Senzy.Api.Infrastructure.Outbox;
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
}
