using Nito.AsyncEx;
using Soenneker.Atomics.Bools;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using Soenneker.Utils.AsyncSingleton.Enums;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.AsyncSingleton;

/// <inheritdoc cref="IAsyncSingleton{T}"/>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AsyncSingleton<T> : IAsyncSingleton<T>
{
    // Boxed for value types; reference types are stored directly.
    private object? _instance;

    private AtomicBool _hasValue;

    private readonly AsyncLock _lock = new();

    private Func<ValueTask<T>>? _asyncFunc;
    private Func<T>? _func;

    private Func<object[], ValueTask<T>>? _asyncObjectFunc;
    private Func<object[], T>? _objectFunc;

    private Func<CancellationToken, object[], ValueTask<T>>? _asyncObjectTokenFunc;
    private Func<CancellationToken, object[], T>? _objectTokenFunc;

    private bool _disposed;
    private InitializationType _initializationType;

    public AsyncSingleton(Func<object[], ValueTask<T>> func) : this()
    {
        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public AsyncSingleton(Func<CancellationToken, object[], ValueTask<T>> func) : this()
    {
        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public AsyncSingleton(Func<ValueTask<T>> func) : this()
    {
        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public AsyncSingleton(Func<CancellationToken, object[], T> func) : this()
    {
        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public AsyncSingleton(Func<object[], T> func) : this()
    {
        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public AsyncSingleton(Func<T> func) : this()
    {
        _initializationType = InitializationType.Sync;
        _func = func;
    }

    public AsyncSingleton()
    {
    }

    public ValueTask<T> Get(params object[] objects) => Get(CancellationToken.None, objects);

    public virtual ValueTask<T> Get(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        // Fast path (no lock)
        if (_hasValue.Value)
            return new ValueTask<T>((T)Volatile.Read(ref _instance)!);

        return GetSlow(cancellationToken, objects);

        async ValueTask<T> GetSlow(CancellationToken ct, object[] args)
        {
            using (await _lock.LockAsync(ct).ConfigureAwait(false))
            {
                if (_hasValue.Value)
                    return (T)Volatile.Read(ref _instance)!;

                T created = await GetInternal(ct, args).NoSync();

                // Publish instance first, then publish the flag (release)
                Volatile.Write(ref _instance, created!);
                _hasValue.Value = true;

                return created;
            }
        }
    }

    private async ValueTask<T> GetInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                return _asyncObjectFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : await _asyncObjectFunc(objects).NoSync();

            case InitializationType.AsyncObjectToken:
                return _asyncObjectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : await _asyncObjectTokenFunc(cancellationToken, objects).NoSync();

            case InitializationType.Async:
                return _asyncFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : await _asyncFunc().NoSync();

            case InitializationType.SyncObject:
                return _objectFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectFunc(objects);

            case InitializationType.SyncObjectToken:
                return _objectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectTokenFunc(cancellationToken, objects);

            case InitializationType.Sync:
                return _func is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _func();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public T GetSync(params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_hasValue.Value)
            return (T)Volatile.Read(ref _instance)!;

        using (_lock.Lock())
        {
            if (_hasValue.Value)
                return (T)Volatile.Read(ref _instance)!;

            T created = GetSyncInternal(objects);

            Volatile.Write(ref _instance, created!);
            _hasValue.Value = true;

            return created;
        }
    }

    private T GetSyncInternal(params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                return _asyncObjectFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncObjectFunc(objects).AwaitSync();

            case InitializationType.AsyncObjectToken:
                return _asyncObjectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncObjectTokenFunc(CancellationToken.None, objects).AwaitSync();

            case InitializationType.Async:
                return _asyncFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncFunc().AwaitSync();

            case InitializationType.SyncObject:
                return _objectFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectFunc(objects);

            case InitializationType.SyncObjectToken:
                return _objectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectTokenFunc(CancellationToken.None, objects);

            case InitializationType.Sync:
                return _func is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _func();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetInitialization(Func<object[], ValueTask<T>> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], ValueTask<T>> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public void SetInitialization(Func<ValueTask<T>> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public void SetInitialization(Func<object[], T> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], T> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public void SetInitialization(Func<T> func)
    {
        if (_hasValue.Value)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Sync;
        _func = func;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_hasValue.Value)
        {
            GC.SuppressFinalize(this);
            return;
        }

        object? local = Volatile.Read(ref _instance);

        if (local is IDisposable disposable)
        {
            disposable.Dispose();
        }
        else if (local is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AwaitSync();
        }

        Volatile.Write(ref _instance, null);
        _hasValue.Value = false;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!_hasValue.Value)
        {
            GC.SuppressFinalize(this);
            return;
        }

        object? local = Volatile.Read(ref _instance);

        if (local is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().NoSync();
        }
        else if (local is IDisposable disposable)
        {
            disposable.Dispose();
        }

        Volatile.Write(ref _instance, null);
        _hasValue.Value = false;
        GC.SuppressFinalize(this);
    }
}
