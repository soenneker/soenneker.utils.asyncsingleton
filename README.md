[![](https://img.shields.io/nuget/v/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.asyncsingleton/publish.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.asyncsingleton/actions/workflows/publish.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.AsyncSingleton
### An externally initializing singleton that uses double-check asynchronous locking, with optional async and sync disposal

## Installation

```
Install-Package Soenneker.Utils.AsyncSingleton
```

## Example

The example below is a long-living `HttpClient` implementation using `AsyncSingleton`. It avoids the additional overhead of `IHttpClientFactory`, and doesn't rely on short-lived clients.

```csharp
public class HttpRequester : IDisposable, IAsyncDisposable
{
    private readonly AsyncSingleton<HttpClient> _client;

    public HttpRequester()
    {
        // This func will lazily execute once it's retrieved the first time.
        // Other threads calling this at the same moment will asynchronously wait,
        // and then utilize the HttpClient that was created from the first caller.
        _client = new AsyncSingleton<HttpClient>(() =>
        {
            var socketsHandler = new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                MaxConnectionsPerServer = 10
            };

            return new HttpClient(socketsHandler);
        });
    }

    public async ValueTask Get()
    {
        // retrieve the singleton async, thus not blocking the calling thread
        await (await _client.Get()).GetAsync("https://google.com");
    }

    // Disposal is not necessary for AsyncSingleton unless the type used is IDisposable/IAsyncDisposable
    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);

        return _client.DisposeAsync();
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        
        _client.Dispose();
    }
}
```