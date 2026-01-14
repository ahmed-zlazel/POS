using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using POS.Persistence.Logging;
using System.Data;

namespace POS.Persistence.Transaction
{
    public interface ITransactionManager
    {
        Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3);
        Task ExecuteInTransactionAsync(Func<Task> operation, string operationName, int maxRetries = 3);
    }

    public class TransactionManager : ITransactionManager
    {
        private readonly DbContext _context;

        public TransactionManager(DbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, string operationName, int maxRetries = 3)
        {
            var attempt = 0;
            Exception? lastException = null;

            while (attempt < maxRetries)
            {
                attempt++;
                IDbContextTransaction? transaction = null;

                try
                {
                    AppLogger.LogTransaction(operationName, $"Starting transaction (Attempt {attempt}/{maxRetries})");

                    // Start transaction with serializable isolation for maximum consistency
                    transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);

                    var result = await operation();

                    await transaction.CommitAsync();

                    AppLogger.LogTransaction(operationName, $"Transaction committed successfully on attempt {attempt}");

                    return result;
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    lastException = ex;
                    await transaction?.RollbackAsync()!;

                    AppLogger.LogWarning($"Concurrency conflict in {operationName} (Attempt {attempt}/{maxRetries})", new { Exception = ex.Message });

                    if (attempt < maxRetries)
                    {
                        // Refresh entities and retry
                        foreach (var entry in ex.Entries)
                        {
                            await entry.ReloadAsync();
                        }

                        await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt)); // Exponential backoff
                    }
                }
                catch (DbUpdateException ex) when (IsDatabaseLockException(ex))
                {
                    lastException = ex;
                    await transaction?.RollbackAsync()!;

                    AppLogger.LogWarning($"Database locked during {operationName} (Attempt {attempt}/{maxRetries})", new { Exception = ex.Message });

                    if (attempt < maxRetries)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt)); // Longer wait for lock
                    }
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    await transaction?.RollbackAsync()!;

                    AppLogger.LogError(operationName, ex, new { Attempt = attempt, MaxRetries = maxRetries });

                    // Don't retry on non-transient errors
                    throw new TransactionException($"Transaction failed for operation '{operationName}'", ex);
                }
                finally
                {
                    transaction?.Dispose();
                }
            }

            // All retries exhausted
            var errorMessage = $"Transaction failed after {maxRetries} attempts for operation '{operationName}'";
            AppLogger.LogCritical(errorMessage, lastException);
            throw new TransactionException(errorMessage, lastException);
        }

        public async Task ExecuteInTransactionAsync(Func<Task> operation, string operationName, int maxRetries = 3)
        {
            await ExecuteInTransactionAsync(async () =>
            {
                await operation();
                return true;
            }, operationName, maxRetries);
        }

        private bool IsDatabaseLockException(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message?.ToLower() ?? ex.Message.ToLower();
            return message.Contains("database is locked") ||
                   message.Contains("lock") ||
                   message.Contains("timeout") ||
                   message.Contains("deadlock");
        }
    }

    public class TransactionException : Exception
    {
        public TransactionException(string message) : base(message) { }
        public TransactionException(string message, Exception? innerException) : base(message, innerException) { }
    }
}
