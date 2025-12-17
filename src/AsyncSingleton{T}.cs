using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using Soenneker.Utils.AsyncSingleton.Enums;

namespace Soenneker.Utils.AsyncSingleton;

///<inheritdoc cref="IAsyncSingleton{T}"/>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AsyncSingleton<T> : IAsyncSingleton<T> where T : class
{
    private T? _instance;
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

        T? inst = Volatile.Read(ref _instance);
        if (inst is not null)
            return new ValueTask<T>(inst);

        return GetSlow(cancellationToken, objects);

        async ValueTask<T> GetSlow(CancellationToken ct, object[] args)
        {
            using (await _lock.LockAsync(ct)
                              .ConfigureAwait(false))
            {
                inst = Volatile.Read(ref _instance);
                if (inst is not null)
                    return inst;

                inst = await GetInternal(ct, args)
                    .NoSync();
                Volatile.Write(ref _instance, inst);
                return inst;
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
                    : await _asyncObjectFunc(objects)
                        .NoSync();

            case InitializationType.AsyncObjectToken:
                return _asyncObjectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : await _asyncObjectTokenFunc(cancellationToken, objects)
                        .NoSync();

            case InitializationType.Async:
                return _asyncFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : await _asyncFunc()
                        .NoSync();

            case InitializationType.SyncObject:
                return _objectFunc is null ? throw new NullReferenceException(Constants.InitializationFuncError) : _objectFunc(objects);

            case InitializationType.SyncObjectToken:
                return _objectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectTokenFunc(cancellationToken, objects);

            case InitializationType.Sync:
                return _func is null ? throw new NullReferenceException(Constants.InitializationFuncError) : _func();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public T GetSync(params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        T? inst = Volatile.Read(ref _instance);
        if (inst is not null)
            return inst;

        using (_lock.Lock())
        {
            inst = Volatile.Read(ref _instance);
            if (inst is not null)
                return inst;

            inst = GetSyncInternal(objects);
            Volatile.Write(ref _instance, inst);
            return inst;
        }
    }

    private T GetSyncInternal(params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                return _asyncObjectFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncObjectFunc(objects)
                        .AwaitSync();

            case InitializationType.AsyncObjectToken:
                return _asyncObjectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncObjectTokenFunc(CancellationToken.None, objects)
                        .AwaitSync();

            case InitializationType.Async:
                return _asyncFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _asyncFunc()
                        .AwaitSync();

            case InitializationType.SyncObject:
                return _objectFunc is null ? throw new NullReferenceException(Constants.InitializationFuncError) : _objectFunc(objects);

            case InitializationType.SyncObjectToken:
                return _objectTokenFunc is null
                    ? throw new NullReferenceException(Constants.InitializationFuncError)
                    : _objectTokenFunc(CancellationToken.None, objects);

            case InitializationType.Sync:
                return _func is null ? throw new NullReferenceException(Constants.InitializationFuncError) : _func();

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetInitialization(Func<object[], ValueTask<T>> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], ValueTask<T>> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public void SetInitialization(Func<ValueTask<T>> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public void SetInitialization(Func<object[], T> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], T> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public void SetInitialization(Func<T> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Sync;
        _func = func;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        T? localInstance = _instance;

        if (localInstance is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        // Handle IDisposable cleanup
        if (localInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }
        // Handle IAsyncDisposable in a synchronous context
        else if (localInstance is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync()
                           .AwaitSync();
        }

        // Clear the instance reference and suppress finalization
        _instance = null;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        T? localInstance = _instance;

        if (localInstance is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        // Handle IAsyncDisposable cleanup
        if (localInstance is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync()
                                 .NoSync();
        }
        // Handle IDisposable in an asynchronous context
        else if (localInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Clear the instance reference and suppress finalization
        _instance = null;
        GC.SuppressFinalize(this);
    }
}