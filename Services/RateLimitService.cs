namespace AppointmentBookingSystem.Services;

public enum RateLimitAction { Book, Reschedule }

public class RateLimitService(IConfiguration config)
{
    // key = "{customerId}:{action}:{yyyyMMdd}"
    private readonly Dictionary<string, int> _counts = new();
    private readonly Lock _lock = new();

    // Limits (configurable from appsettings) 
    public int MaxBookingsPerDay => config.GetValue("RateLimit:MaxBookingsPerDay", 5);
    public int MaxReschedulesPerDay => config.GetValue("RateLimit:MaxReschedulesPerDay", 3);

    // Public methods 

    /// <summary>Returns true if the customer is allowed to perform the action.</summary>
    public bool IsAllowed(string customerId, RateLimitAction action)
    {
        var limit = action == RateLimitAction.Book
            ? MaxBookingsPerDay
            : MaxReschedulesPerDay;

        var key = BuildKey(customerId, action);

        lock (_lock)
        {
            _counts.TryGetValue(key, out var current);
            return current < limit;
        }
    }

    /// <summary>Increments the counter after a successful action.</summary>
    public void Increment(string customerId, RateLimitAction action)
    {
        var key = BuildKey(customerId, action);

        lock (_lock)
        {
            _counts.TryGetValue(key, out var current);
            _counts[key] = current + 1;
        }
    }

    /// <summary>Returns current usage for the customer today.</summary>
    public (int bookings, int reschedules) GetUsage(string customerId)
    {
        lock (_lock)
        {
            _counts.TryGetValue(BuildKey(customerId, RateLimitAction.Book), out var b);
            _counts.TryGetValue(BuildKey(customerId, RateLimitAction.Reschedule), out var r);
            return (b, r);
        }
    }

    // Private helpers 
    private static string BuildKey(string customerId, RateLimitAction action) =>
        $"{customerId}:{action}:{DateTime.UtcNow:yyyyMMdd}";
}