using Senzy.Api.Infrastructure.Persistence.Models;

namespace Senzy.Api.Features.Payments;

public static class PaymentStateMachine
{
    public static bool CanTransition(PaymentStatus from, PaymentStatus to) => (from, to) switch
    {
        (PaymentStatus.Pending, PaymentStatus.Posted or PaymentStatus.Failed) => true,
        (PaymentStatus.Posted, PaymentStatus.Reversed) => true,
        _ => false
    };
}
