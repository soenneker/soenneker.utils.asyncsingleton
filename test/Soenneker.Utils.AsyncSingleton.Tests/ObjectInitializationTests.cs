﻿using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
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
}