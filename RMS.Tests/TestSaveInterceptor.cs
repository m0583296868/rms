using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RMS.Models;

namespace RMS.Tests
{
    public sealed class TestSaveInterceptor : SaveChangesInterceptor
    {
        public static Barrier? Gate;
        public static int ConflictRequestId;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Prepare(eventData.Context);
            Gate?.SignalAndWait(TimeSpan.FromSeconds(15));
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        private static void Prepare(DbContext? context)
        {
            var id = ConflictRequestId;
            if (context == null || id == 0)
                return;

            ConflictRequestId = 0;
            var entry = context.ChangeTracker.Entries<Requests>().FirstOrDefault(e => e.Entity.Id == id);
            if (entry != null)
                entry.Property(r => r.Version).OriginalValue = -1;
        }
    }
}
