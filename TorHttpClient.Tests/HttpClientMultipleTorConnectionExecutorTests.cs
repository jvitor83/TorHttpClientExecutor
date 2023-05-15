using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace TorHttpClientExecutor.Tests
{
    public class HttpClientMultipleTorConnectionExecutorTests
    {
        [Fact]
        public async Task TorHttpClient_ShouldNotHaveSameIP_WhenRequestedMultipleTimes()
        {
            var ips = new List<string>();

            using (var executer = HttpClientMultipleTorConnectionExecutor.Start())
            {
                await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
                {
                    var myIP = await executer.Execute(async httpClient => await httpClient.GetStringAsync("https://api.ipify.org"));
                    ips.Add(myIP);
                });
            }

            Assert.All(ips, s => Assert.Equal(1, ips.Count(x => x == s)));
        }
    }
}