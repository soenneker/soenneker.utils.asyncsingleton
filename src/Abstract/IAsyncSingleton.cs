using System;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace Soenneker.Utils.AsyncSingleton.Abstract;

/// <summary>
/// An externally initializing singleton that uses double-check asynchronous locking, with optional async and sync disposal
/// </summary>
/// <remarks>Be sure to dispose of this gracefully if using a Disposable type</remarks>
public interface IAsyncSingleton<T> : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// This method should be called even if the initialization func was synchronous
    /// </summary>
    /// <remarks>The initialization func needs to be set before calling this, either in the ctor or via the other methods</remarks>
    /// <exception cref="ObjectDisposedException"></exception>
    /// <exception cref="NullReferenceException"></exception>
    [Pure]
    ValueTask<T> Get();

    /// <summary>
    /// <see cref="Get"/> should be used instead of this if possible. This method can block the calling thread
    /// </summary>
    /// <remarks>The initialization func needs to be set before calling this, either in the ctor or via the other methods</remarks>
    /// <exception cref="ObjectDisposedException"></exception>
    /// <exception cref="NullReferenceException"></exception>
    [Pure]
    T GetSync();

    /// <summary>
    /// Allows for setting the initialization code outside of the constructor
    /// </summary>
    void SetAsyncInitialization(Func<Task<T>> asyncInitializationFunc);

    /// <summary>
    /// Allows for setting the initialization code outside of the constructor
    /// </summary>
    void SetInitialization(Func<T> initializationFunc);
}