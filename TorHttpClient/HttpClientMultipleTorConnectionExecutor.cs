using System.Net;
using Knapcode.TorSharp;

namespace TorHttpClientExecutor
{
    public class HttpClientMultipleTorConnectionExecutor : IDisposable
    {
        protected HttpClientMultipleTorConnectionExecutor(bool autoLoad = true)
        {
            if (autoLoad)
            {
                Load().Wait();
            }
        }
        protected static HttpClientMultipleTorConnectionExecutor _instance;
        public static HttpClientMultipleTorConnectionExecutor Start(bool autoLoad = true)
        {
            if (_instance == null)
            {
                _instance = new HttpClientMultipleTorConnectionExecutor(autoLoad);
            }
            return _instance;
        }

        private static TorSharpSettings? _baseSettings;
        public async Task Load(TorSharpSettings? torSharpSettings = null, HttpClient? httpClient = null)
        {
            _baseSettings = torSharpSettings ?? new();

            // Erase the extracted tools dir
            if (Directory.Exists(_baseSettings.ExtractedToolsDirectory)) Directory.Delete(_baseSettings.ExtractedToolsDirectory, true);

            // Share the same downloaded tools with all instances.
            httpClient = httpClient ?? new();
            using (httpClient)
            {
                var fetcher = new TorSharpToolFetcher(_baseSettings, httpClient);
                await fetcher.FetchAsync();
            }
        }
        private static void RetrievePortsAvailable(out int socksPort, out int controlPort)
        {
            socksPort = _executionNumber;
            var isSocksPortAvailable = PortChecker.IsPortAvailable(socksPort);
            controlPort = _executionNumber + 1;
            var isControlPortAvailable = PortChecker.IsPortAvailable(controlPort);

            while (!(isSocksPortAvailable && isControlPortAvailable))
            {
                for (int i = 0; i < 2; i++)
                {
                    Interlocked.Increment(ref _executionNumber);
                }

                socksPort = _executionNumber;
                isSocksPortAvailable = PortChecker.IsPortAvailable(socksPort);
                controlPort = _executionNumber + 1;
                isControlPortAvailable = PortChecker.IsPortAvailable(controlPort);
            }

            Interlocked.Increment(ref _executionNumber);
            Interlocked.Increment(ref _executionNumber);
        }

        private static readonly object Locker = new();
        private static int _executionNumber = 10000;
        private bool disposedValue;
        private List<TorSharpProxy> TorSharpProxyList = new();
        private List<HttpClient> HttpClients = new();
        public async Task<T> Execute<T>(Func<HttpClient, Task<T>> action, TorSharpSettings? torSharpSettings = null)
        {
            _executionNumber = torSharpSettings?.TorSettings?.SocksPort ?? 10000;
            int socksPort = _executionNumber;
            int controlPort = socksPort + 1;

            lock (Locker)
            {
                RetrievePortsAvailable(out socksPort, out controlPort);

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
                    WaitForConnect = TimeSpan.FromSeconds(0),

                    // The ports should not overlap either.
                    TorSettings = { SocksPort = socksPort, ControlPort = controlPort },
                    PrivoxySettings = { Disable = true },
                };

                TorSharpProxy TorProxy = new TorSharpProxy(torSharpSettings);
                TorProxy.ConfigureAndStartAsync().Wait();
                TorSharpProxyList.Add(TorProxy);
            }

            var httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = new WebProxy(new Uri("socks5://localhost:" + torSharpSettings.TorSettings.SocksPort)),
            });
            HttpClients.Add(httpClient);
            var task = action.Invoke(httpClient);
            var response = await task;
            return response;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (var item in HttpClients)
                    {
                        item.Dispose();
                    }
                    foreach (var item in TorSharpProxyList)
                    {
                        item.Stop();
                        item.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~HttpClientMultipleTorConnectionExecutor()
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