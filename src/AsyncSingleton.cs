using System;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;

namespace Soenneker.Utils.AsyncSingleton;

///<inheritdoc cref="IAsyncSingleton{T}"/>
public class AsyncSingleton<T> : IAsyncSingleton<T>
{
    private T? _instance;

    private readonly AsyncLock _lock;

    private Func<ValueTask<T>>? _asyncInitializationFunc;
    private Func<T>? _initializationFunc;

    private bool _disposed;

    /// <summary>
    /// If an async initialization func is used, it's recommend that GetSync() not be used.
    /// </summary>
    /// <param name="asyncInitializationFunc"></param>
    public AsyncSingleton(Func<ValueTask<T>> asyncInitializationFunc) : this()
    {
        _asyncInitializationFunc = asyncInitializationFunc;
    }

    public AsyncSingleton(Func<T> initializationFunc) : this()
    {
        _initializationFunc = initializationFunc;
    }

    // ReSharper disable once MemberCanBePrivate.Global
    /// <summary>
    /// If this is used, be sure to set the initialization func, see <see cref="SetAsyncInitialization"/> or <see cref="SetInitialization"/> or use another constructor.
    /// </summary>
    public AsyncSingleton()
    {
        _lock = new AsyncLock();
    }

    public async ValueTask<T> Get()
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance != null)
            return _instance;

        using (await _lock.LockAsync().ConfigureAwait(false))
        {
            if (_instance != null)
                return _instance;

            T tempInstance;

            if (_asyncInitializationFunc != null)
            {
                tempInstance = await _asyncInitializationFunc().NoSync();
            }
            else if (_initializationFunc != null)
            {
                tempInstance = _initializationFunc();
            }
            else
            {
                throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");
            }

            _instance = tempInstance;
        }

        return _instance;
    }

    public T GetSync()
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance != null)
            return _instance;

        using (_lock.Lock())
        {
            if (_instance != null)
                return _instance;

            T tempInstance;

            if (_initializationFunc != null)
            {
                tempInstance = _initializationFunc();
            }
            else if (_asyncInitializationFunc != null)
            {
                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                tempInstance = _asyncInitializationFunc().GetAwaiter().GetResult();
            }
            else
            {
                throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");
            }

            _instance = tempInstance;
        }

        return _instance;
    }

    public void SetAsyncInitialization(Func<ValueTask<T>> asyncInitializationFunc)
    {
        if (_instance != null)
            throw new Exception("Initializing an AsyncSingleton after it's already has been set is not allowed");

        _asyncInitializationFunc = asyncInitializationFunc;
    }

    public void SetInitialization(Func<T> initializationFunc)
    {
        if (_instance != null)
            throw new Exception("Initializing an AsyncSingleton after it's already has been set is not allowed");

        _initializationFunc = initializationFunc;
    }

    public void Dispose()
    {
        _disposed = true;

        GC.SuppressFinalize(this);

        if (_instance == null)
            return;

        switch (_instance)
        {
            case IDisposable disposable:
                disposable.Dispose();
                break;
            case IAsyncDisposable asyncDisposable:
                // Kind of a weird situation - basically the instance is IAsyncDisposable but it's being disposed synchronously (which can happen).
                asyncDisposable.DisposeAsync().GetAwaiter().GetResult();
                break;
        }

        _instance = default;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;

        GC.SuppressFinalize(this);

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
    }
}