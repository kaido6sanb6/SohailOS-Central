using System.Collections.Concurrent;
using SohailOS.Core;

namespace SohailOS.Memory;

public sealed class InMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<string, string> _items = new();
    public Task SaveAsync(string key, string value, CancellationToken cancellationToken = default)
    { _items[key] = value; return Task.CompletedTask; }
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_items.TryGetValue(key, out var value) ? value : null);
}
