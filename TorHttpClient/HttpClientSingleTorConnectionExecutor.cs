using System.Net.Sockets;
using System.Net;
using Knapcode.TorSharp;
using System.Net.Http;

namespace TorHttpClientExecutor
{
    public class HttpClientSingleTorConnectionExecutor : IDisposable
    {
        protected WebProxy? Proxy;
        protected TorSharpProxy? TorProxy;
        private bool disposedValue;

        public HttpClientSingleTorConnectionExecutor(bool autoLoad = true)
        {
            if (autoLoad)
            {
                Load().Wait();
            }
        }

        public async Task Load(TorSharpSettings? torSharpSettings = null, HttpClient? httpClient = null)
        {
            if (Proxy is not null)
            {
                throw new NotSupportedException($"{nameof(Load)} already executed! If intended to use multiple times, use the `{nameof(HttpClientMultipleTorConnectionExecutor)}`");
            }

            int socksPort = torSharpSettings?.TorSettings?.SocksPort ?? 10000;
            int controlPort = socksPort + 1;
            string name = socksPort.ToString();

            torSharpSettings ??= new TorSharpSettings
            {
                // The extracted tools directory must not be shared.
                ExtractedToolsDirectory = Path.Combine((torSharpSettings ?? new()).ExtractedToolsDirectory, name),

                // The zipped tools directory can be shared, as long as the tool fetcher does not run in parallel.
                ZippedToolsDirectory = (torSharpSettings ?? new()).ZippedToolsDirectory,

                // https://github.com/joelverhagen/TorSharp/issues/100
                ToolRunnerType = ToolRunnerType.Simple,

                // TODO: change to be configurable
                WaitForConnect = TimeSpan.FromSeconds(8),

                // The ports should not overlap either.
                TorSettings = { SocksPort = socksPort, ControlPort = controlPort },
                PrivoxySettings = { Disable = true },
            };

            //// Erase the extracted tools dir
            //if (Directory.Exists(_baseSettings.ExtractedToolsDirectory)) Directory.Delete(_baseSettings.ExtractedToolsDirectory, true);

            // Share the same downloaded tools with all instances.
            httpClient ??= new();
            using (httpClient)
            {
                var fetcher = new TorSharpToolFetcher(torSharpSettings, httpClient);
                await fetcher.FetchAsync();
            }

            TorProxy = new TorSharpProxy(torSharpSettings);
            await TorProxy.ConfigureAndStartAsync();
            Proxy = new WebProxy(new Uri("socks5://localhost:" + torSharpSettings.TorSettings.SocksPort));
        }

        protected HttpClient? HttpClient;
        public async Task<T> Execute<T>(Func<HttpClient, Task<T>> action)
        {
            if (Proxy is null)
            {
                throw new NotSupportedException($"Have to Load by using the method `{nameof(Load)}`.");
            }

            HttpClient ??= new HttpClient(new HttpClientHandler
            {
                Proxy = Proxy,
            });
            var task = action.Invoke(HttpClient);
            var response = await task;
            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    HttpClient?.Dispose();
                    TorProxy!.Stop();
                    TorProxy!.Dispose();
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~HttpClientSingleTorConnectionExecutor()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}