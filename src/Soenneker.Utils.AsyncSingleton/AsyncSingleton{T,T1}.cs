using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Locks;

namespace Soenneker.Utils.AsyncSingleton;

/// <summary>
/// Async-safe, allocation-free singleton initialized with one argument.
/// The first successful initialization wins; subsequent calls ignore arguments.
/// </summary>
public sealed class AsyncSingleton<T, T1> : IAsyncSingleton<T, T1>
{
    private T? _instance;

    private ValueAtomicBool _hasValue;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    private readonly Func<T1, ValueTask<T>>? _asyncFactory;
    private readonly Func<CancellationToken, T1, ValueTask<T>>? _asyncFactoryToken;

    private readonly Func<T1, T>? _syncFactory;
    private readonly Func<CancellationToken, T1, T>? _syncFactoryToken;

    public AsyncSingleton(Func<T1, ValueTask<T>> factory) => _asyncFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(Func<CancellationToken, T1, ValueTask<T>> factory) =>
        _asyncFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(Func<T1, T> factory) => _syncFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(Func<CancellationToken, T1, T> factory) => _syncFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public ValueTask<T> Get(T1 arg, CancellationToken cancellationToken = default) => GetOrCreate(cancellationToken, arg);

    private ValueTask<T> GetOrCreate(CancellationToken cancellationToken, T1 arg)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T, T1>).Name);

        if (_hasValue.Value)
            return new ValueTask<T>(_instance!);

        return Slow(cancellationToken, arg);
    }

    private async ValueTask<T> Slow(CancellationToken cancellationToken, T1 a)
    {
        using (await _lock.Lock(cancellationToken)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T, T1>).Name);

            if (_hasValue.Value)
                return _instance!;

            T created = await Create(cancellationToken, a)
                .NoSync();

            _instance = created!;
            _hasValue.Value = true;

            return created;
        }
    }

    public T GetSync(T1 arg, CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T, T1>).Name);

        if (_hasValue.Value)
            return _instance!;

        using (_lock.LockSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T, T1>).Name);

            if (_hasValue.Value)
                return _instance!;

            T created = CreateSync(cancellationToken, arg);

            _instance = created!;
            _hasValue.Value = true;

            return created;
        }
    }

    private ValueTask<T> Create(CancellationToken cancellationToken, T1 arg)
    {
        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(cancellationToken, arg);

        if (_asyncFactory is not null)
            return _asyncFactory(arg);

        if (_syncFactoryToken is not null)
            return new ValueTask<T>(_syncFactoryToken(cancellationToken, arg));

        if (_syncFactory is not null)
            return new ValueTask<T>(_syncFactory(arg));

        throw new InvalidOperationException("No initialization factory was configured.");
    }

    private T CreateSync(CancellationToken cancellationToken, T1 arg)
    {
        if (_syncFactoryToken is not null)
            return _syncFactoryToken(cancellationToken, arg);

        if (_syncFactory is not null)
            return _syncFactory(arg);

        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(cancellationToken, arg)
                .AwaitSync();

        if (_asyncFactory is not null)
            return _asyncFactory(arg)
                .AwaitSync();

        throw new InvalidOperationException("No initialization factory was configured.");
    }

    public void Dispose()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        object? local;

        using (_lock.LockSync())
        {
            _hasValue.Value = false;
            local = _instance;
            _instance = default;
        }

        if (local is IAsyncDisposable ad)
            ad.DisposeAsync()
              .AwaitSync();
        else if (local is IDisposable d)
            d.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        object? local;

        using (await _lock.Lock()
                          .NoSync())
        {
            _hasValue.Value = false;
            local = _instance;
            _instance = default;
        }

        if (local is IAsyncDisposable ad)
            await ad.DisposeAsync()
                    .NoSync();
        else if (local is IDisposable d)
            d.Dispose();
    }
}