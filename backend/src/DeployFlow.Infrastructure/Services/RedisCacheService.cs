using DeployFlow.Application.Common;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DeployFlow.Infrastructure.Services;

/// <summary>
/// Cache service backed by Redis, with automatic in-memory fallback when Redis is unavailable.
/// </summary>
public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _redis;
    private readonly IMemoryCache _memory;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IDistributedCache redis, IMemoryCache memory, ILogger<RedisCacheService> logger)
    {
        _redis  = redis;
        _memory = memory;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var json = await _redis.GetStringAsync(key, ct);
            if (json is not null) return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for GET {Key}, falling back to memory cache.", key);
        }
        return _memory.TryGetValue<T>(key, out var v) ? v : default;
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json    = JsonSerializer.Serialize(value);
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(15)
        };
        try
        {
            await _redis.SetStringAsync(key, json, options, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable for SET {Key}, using memory cache fallback.", key);
        }
        // Always write to memory so callers succeed even without Redis
        _memory.Set(key, value, expiry ?? TimeSpan.FromMinutes(15));
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try { await _redis.RemoveAsync(key, ct); } catch { /* ignore */ }
        _memory.Remove(key);
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        try
        {
            if (await _redis.GetStringAsync(key, ct) is not null) return true;
        }
        catch { /* ignore */ }
        return _memory.TryGetValue<object>(key, out _);
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null) return cached;
        var value = await factory();
        await SetAsync(key, value, expiry, ct);
        return value;
    }
}
