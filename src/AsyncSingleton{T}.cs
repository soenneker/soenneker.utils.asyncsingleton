using Soenneker.Atomics.ValueBools;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using System;
using System.Threading;
using System.Threading.Tasks;
using Soenneker.Asyncs.Locks;

namespace Soenneker.Utils.AsyncSingleton;

/// <inheritdoc cref="IAsyncSingleton{T}"/>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AsyncSingleton<T> : IAsyncSingleton<T>
{
    // Boxed for value types; reference types are stored directly.
    private object? _instance;

    private ValueAtomicBool _hasValue;
    private ValueAtomicBool _disposed;

    private readonly AsyncLock _lock = new();

    private readonly Func<ValueTask<T>>? _asyncFactory;
    private readonly Func<CancellationToken, ValueTask<T>>? _asyncFactoryToken;

    private readonly Func<T>? _syncFactory;
    private readonly Func<CancellationToken, T>? _syncFactoryToken;

    private readonly object? _state;
    private readonly Func<object, CancellationToken, ValueTask<T>>? _asyncFactoryTokenState;

    public AsyncSingleton(Func<ValueTask<T>> factory) => _asyncFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(Func<CancellationToken, ValueTask<T>> factory) => _asyncFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(object state, Func<object, CancellationToken, ValueTask<T>> factory)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _asyncFactoryTokenState = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public AsyncSingleton(Func<T> factory) => _syncFactory = factory ?? throw new ArgumentNullException(nameof(factory));

    public AsyncSingleton(Func<CancellationToken, T> factory) => _syncFactoryToken = factory ?? throw new ArgumentNullException(nameof(factory));

    public ValueTask<T> Get(CancellationToken cancellationToken = default) => GetOrCreate(cancellationToken);

    public virtual ValueTask<T> GetOrCreate(CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        // Fast path (no lock)
        if (_hasValue.Value)
            return new ValueTask<T>((T)_instance!);

        return Slow(cancellationToken);
    }

    private async ValueTask<T> Slow(CancellationToken ct)
    {
        using (await _lock.Lock(ct)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

            if (_hasValue.Value)
                return (T)_instance!;

            T created = await Create(ct)
                .NoSync();

            _instance = created!;
            _hasValue.Value = true;

            return created;
        }
    }

    public T GetSync(CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_hasValue.Value)
            return (T)_instance!;

        using (_lock.LockSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

            if (_hasValue.Value)
                return (T)_instance!;

            T created = CreateSync(cancellationToken);

            _instance = created!;
            _hasValue.Value = true;

            return created;
        }
    }

    private ValueTask<T> Create(CancellationToken ct)
    {
        if (_asyncFactoryTokenState is not null)
            return _asyncFactoryTokenState(_state!, ct);

        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(ct);

        if (_asyncFactory is not null)
            return _asyncFactory();

        if (_syncFactoryToken is not null)
            return new ValueTask<T>(_syncFactoryToken(ct));

        if (_syncFactory is not null)
            return new ValueTask<T>(_syncFactory());

        throw new InvalidOperationException("No initialization factory was configured.");
    }

    private T CreateSync(CancellationToken ct)
    {
        if (_asyncFactoryTokenState is not null)
            return _asyncFactoryTokenState(_state!, ct).AwaitSync();

        if (_syncFactoryToken is not null)
            return _syncFactoryToken(ct);

        if (_syncFactory is not null)
            return _syncFactory();

        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(ct)
                .AwaitSync();

        if (_asyncFactory is not null)
            return _asyncFactory()
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
            _instance = null;
        }

        // Prefer async disposal if supported (even in sync Dispose).
        if (local is IAsyncDisposable ad)
            ad.DisposeAsync()
              .AwaitSync();
        else if (local is IDisposable d)
            d.Dispose();

        GC.SuppressFinalize(this);
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
            _instance = null;
        }

        if (local is IAsyncDisposable ad)
            await ad.DisposeAsync()
                    .NoSync();
        else if (local is IDisposable d)
            d.Dispose();

        GC.SuppressFinalize(this);
    }
}