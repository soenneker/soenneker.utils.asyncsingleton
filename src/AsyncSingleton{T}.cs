﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using Soenneker.Extensions.ValueTask;
using Soenneker.Utils.AsyncSingleton.Abstract;
using Soenneker.Utils.AsyncSingleton.Enums;

namespace Soenneker.Utils.AsyncSingleton;

///<inheritdoc cref="IAsyncSingleton{T}"/>
// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AsyncSingleton<T> : IAsyncSingleton<T>
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
    }

    public ValueTask<T> Get(params object[] objects)
    {
        return Get(CancellationToken.None, objects);
    }

    public virtual async ValueTask<T> Get(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance is not null)
            return _instance;

        using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_instance is not null)
                return _instance;

            _instance = await GetInternal(cancellationToken, objects).NoSync();
            return _instance;
        }
    }

    private async ValueTask<T> GetInternal(CancellationToken cancellationToken, params object[] objects)
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

    public T GetSync(params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(typeof(AsyncSingleton<T>).Name);

        if (_instance is not null)
            return _instance;

        using (_lock.Lock())
        {
            if (_instance is not null)
                return _instance;

            _instance = GetSyncInternal(objects);
            return _instance;
        }
    }

    private T GetSyncInternal(params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncObjectFunc(objects).AwaitSync();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncObjectTokenFunc(CancellationToken.None, objects).AwaitSync();
            case InitializationType.Async:
                if (_asyncFunc is null)
                    throw new NullReferenceException(Constants.InitializationFuncError);

                // Not a great situation here - we only have async initialization but we're calling this synchronously... so we'll block
                return _asyncFunc().AwaitSync();
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
            asyncDisposable.DisposeAsync().AwaitSync();
        }

        // Clear the instance reference and suppress finalization
        _instance = default;
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
            await asyncDisposable.DisposeAsync().NoSync();
        }
        // Handle IDisposable in an asynchronous context
        else if (localInstance is IDisposable disposable)
        {
            disposable.Dispose();
        }

        // Clear the instance reference and suppress finalization
        _instance = default;
        GC.SuppressFinalize(this);
    }
}