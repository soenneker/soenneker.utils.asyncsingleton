[![](https://img.shields.io/nuget/v/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.asyncsingleton/publish-package.yml?style=for-the-badge)](https://github.com/soenneker/soenneker.utils.asyncsingleton/actions/workflows/publish-package.yml)
[![](https://img.shields.io/nuget/dt/Soenneker.Utils.AsyncSingleton.svg?style=for-the-badge)](https://www.nuget.org/packages/Soenneker.Utils.AsyncSingleton/)
[![](https://img.shields.io/github/actions/workflow/status/soenneker/soenneker.utils.asyncsingleton/codeql.yml?label=CodeQL&style=for-the-badge)](https://github.com/soenneker/soenneker.utils.asyncsingleton/actions/workflows/codeql.yml)

# Soenneker.Utils.AsyncSingleton

Thread-safe, lazy initialization for a single shared value, with synchronous and asynchronous factories and retrieval.

## Installation

```bash
dotnet add package Soenneker.Utils.AsyncSingleton
```

## Usage

Create the singleton with a factory, then retain and dispose the wrapper for as long as the shared value should live:

```csharp
await using var client = new AsyncSingleton<HttpClient>(
    static () => new ValueTask<HttpClient>(new HttpClient()));

HttpClient first = await client.Get();
HttpClient sameInstance = await client.Get();
```

Concurrent callers share one successful factory result. If the factory throws or is cancelled, no value is cached and a later call can retry.

Use `AsyncSingleton<T, T1>` when initialization needs an argument:

```csharp
var client = new AsyncSingleton<HttpClient, Uri>(
    static baseAddress => new HttpClient { BaseAddress = baseAddress });

HttpClient value = await client.Get(new Uri("https://api.example.com"));
```

Only the argument supplied by the call that successfully initializes the value is used. Arguments from later calls are ignored because those calls receive the cached instance.

## Synchronous access

`GetSync()` is available when the caller must initialize synchronously. Prefer matching asynchronous factories with `Get()`; calling `GetSync()` for an asynchronous factory blocks until that factory completes.

## Lifetime and cancellation

- Disposing the wrapper also disposes the cached value when it implements `IDisposable` or `IAsyncDisposable`.
- After disposal, retrieval throws `ObjectDisposedException`.
- A cancellation token can cancel lock acquisition and is passed to factories that accept a token. It cannot cancel a factory overload that has no token parameter.
