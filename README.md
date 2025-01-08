[![](https://img.shields.io/nuget/v/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.asyncsingleton/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.asyncsingleton/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)

# ![](https://user-images.githubusercontent.com/4441470/224455560-91ed3ee7-f510-4041-a8d2-3fc093025112.png) Soenneker.Utils.AsyncSingleton

`AsyncSingleton` is a lightweight utility that provides lazy (and optionally asynchronous) initialization of an instance. It ensures that the instance is only created once, even in highly concurrent scenarios. It also offers both synchronous and asynchronous initialization methods while supporting a variety of initialization signatures. Additionally, `AsyncSingleton` implements both synchronous and asynchronous disposal.

## Features

- **Lazy Initialization**: The instance is created only upon the first call of `Get()`, `GetAsync()`, `Init()` or `InitSync()`.
- **Thread-safe**: Uses asynchronous locking for coordinated initialization in concurrent environments.
- **Multiple Initialization Patterns**:
  - Sync and async initialization
  - With or without parameters (`params object[]`)
  - With or without `CancellationToken`
- **Re-initialization Guard**: Once the singleton is initialized (or has begun initializing), further initialization reconfigurations are disallowed.

## Installation

```
dotnet add package Soenneker.Utils.AsyncSingleton
```

There are two different types: `AsyncSingleton`, and `AsyncSingleton<T>`:

### `AsyncSingleton<T>`
Useful in scenarios where you need a result of the initialization. `Get()` is the primary method.

```csharp
using Microsoft.Extensions.Logging;

public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly AsyncSingleton<HttpClient> _asyncSingleton;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;

        _asyncSingleton = new AsyncSingleton(async () =>
        {
            _logger.LogInformation("Initializing the singleton resource synchronously...");
            await Task.Delay(1000);

            return new HttpClient();
        });
    }

    public async ValueTask StartWork()
    {
        var httpClient = await _asyncSingleton.Get();

        // At this point the task has been run, guaranteed only once (no matter if this is called concurrently)

        var sameHttpClient = await _asyncSingleton.Get(); // This is the same instance of the httpClient above
    }
}
```

### `AsyncSingleton`
Useful in scenarios where you just need async single initialization, and you don't ever need to leverage an instance.  `Init()` is the primary method.

```csharp
using Microsoft.Extensions.Logging;

public class MyService
{
    private readonly ILogger<MyService> _logger;
    private readonly AsyncSingleton _singleExecution;

    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;

        _singleExecution = new AsyncSingleton(async () =>
        {
            _logger.LogInformation("Initializing the singleton resource ...");
            await Task.Delay(1000); // Simulates an async call

            return new object(); // This object is needed for AsyncSingleton to recognize that initialization has occurred
        });
    }

    public async ValueTask StartWork()
    {
        await _singleExecution.Init();

        // At this point the task has been run, guaranteed only once (no matter if this is called concurrently)

        await _singleExecution.Init(); // This will NOT execute the task, since it's already been called
    }
}
```

Tips:
- If you need to cancel the initialization, pass a `CancellationToken` to the `Init()`, and `Get()` method. This will cancel any locking occurring during initialization.
- If you use a type of `AsyncSingleton` that implements `IDisposable` or `IAsyncDisposable`, be sure to dispose of the `AsyncSingleton` instance. This will dispose the underlying instance.
- Be careful about updating the underlying instance directly, as `AsyncSingleton` holds a reference to it, and will return those changes to further callers.
- `SetInitialization()` can be used to set the initialization function after the `AsyncSingleton` has been created. This can be useful in scenarios where the initialization function is not known at the time of creation.
- Try not to use an asynchronous initialization method, and then retrieve it synchronously. If you do so, `AsyncSingleton` will block to maintain thread-safety.
- Using a synchronous initialization method with asynchronous retrieval will not block, and will still provide thread-safety.
- Similarly, if the underlying instance is `IAsyncDisposable`, try to leverage `AsyncSingleton.DisposeAsync()`. Using `AsyncSingleton.DisposeAsync()` with an `IDisposable` underlying instance is fine.