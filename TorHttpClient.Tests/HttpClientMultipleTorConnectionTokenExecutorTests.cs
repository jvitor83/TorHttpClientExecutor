using Microsoft.VisualBasic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.DataCollection;
using Polly;
using Polly.Extensions.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace TorHttpClientExecutor.Tests
{
    public class HttpClientMultipleTorConnectionTokenExecutorTests
    {
        [Fact]
        public async Task TorHttpClient_ShouldNotHaveSameIP_WhenRequestedMultipleTimesWithDifferentExecutors()
        {
            var ips = new List<string>();

            await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
            {
                using (var executer = await TorHttpClientExecutor.LoadAndCreate())
                {
                    var myIP = await executer.ExecuteAsync(async httpClient => await httpClient.GetStringAsync("https://api.ipify.org"));
                    ips.Add(myIP);
                }
            });

            Assert.All(ips, s => Assert.Equal(1, ips.Count(x => x == s)));
        }

        [Fact]
        public async Task TorHttpClient_ShouldNotHaveSameIP_WhenRequestedMultipleTimesWithDifferentExecutorsUsingPolly()
        {
            var ips = new List<string>();

            var pollyContext = new Context();
            var policy = HttpPolicyExtensions.HandleTransientHttpError().RetryAsync(4);
            var response = await policy.ExecuteAsync(async ctx =>
            {
                using (var executer = await TorHttpClientExecutor.LoadAndCreate())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
                    var response = await executer.ExecuteAsync(async httpClient => await httpClient.SendAsync(request));
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }, pollyContext);

            Assert.All(ips, s => Assert.Equal(1, ips.Count(x => x == s)));
        }

        [Fact]
        public async Task TorHttpClient_ShouldNotHaveSamePort_WhenUsingDifferentExecutors()
        {
            var executors = new List<TorHttpClientExecutor>();

            try
            {
                await Parallel.ForEachAsync(Enumerable.Range(1, 10), async (a, ct) =>
                {
                    executors.Add(await TorHttpClientExecutor.LoadAndCreate());
                });

                //await Task.Delay(TimeSpan.FromSeconds(3));

                //using (var executer1 = await TorHttpClientExecutor.LoadAndCreate())
                //{
                //    using (var executer2 = await TorHttpClientExecutor.LoadAndCreate())
                //    {
                //        await executer1.Execute(async httpClient => await Task.Delay(TimeSpan.FromSeconds(5)));

                //    }
                //}

                Assert.All(executors, s => Assert.Equal(1, executors.Count(x => x.TorSettings.TorSettings.SocksPort == s.TorSettings.TorSettings.SocksPort)));

            }
            finally
            {
                IEnumerable<TorHttpClientExecutor> items = executors.Except(new[] { executors.Last() });
                foreach (var item in items)
                {
                    item.Dispose();
                }
            }

            using (var executor = executors.Last())
            {
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
                var response = await executor.ExecuteAsync(async httpClient => await httpClient.SendAsync(request));
                response.EnsureSuccessStatusCode();
            }
        }
    }
}