namespace Senzy.Modules.Ledger;

public sealed class LedgerPostingService(
    ILedgerPostingStore store,
    LedgerPostingEngine engine) : ILedgerPostingService
{
  public LedgerPostingResult Post(
      LedgerPostingRequest request,
      CancellationToken cancellationToken = default)
  {
    cancellationToken.ThrowIfCancellationRequested();

    var context = store.Load(request.DebitAccountId, request.CreditAccountId);
    var plan = engine.CreatePlan(context.Debit, context.Credit, request);
    return new(store.Save(plan));
  }
}
