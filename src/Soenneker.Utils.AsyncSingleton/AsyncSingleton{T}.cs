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
/// <inheritdoc cref="IAsyncSingleton"/>
public class AsyncSingleton<T> : IAsyncSingleton<T>
{
    private T? _instance;

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

    /// <summary>
    /// Returns the cached singleton value or runs the asynchronous factory once to create it.
    /// </summary>
    /// <param name="cancellationToken">Signals that the operation should stop.</param>
    /// <returns>The existing singleton value or the factory-produced value.</returns>
    public virtual ValueTask<T> GetOrCreate(CancellationToken cancellationToken = default)
    {
        if (_disposed.Value)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        // Fast path (no lock)
        if (_hasValue.Value)
            return new ValueTask<T>(_instance!);

        return Slow(cancellationToken);
    }

    private async ValueTask<T> Slow(CancellationToken cancellationToken)
    {
        using (await _lock.Lock(cancellationToken)
                          .NoSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

            if (_hasValue.Value)
                return _instance!;

            T created = await Create(cancellationToken)
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
            return _instance!;

        using (_lock.LockSync())
        {
            if (_disposed.Value)
                throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

            if (_hasValue.Value)
                return _instance!;

            T created = CreateSync(cancellationToken);

            _instance = created!;
            _hasValue.Value = true;

            return created;
        }
    }

    private ValueTask<T> Create(CancellationToken cancellationToken)
    {
        if (_asyncFactoryTokenState is not null)
            return _asyncFactoryTokenState(_state!, cancellationToken);

        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(cancellationToken);

        if (_asyncFactory is not null)
            return _asyncFactory();

        if (_syncFactoryToken is not null)
            return new ValueTask<T>(_syncFactoryToken(cancellationToken));

        if (_syncFactory is not null)
            return new ValueTask<T>(_syncFactory());

        throw new InvalidOperationException("No initialization factory was configured.");
    }

    private T CreateSync(CancellationToken cancellationToken)
    {
        if (_asyncFactoryTokenState is not null)
            return _asyncFactoryTokenState(_state!, cancellationToken).AwaitSync();

        if (_syncFactoryToken is not null)
            return _syncFactoryToken(cancellationToken);

        if (_syncFactory is not null)
            return _syncFactory();

        if (_asyncFactoryToken is not null)
            return _asyncFactoryToken(cancellationToken)
                .AwaitSync();

        if (_asyncFactory is not null)
            return _asyncFactory()
                .AwaitSync();

        throw new InvalidOperationException("No initialization factory was configured.");
    }

    /// <summary>
    /// Releases resources used by the current instance.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        T? local;

        using (_lock.LockSync())
        {
            _hasValue.Value = false;
            local = _instance;
            _instance = default;
        }

        // Prefer async disposal if supported (even in sync Dispose).
        if (local is IAsyncDisposable ad)
            ad.DisposeAsync()
              .AwaitSync();
        else if (local is IDisposable d)
            d.Dispose();
    }

    /// <summary>
    /// Asynchronously releases resources used by the current instance.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async ValueTask DisposeAsync()
    {
        if (!_disposed.CompareAndSet(false, true))
            return;

        T? local;

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
