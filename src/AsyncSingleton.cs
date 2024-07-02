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

    private Func<ValueTask<T>>? _asyncInitializationFunc;
    private Func<T>? _initializationFunc;

    private Func<object[], ValueTask<T>>? _asyncObjectInitializationFunc;
    private Func<object[], T>? _objectInitializationFunc;

    private bool _disposed;

    private InitializationType _initializationType;

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
    /// <param name="asyncInitializationFunc"></param>
    public AsyncSingleton(Func<object[], ValueTask<T>> asyncInitializationFunc) : this()
    {
        _initializationType = InitializationType.AsyncObject;
        _asyncObjectInitializationFunc = asyncInitializationFunc;
    }

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
    public AsyncSingleton(Func<ValueTask<T>> asyncInitializationFunc) : this()
    {
        _initializationType = InitializationType.Async;
        _asyncInitializationFunc = asyncInitializationFunc;
    }

    public AsyncSingleton(Func<object[], T> func) : this()
    {
        _initializationType = InitializationType.SyncObject;
        _objectInitializationFunc = func;
    }

    public AsyncSingleton(Func<T> initializationFunc) : this()
    {
        _initializationType = InitializationType.Sync;
        _initializationFunc = initializationFunc;
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

            T tempInstance;

            switch (_initializationType)
            {
                case InitializationType.AsyncObject:
                    if (_asyncObjectInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects == null || objects.Length == 0)
                        throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                    tempInstance = await _asyncObjectInitializationFunc(objects).NoSync();
                    
                    break;
                case InitializationType.Async:
                    if (_asyncInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects != null && objects.Any())
                        throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                    tempInstance = await _asyncInitializationFunc().NoSync();
                    break;
                case InitializationType.SyncObject:
                    if (_objectInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects == null || objects.Length == 0)
                        throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                    tempInstance = _objectInitializationFunc(objects);
                    
                    break;

                case InitializationType.Sync:
                    if (_initializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects != null && objects.Any())
                        throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                    tempInstance = _initializationFunc();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _instance = tempInstance;
        }

        return _instance;
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

            T tempInstance;

            switch (_initializationType)
            {
                case InitializationType.AsyncObject:
                    if (_asyncObjectInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects == null || objects.Length == 0)
                        throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                    // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                    tempInstance = _asyncObjectInitializationFunc(objects).NoSync().GetAwaiter().GetResult();
                    break;
                case InitializationType.Async:
                    if (_asyncInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects != null && objects.Any())
                        throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                    // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                    tempInstance = _asyncInitializationFunc().NoSync().GetAwaiter().GetResult();
                    break;
                case InitializationType.SyncObject:
                    if (_objectInitializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects == null || objects.Length == 0)
                        throw new ArgumentException("Mismatched initialization: objects were not sent when retrieving the singleton when they are expected");

                    tempInstance = _objectInitializationFunc(objects);
                    break;
                case InitializationType.Sync:
                    if (_initializationFunc == null)
                        throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                    if (objects != null && objects.Any())
                        throw new ArgumentException("Mismatched initialization: objects were sent when retrieving the singleton when none are expected");

                    tempInstance = _initializationFunc();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _instance = tempInstance;
        }

        return _instance;
    }

    public void SetInitialization(Func<object[], ValueTask<T>> asyncObjectInitializationFunc)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectInitializationFunc = asyncObjectInitializationFunc;
    }

    public void SetInitialization(Func<ValueTask<T>> asyncInitializationFunc)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncInitializationFunc = asyncInitializationFunc;
    }

    public void SetInitialization(Func<T> initializationFunc)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Sync;
        _initializationFunc = initializationFunc;
    }

    public void SetInitialization(Func<object[], T> objectInitializationFunc)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectInitializationFunc = objectInitializationFunc;
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