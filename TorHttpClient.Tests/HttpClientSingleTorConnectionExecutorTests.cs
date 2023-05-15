using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TorHttpClientExecutor.Tests
{
    public class HttpClientSingleTorConnectionExecutorTests
    {
        [Fact]
        public async Task TorHttpClient_ShouldHaveSameIP_WhenRequestedMultipleTimesInSameInstance()
        {
            var ips = new List<string>();

            using (HttpClientSingleTorConnectionExecutor executor = new HttpClientSingleTorConnectionExecutor())
            {
                await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
                {
                    var myIP = await executor.Execute(async httpClient => await httpClient.GetStringAsync("https://api.ipify.org"));
                    ips.Add(myIP);
                });
            }

            Assert.All(ips, r => r.Equals(ips.Last()));
        }
    }
}