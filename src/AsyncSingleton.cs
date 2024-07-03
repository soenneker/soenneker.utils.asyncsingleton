using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using Soenneker.Utils.AsyncSingleton.Enums;

namespace Soenneker.Utils.AsyncSingleton;

///<inheritdoc cref="IAsyncSingleton{T}"/>
public class AsyncSingleton<T> : IAsyncSingleton<T>
{
    private T? _instance;

    private readonly AsyncLock _lock;

    private Func<ValueTask<T>>? _asyncFunc;
    private Func<T>? _func;

    private Func<object[], ValueTask<T>>? _asyncObjectFunc;
    private Func<object[], T>? _objectFunc;

    private Func<CancellationToken, object[], ValueTask<T>>? _asyncObjectTokenFunc;
    private Func<CancellationToken, object[], T>? _objectTokenFunc;

    private bool _disposed;

    private InitializationType _initializationType;

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
    /// <param name="func"></param>
    public AsyncSingleton(Func<object[], ValueTask<T>> func) : this()
    {
        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
    /// <param name="func"></param>
    public AsyncSingleton(Func<CancellationToken, object[], ValueTask<T>> func) : this()
    {
        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
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

    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>
    /// If this is used, be sure to set the initialization func via SetInitialization(), or use another constructor.
    /// </summary>
    public AsyncSingleton()
    {
        _lock = new AsyncLock();
    }

    public ValueTask<T> Get(params object[] objects)
    {
        return Get(CancellationToken.None, objects);
    }

    public async ValueTask<T> Get(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance != null)
            return _instance;

        using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_instance != null)
                return _instance;

            return await GetInternal(cancellationToken, objects);
        }
    }

    private async ValueTask<T> GetInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return await _asyncObjectFunc(objects).NoSync();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return await _asyncObjectTokenFunc(cancellationToken, objects).NoSync();
            case InitializationType.Async:
                if (_asyncFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects != null && objects.Any())
                    throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                return await _asyncFunc().NoSync();
            case InitializationType.SyncObject:
                if (_objectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return _objectTokenFunc(cancellationToken, objects);
            case InitializationType.Sync:
                if (_func == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects != null && objects.Any())
                    throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public T GetSync(params object[] objects)
    {
        return GetSync(CancellationToken.None, objects);
    }

    public T GetSync(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance != null)
            return _instance;

        using (_lock.Lock(cancellationToken))
        {
            if (_instance != null)
                return _instance;

            return GetSyncInternal(cancellationToken, objects);
        }
    }

    private T GetSyncInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncObjectFunc(objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncObjectTokenFunc(cancellationToken, objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.Async:
                if (_asyncFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects != null && objects.Any())
                    throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncFunc().NoSync().GetAwaiter().GetResult();
            case InitializationType.SyncObject:
                if (_objectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects == null || objects.Length == 0)
                    throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                return _objectTokenFunc(cancellationToken, objects);
            case InitializationType.Sync:
                if (_func == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                if (objects != null && objects.Any())
                    throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetInitialization(Func<object[], ValueTask<T>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], ValueTask<T>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public void SetInitialization(Func<ValueTask<T>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public void SetInitialization(Func<object[], T> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], T> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public void SetInitialization(Func<T> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Sync;
        _func = func;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_instance == null)
            return;

        switch (_instance)
        {
            case IDisposable disposable:
                disposable.Dispose();
                break;
            case IAsyncDisposable asyncDisposable:
                // Kind of a weird situation - basically the instance is IAsyncDisposable but it's being disposed synchronously (which can happen).
                asyncDisposable.DisposeAsync().NoSync().GetAwaiter().GetResult();
                break;
        }

        _instance = default;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_instance == null)
            return;

        switch (_instance)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync().NoSync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }

        _instance = default;
        GC.SuppressFinalize(this);
    }
}