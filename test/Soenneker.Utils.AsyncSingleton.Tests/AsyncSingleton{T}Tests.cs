using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;

namespace Soenneker.Utils.AsyncSingleton.Tests;

public class AsyncSingletonTTests
{
    [Test]
    public async Task Get_should_return_instance(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());
        HttpClient result = await httpClientSingleton.Get(cancellationToken: cancellationToken);
        result.Should()
              .NotBeNull();
    }

    [Test]
    public async Task Get_async_should_return_instance(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(async () =>
        {
            await Task.Delay(500);
            return new HttpClient();
        });

        HttpClient result = await httpClientSingleton.Get(cancellationToken: cancellationToken);
        result.Should()
              .NotBeNull();
    }

    [Test]
    public async Task Get_in_parallel_should_return_both_instances(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());

        HttpClient? client1 = null;
        HttpClient? client2 = null;

        HttpClient result = await httpClientSingleton.Get(cancellationToken: cancellationToken);
        result.Should()
              .NotBeNull();

        Task t1 = Task.Run(async () => client1 = await httpClientSingleton.Get(cancellationToken: cancellationToken));
        Task t2 = Task.Run(async () => client2 = await httpClientSingleton.Get(cancellationToken: cancellationToken));

        await Task.WhenAll(t1, t2);

        client1.Should()
               .NotBeNull();
        client2.Should()
               .NotBeNull();
    }

    [Test]
    public async Task Get_DisposeAsync_should_throw_after_disposing(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());

        _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        await httpClientSingleton.DisposeAsync();

        Func<Task> act = async () => _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        await act.Should()
                 .ThrowAsync<ObjectDisposedException>();
    }

    [Test]
    public async Task GetSync_Dispose_should_throw_after_disposing(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => new HttpClient());

        _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();

        Action act = () => _ = httpClientSingleton.GetSync(cancellationToken: cancellationToken);

        act.Should()
           .Throw<ObjectDisposedException>();
    }

    [Test]
    public async Task Dispose_with_nondisposable_should_not_throw(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<object>(() => new object());

        _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        // ReSharper disable once MethodHasAsyncOverload
        httpClientSingleton.Dispose();
    }

    [Test]
    public async Task DisposeAsync_with_nondisposable_should_not_throw(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<object>(() => new object());

        _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        await httpClientSingleton.DisposeAsync();
    }

    [Test]
    public async Task DisposeAsync_with_cancellationToken_with_nondisposable_should_not_throw(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<object>(() => new object());

        _ = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        await httpClientSingleton.DisposeAsync();
    }

    [Test]
    public async Task Async_with_object_and_cancellationToken_should_not_throw(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<object, int>(async (token, _) =>
        {
            await Task.Delay(100, token);
            return new object();
        });

        _ = await httpClientSingleton.Get(3, cancellationToken);
    }

    [Test]
    public void Sync_with_object_and_cancellationToken_should_not_throw()
    {
        var httpClientSingleton = new AsyncSingleton<object, int>(_ => new object());

        object httpClient = httpClientSingleton.GetSync(3, CancellationToken.None);
    }

    [Test]
    public async Task Async_Get_should_only_initialize_once(CancellationToken cancellationToken)
    {
        var x = 0;

        var httpClientSingleton = new AsyncSingleton<HttpClient>(async () =>
        {
            await Task.Delay(100);
            x++;
            return new HttpClient();
        });

        HttpClient result = await httpClientSingleton.Get(cancellationToken: cancellationToken);
        result = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        x.Should()
         .Be(1);
    }

    [Test]
    public async Task Sync_Get_Async_should_only_initialize_once(CancellationToken cancellationToken)
    {
        var x = 0;

        var httpClientSingleton = new AsyncSingleton<HttpClient>(() =>
        {
            x++;
            return new HttpClient();
        });

        HttpClient result = await httpClientSingleton.Get(cancellationToken: cancellationToken);
        result = await httpClientSingleton.Get(cancellationToken: cancellationToken);

        x.Should()
         .Be(1);
    }

    [Test]
    public void Sync_Get_Sync_should_only_initialize_once()
    {
        var x = 0;

        var httpClientSingleton = new AsyncSingleton<HttpClient>(() =>
        {
            x++;
            return new HttpClient();
        });

        HttpClient result = httpClientSingleton.GetSync();
        result = httpClientSingleton.GetSync();

        x.Should()
         .Be(1);
    }

    [Test]
    public async Task Value_type_should_be_cached_without_changing_its_value(CancellationToken cancellationToken)
    {
        var calls = 0;
        var singleton = new AsyncSingleton<int>(() => ++calls);

        int first = await singleton.Get(cancellationToken: cancellationToken);
        int second = await singleton.Get(cancellationToken: cancellationToken);

        first.Should().Be(1);
        second.Should().Be(1);
        calls.Should().Be(1);
    }
}
