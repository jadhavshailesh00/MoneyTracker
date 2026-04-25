namespace Budget.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
        Task RemoveAsync(string key);
    }

    /// <summary>
    /// In-memory cache (for monolith).
    /// Replace with Redis in microservices.
    /// </summary>
    public class InMemoryCacheService : ICacheService
    {
        private readonly Dictionary<string, (object Value, DateTime Expiry)> _cache = new();
        private readonly object _lock = new();

        public Task<T?> GetAsync<T>(string key) where T : class
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(key, out var entry))
                {
                    if (entry.Expiry > DateTime.UtcNow)
                    {
                        return Task.FromResult((T?)entry.Value);
                    }
                    _cache.Remove(key);
                }
            }
            return Task.FromResult<T?>(null);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class
        {
            lock (_lock)
            {
                _cache[key] = (value, DateTime.UtcNow.Add(expiry ?? TimeSpan.FromMinutes(15)));
            }
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            lock (_lock)
            {
                _cache.Remove(key);
            }
            return Task.CompletedTask;
        }
    }
}