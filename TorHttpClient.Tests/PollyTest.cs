using Microsoft.Extensions.DependencyInjection;
using Polly.Extensions.Http;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerClass, DisableTestParallelization = true)]


namespace TorHttpClientExecutor.Tests
{

    public class HttpClient_Polly_Test
    {
        const string TestClient = "TestClient";
        private bool _isRetryCalled;

        [Fact]
        public async Task Given_A_Retry_Policy_Has_Been_Registered_For_A_HttpClient_When_The_HttpRequest_Fails_Then_The_Request_Is_Retried()
        {
            var ips = new List<string>();

            // Arrange 
            IServiceCollection services = new ServiceCollection();
            _isRetryCalled = false;

            services.AddTransient<TorHttpClientExecutor>((s) => TorHttpClientExecutor.LoadAndCreate().Result);
            services.AddTransient<TorWebProxyHandlerExecutor>();

            services.AddHttpClient(TestClient)
                .AddPolicyHandler(GetRetryPolicy())
                .ConfigurePrimaryHttpMessageHandler<TorWebProxyHandlerExecutor>()
                .AddHttpMessageHandler(() => new StubDelegatingHandler())
                ;

            ServiceProvider serviceProvider = services.BuildServiceProvider();

            // Act
            await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
            {
                HttpClient configuredClient =
                serviceProvider
                    .GetRequiredService<IHttpClientFactory>()
                    .CreateClient(TestClient);

                HttpResponseMessage result = null;
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
                //result = await executer.Execute(async httpClient => await httpClient.SendAsync(request));
                result = await configuredClient.SendAsync(request);
                var ip = await result.Content.ReadAsStringAsync();
                ips.Add(ip);
            });
            //var result = await configuredClient.GetAsync("https://www.stackoverflow.com");


            // Assert
            Assert.True(_isRetryCalled);
            //Assert.All(ips, s => Assert.Equal(1, ips.Count(x => x == s)));
        }



        [Fact]
        public async Task Given_A_Retry_Policy_Has_Been_Registered_For_A_HttpClient_When_The_HttpRequest_Fails_Then_The_Request_Is_Retried_Different_IPs()
        {
            var ips = new List<string>();



            // Act
            await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
            {
                HttpResponseMessage result = null;
                var request = new HttpRequestMessage(HttpMethod.Get, "https://api.ipify.org");
                IAsyncPolicy<HttpResponseMessage> asyncPolicy = GetRetryPolicy();
                var a = await asyncPolicy.ExecuteAsync(async () =>
                {
                    using (var executer = await TorHttpClientExecutor.LoadAndCreate())
                    {
                        var result = await executer.ExecuteAsync(async httpClient => await httpClient.SendAsync(request));
                        return result;
                    }
                });
                var ip = await a.Content.ReadAsStringAsync();
                ips.Add(ip);
            });


            // Assert
            Assert.All(ips, s => Assert.Equal(1, ips.Count(x => x == s)));
        }


        public IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions.HandleTransientHttpError()
                .WaitAndRetryAsync(
                    6,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetryAsync: OnRetryAsync);
        }

        private async Task OnRetryAsync(DelegateResult<HttpResponseMessage> outcome, TimeSpan timespan, int retryCount, Context context)
        {
            //Log result
            _isRetryCalled = true;
        }
    }

    public class StubDelegatingHandler : DelegatingHandler
    {
        private int _count = 0;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_count == 0)
            {
                _count++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

}
