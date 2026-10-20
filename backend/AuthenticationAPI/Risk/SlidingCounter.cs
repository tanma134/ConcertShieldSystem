using System.Collections.Concurrent;

namespace AuthenticationAPI.Risk;

/// <summary>Đếm số lần xảy ra trong một cửa sổ thời gian (trong bộ nhớ, đủ cho demo/1 instance).</summary>
public sealed class SlidingCounter
{
    private readonly ConcurrentDictionary<string, Queue<long>> _d = new();

    /// <summary>Ghi 1 lần xảy ra và trả về tổng số lần trong cửa sổ (đã gồm lần này).</summary>
    public int Hit(string key, TimeSpan window)
    {
        var now = Environment.TickCount64;
        var min = now - (long)window.TotalMilliseconds;
        var q = _d.GetOrAdd(key, _ => new Queue<long>());
        int count;
        lock (q)
        {
            q.Enqueue(now);
            while (q.Count > 0 && q.Peek() < min) q.Dequeue();
            count = q.Count;
        }
        if (_d.Count > 5000) Purge(now - 120_000);
        return count;
    }

    /// <summary>Đếm không ghi thêm.</summary>
    public int Peek(string key, TimeSpan window)
    {
        if (!_d.TryGetValue(key, out var q)) return 0;
        var min = Environment.TickCount64 - (long)window.TotalMilliseconds;
        lock (q) { return q.Count(t => t >= min); }
    }

    private void Purge(long olderThan)
    {
        foreach (var kv in _d)
        {
            bool stale;
            lock (kv.Value) { stale = kv.Value.Count == 0 || kv.Value.Peek() < olderThan && kv.Value.Last() < olderThan; }
            if (stale) _d.TryRemove(kv.Key, out _);
        }
    }
}
