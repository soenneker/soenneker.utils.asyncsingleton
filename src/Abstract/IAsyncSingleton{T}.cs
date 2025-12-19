using System;
using System.Threading;
using System.Threading.Tasks;

namespace Soenneker.Utils.AsyncSingleton.Abstract;

/// <summary>
/// Represents an async-safe singleton that initializes an instance of <typeparamref name="T"/> at most once.
/// The first successful initialization wins; subsequent calls return the cached instance.
/// </summary>
/// <typeparam name="T">The singleton instance type.</typeparam>
public interface IAsyncSingleton<T> : IDisposable, IAsyncDisposable
{
    /// <summary>
    /// Gets the singleton instance asynchronously, creating it if necessary.
    /// </summary>
    ValueTask<T> Get(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the singleton instance synchronously, creating it if necessary.
    /// </summary>
    T GetSync(CancellationToken cancellationToken = default);
}