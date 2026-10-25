namespace Senzy.Modules.Payments;

public enum PaymentStatus
{
  Pending,
  Posted,
  Failed,
  Reversed
}

public static class PaymentStateMachine
{
  public static bool CanTransition(PaymentStatus from, PaymentStatus to) => (from, to) switch
  {
    (PaymentStatus.Pending, PaymentStatus.Posted or PaymentStatus.Failed) => true,
    (PaymentStatus.Posted, PaymentStatus.Reversed) => true,
    _ => false
  };
}
