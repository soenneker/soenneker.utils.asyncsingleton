﻿using System;
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
    private readonly AsyncLock _lock;

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
        _lock = new AsyncLock();
    }

    public async ValueTask Init(params object[] objects)
    {
        await Init(CancellationToken.None, objects);
    }

    public async ValueTask Init(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncSingleton));

        if (_instance != null)
            return;

        using (await _lock.LockAsync(cancellationToken).ConfigureAwait(false))
        {
            if (_instance != null)
                return;

            _instance = await InitInternal(cancellationToken, objects);
        }
    }

    private async ValueTask<object> InitInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return await _asyncObjectFunc(objects).NoSync();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return await _asyncObjectTokenFunc(cancellationToken, objects).NoSync();
            case InitializationType.Async:
                if (_asyncFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return await _asyncFunc().NoSync();
            case InitializationType.SyncObject:
                if (_objectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _objectTokenFunc(cancellationToken, objects);
            case InitializationType.Sync:
                if (_func == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void InitSync(params object[] objects)
    {
        InitSync(CancellationToken.None, objects);
    }

    public void InitSync(CancellationToken cancellationToken, params object[] objects)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(AsyncSingleton));

        if (_instance != null)
            return;

        using (_lock.Lock(cancellationToken))
        {
            if (_instance != null)
                return;

            _instance = InitSyncInternal(cancellationToken, objects);
        }
    }

    private object InitSyncInternal(CancellationToken cancellationToken, params object[] objects)
    {
        switch (_initializationType)
        {
            case InitializationType.AsyncObject:
                if (_asyncObjectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _asyncObjectFunc(objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.AsyncObjectToken:
                if (_asyncObjectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _asyncObjectTokenFunc(cancellationToken, objects).NoSync().GetAwaiter().GetResult();
            case InitializationType.Async:
                if (_asyncFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _asyncFunc().NoSync().GetAwaiter().GetResult();
            case InitializationType.SyncObject:
                if (_objectFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _objectFunc(objects);
            case InitializationType.SyncObjectToken:
                if (_objectTokenFunc == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _objectTokenFunc(cancellationToken, objects);
            case InitializationType.Sync:
                if (_func == null)
                    throw new NullReferenceException("Initialization func for AsyncSingleton cannot be null");

                return _func();
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public void SetInitialization(Func<object[], ValueTask<object>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObject;
        _asyncObjectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], ValueTask<object>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.AsyncObjectToken;
        _asyncObjectTokenFunc = func;
    }

    public void SetInitialization(Func<ValueTask<object>> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.Async;
        _asyncFunc = func;
    }

    public void SetInitialization(Func<object[], object> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObject;
        _objectFunc = func;
    }

    public void SetInitialization(Func<CancellationToken, object[], object> func)
    {
        if (_instance != null)
            throw new Exception("Setting the initialization of an AsyncSingleton after it's already has been set is not allowed");

        _initializationType = InitializationType.SyncObjectToken;
        _objectTokenFunc = func;
    }

    public void SetInitialization(Func<object> func)
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