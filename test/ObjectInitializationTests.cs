using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Soenneker.Utils.AsyncSingleton.Tests;

public class ObjectInitializationTests
{
    [Fact]
    public async Task Get_should_return_instance()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(objects =>
        {
            var cancellationToken = (CancellationToken) objects[0];

            return new HttpClient();
        });

        var cancellationToken = new CancellationToken();
        HttpClient result = await httpClientSingleton.Get(cancellationToken, cancellationToken);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Get_should_throw_when_objects()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(() => { return new HttpClient(); });

        var cancellationToken = new CancellationToken();

        Func<Task> act = async () =>
        {
            HttpClient result = await httpClientSingleton.Get(cancellationToken, cancellationToken);
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Get_should_throw_when_no_objects()
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(objects => { return new HttpClient(); });

        Func<Task> act = async () =>
        {
            HttpClient result = await httpClientSingleton.Get();
        };

        await act.Should().ThrowAsync<ArgumentException>();
    }
}