using System;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Soenneker.Utils.AsyncSingleton.Tests;

public class AsyncSingletonTests
{
    [Fact]
    public async Task Get_should_return_instance()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());
        HttpClient result = await httpClientSingleton.Get();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_async_should_return_instance()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(async () =>
        {
            await Task.Delay(500);
            return new HttpClient();
        });

        HttpClient result = await httpClientSingleton.Get();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_in_parallel_should_return_both_instances()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());
        
        HttpClient? client1 = null;
        HttpClient? client2 = null;

        HttpClient result = await httpClientSingleton.Get();
        result.Should().NotBeNull();

        Task t1 = Task.Run(async () => client1 = await httpClientSingleton.Get());
        Task t2 = Task.Run(async () => client2 = await httpClientSingleton.Get());

        await Task.WhenAll(t1, t2);

        client1.Should().NotBeNull();
        client2.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_DisposeAsync_should_throw_after_disposing()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());

        _ = await httpClientSingleton.Get();

        await httpClientSingleton.DisposeAsync();

        Func<Task> act = async () => _ = await httpClientSingleton.Get();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task GetSync_Dispose_should_throw_after_disposing()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());

        _ = await httpClientSingleton.Get();

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();

        Action act = () => _ = httpClientSingleton.GetSync();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Dispose_with_nondisposable_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton<object>(() => new object());

        _ = await httpClientSingleton.Get();

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();
    }

    [Fact]
    public async Task DisposeAsync_with_nondisposable_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton<object>(() => new object());

        _ = await httpClientSingleton.Get();

        await httpClientSingleton.DisposeAsync();
    }
}