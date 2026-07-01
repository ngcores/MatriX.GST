using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MatriX.GST.Services;

public class BufferPool : IDisposable
{
    static readonly ConcurrentBag<byte[]> _pool = new();

    private byte[] _buf;
    private int _disposed;

    public BufferPool()
    {
        if (!_pool.TryTake(out _buf))
            _buf = new byte[80 * 1024];
    }

    public byte[] Buffer
        => _buf;

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        _pool.Add(_buf);
    }
}
