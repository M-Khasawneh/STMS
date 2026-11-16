namespace SourcingTenderManagementSystem.Web.Data;

// Called only while holding the supplier-registration storage lease and outside its SQL transaction.
public static class SupplierDocumentRecovery
{
    public static async Task ReconcileAsync(IPrivateSupplierFiles files, Func<string, CancellationToken, Task<bool>> committedReference,
        CancellationToken ct, IEnumerable<string>? known = null)
    {
        foreach (var reference in (await files.PendingAsync(ct)).Concat(known ?? []).Distinct())
        {
            // If SQL cannot establish the outcome, leave both bytes and durable intent intact.
            if (!await committedReference(reference, ct)) files.Delete(reference);
            files.Complete(reference);
        }
    }
}
