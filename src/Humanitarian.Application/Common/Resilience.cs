namespace Humanitarian.Application.Common;

/// <summary>An external service (mapping, e-mail ...) kept failing after every retry.</summary>
public class ExternalServiceException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Retry + timeout policy for calls to external services: each attempt has its own timeout, failed attempts are
/// retried with exponential back-off, and when every attempt fails an <see cref="ExternalServiceException"/> is thrown
/// so the caller can fall back (for example to a distance estimate). The caller cancelling is never treated as a failure.
/// </summary>
public static class ResilientCall
{
    public static async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        int maxAttempts, TimeSpan timeout, TimeSpan baseDelay,
        CancellationToken ct = default,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= (d, token) => Task.Delay(d, token);
        Exception? last = null;

        for (var attempt = 1; attempt <= Math.Max(1, maxAttempts); attempt++)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(timeout);
                return await operation(cts.Token);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // the caller gave up: do not hide that
            }
            catch (Exception ex)
            {
                last = ex;
                if (attempt < maxAttempts)
                    await delay(TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1)), ct);
            }
        }

        throw new ExternalServiceException($"The external service failed after {maxAttempts} attempt(s).", last);
    }
}
