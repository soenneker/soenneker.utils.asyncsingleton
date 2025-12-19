using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.AsyncSingleton.Abstract;

/// <summary>
/// Represents an async-safe, single-initialization singleton that accepts one argument.
/// The first successful initialization wins; subsequent calls reuse the instance.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
/// <typeparam name="T1">The argument type used for initialization.</typeparam>
public interface IAsyncSingleton<T, in T1> : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the singleton instance asynchronously, creating it if necessary.
    /// The first call initializes the instance using the provided argument.
    /// </summary>
    ValueTask<T> Get(T1 arg, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the singleton instance synchronously, creating it if necessary.
    /// The first call initializes the instance using the provided argument.
    /// </summary>
    T GetSync(T1 arg, CancellationToken cancellationToken = default);
}