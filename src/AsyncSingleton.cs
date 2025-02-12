using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using Soenneker.Utils.AsyncSingleton.Enums;

namespace Soenneker.Utils.AsyncSingleton;

///<inheritdoc cref="IAsyncSingleton"/>
public class AsyncSingleton : IAsyncSingleton
{
    private object? _instance;
    private readonly AsyncLock _lock = new();

    private Func<ValueTask<object>>? _asyncFunc;
    private Func<object>? _func;

    private Func<object[], ValueTask<object>>? _asyncObjectFunc;
    private Func<object[], object>? _objectFunc;

    private Func<CancellationToken, object[], ValueTask<object>>? _asyncObjectTokenFunc;
    private Func<CancellationToken, object[], object>? _objectTokenFunc;

    private bool _disposed;
    private InitializationType _initializationType;

    public AsyncSingleton(Func<object[], ValueTask<object>> func) : this()
    {
        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public AsyncSingleton(Func<CancellationToken, object[], ValueTask<object>> func) : this()
    {
        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public AsyncSingleton(Func<ValueTask<object>> func) : this()
    {
        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public AsyncSingleton(Func<CancellationToken, object[], object> func) : this()
    {
        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public AsyncSingleton(Func<object[], object> func) : this()
    {
        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public AsyncSingleton(Func<object> func) : this()
    {
        _initializationType = InitializationType.Sync;
        _func = func;
    }

    public AsyncSingleton()
    {
    }

    public ValueTask Init(params object[] objects)
    {
        return Init(CancellationToken.None, objects);
    }

    public async ValueTask Init(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncSingleton));

        if (_instance is not null)
            return;

        using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_instance is not null)
                return;

            _instance = await InitInternal(cancellationToken, objects).NoSync();
        }
    }

    private async ValueTask<object> InitInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return await _asyncObjectFunc(objects).NoSync();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return await _asyncObjectTokenFunc(cancellationToken, objects).NoSync();
            case InitializationType.Async:
                if (_asyncFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return await _asyncFunc().NoSync();
            case InitializationType.SyncObject:
                if (_objectFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _objectTokenFunc(cancellationToken, objects);
            case InitializationType.Sync:
                if (_func is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void InitSync(params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncSingleton));

        if (_instance is not null)
            return;

        using (_lock.Lock())
        {
            if (_instance is not null)
                return;

            _instance = InitSyncInternal(objects);
        }
    }

    private object InitSyncInternal(params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _asyncObjectFunc(objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _asyncObjectTokenFunc(CancellationToken.None, objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.Async:
                if (_asyncFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _asyncFunc().NoSync().GetAwaiter().GetResult();
            case InitializationType.SyncObject:
                if (_objectFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);    

                return _objectTokenFunc(CancellationToken.None, objects);
            case InitializationType.Sync:
                if (_func is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetInitialization(Func<object[], ValueTask<object>> func)
    {   
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], ValueTask<object>> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public void SetInitialization(Func<ValueTask<object>> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public void SetInitialization(Func<object[], object> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], object> func)
    {
        if (_instance is not null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public void SetInitialization(Func<object> func)
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

        // Capture the instance into a local variable to avoid multiple volatile field accesses.
        object? localInstance = _instance;

        if (localInstance is not null)
        {
            // Explicit conditional checks for better predictability in JIT compilation.
            if (localInstance is IDisposable disposable)
            {
                disposable.Dispose();
            }
            else if (localInstance is IAsyncDisposable asyncDisposable)
            {
                // Handle async disposal in a synchronous context.
                asyncDisposable.DisposeAsync().NoSync().GetAwaiter().GetResult();
            }

            // Clear the instance explicitly to allow for garbage collection.
            _instance = null;
        }

        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        object? localInstance = _instance;

        if (localInstance is null)
        {
            GC.SuppressFinalize(this);
            return;
        }

        // Check for IAsyncDisposable first to avoid unnecessary interface casting
        if (localInstance is IAsyncDisposable asyncDisposable)
        {
            // Await using ConfigureAwait(false) to minimize synchronization context capture
            await asyncDisposable.DisposeAsync().NoSync();
        }
        else if (localInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Clear the instance reference explicitly to assist GC
        _instance = null;

        GC.SuppressFinalize(this);
    }
}