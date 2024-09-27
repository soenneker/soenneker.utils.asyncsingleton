using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Soenneker.Utils.AsyncSingleton.Tests;

public class AsyncSingletonTests
{
    [Fact]
    public async Task Get_should_return_instance()
    {
        var httpClientSingleton = new AsyncSingleton(() => new HttpClient());
        await httpClientSingleton.Init();
    }

    [Fact]
    public async Task Get_async_should_return_instance()
    {
        var httpClientSingleton = new AsyncSingleton(async () =>
        {
            await Task.Delay(500);
            return new HttpClient();
        });

        await httpClientSingleton.Init();
    }

    [Fact]
    public async Task Get_in_parallel_should_return_both_instances()
    {
        var httpClientSingleton = new AsyncSingleton(() => new HttpClient());

        await httpClientSingleton.Init();

        Task t1 = Task.Run(async () => await httpClientSingleton.Init());
        Task t2 = Task.Run(async () => await httpClientSingleton.Init());

        await Task.WhenAll(t1, t2);
    }

    [Fact]
    public async Task Get_DisposeAsync_should_throw_after_disposing()
    {
        var httpClientSingleton = new AsyncSingleton(() => new HttpClient());

        await httpClientSingleton.Init();

        await httpClientSingleton.DisposeAsync();

        Func<Task> act = async () => await httpClientSingleton.Init();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetSync_Dispose_should_throw_after_disposing()
    {
        var httpClientSingleton = new AsyncSingleton(() => new HttpClient());

        await httpClientSingleton.Init();

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();

        Action act = () => httpClientSingleton.InitSync();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_with_nondisposable_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton(() => new object());

        await httpClientSingleton.Init();

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_with_nondisposable_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton(() => new object());

        await httpClientSingleton.Init();

        await httpClientSingleton.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_with_cancellationToken_with_nondisposable_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton(() => new object());

        await httpClientSingleton.Init();

        await httpClientSingleton.DisposeAsync();
    }

    [Fact]
    public async Task Async_with_object_and_cancellationToken_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton(async (token, obj) => new object());

        await httpClientSingleton.Init(CancellationToken.None, 3);
    }

    [Fact]
    public async Task Sync_with_object_and_cancellationToken_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton((token, obj) => new object());

        httpClientSingleton.InitSync(CancellationToken.None, 3);
    }

    [Fact]
    public async Task Async_Get_should_only_initialize_once()
    {
        int x = 0;

        var httpClientSingleton = new AsyncSingleton(async () =>
        {
            x++;
            return new HttpClient();
        });

        await httpClientSingleton.Init();

        x.Should().Be(1);
    }

    [Fact]
    public async Task Sync_Get_Async_should_only_initialize_once()
    {
        int x = 0;

        var httpClientSingleton = new AsyncSingleton(() =>
        {
            x++;
            return new HttpClient();
        });

        await httpClientSingleton.Init();
        await httpClientSingleton.Init();

        x.Should().Be(1);
    }

    [Fact]
    public void Sync_Get_Sync_should_only_initialize_once()
    {
        int x = 0;

        var httpClientSingleton = new AsyncSingleton(() =>
        {
            x++;
            return new HttpClient();
        });

        httpClientSingleton.InitSync();
        httpClientSingleton.InitSync();

        x.Should().Be(1);
    }
}