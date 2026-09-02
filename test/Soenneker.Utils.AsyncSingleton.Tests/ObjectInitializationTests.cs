using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;

namespace Soenneker.Utils.AsyncSingleton.Tests;

public class ObjectInitializationTests
{
    [Test]
    public async Task Get_should_return_instance(CancellationToken cancellationToken)
    {
        var httpClientSingleton = new AsyncSingleton<HttpClient>(objects =>
        {
            return new HttpClient();
        });

        HttpClient result = await httpClientSingleton.Get(cancellationToken);
        result.Should().NotBeNull();
    }
}
