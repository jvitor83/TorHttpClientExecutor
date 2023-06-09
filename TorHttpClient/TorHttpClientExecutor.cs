using System.Net;
using Knapcode.TorSharp;
using System.Net.Http;

namespace TorHttpClientExecutor
{
    public class TorWebProxyHandlerExecutor : HttpClientHandler, IDisposable
    {
        public TorWebProxyHandlerExecutor(TorHttpClientExecutor executor)
        {
            this.Executor = executor;
            this.Proxy = new WebProxy(new Uri("socks5://localhost:" + this.Executor.TorSettings.TorSettings.SocksPort));
        }
        public TorHttpClientExecutor Executor { get; }

        private bool disposedValue;

        protected new virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    Executor.Dispose();
                    base.Dispose(disposing);
                }

                disposedValue = true;
            }
        }
        public new virtual void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
    //public class TorWebProxyHandler : HttpClientHandler
    //{
    //    public TorWebProxyHandler(int TorSocksPort)
    //    {
    //        this.TorSocksPort = TorSocksPort;
    //        this.Proxy = new WebProxy(new Uri("socks5://localhost:" + TorSocksPort));
    //    }
    //    public int TorSocksPort { get; }
    //}
    public class TorHttpClientExecutor : IDisposable
    {
        private TorHttpClientExecutor(TorSharpProxy torProxy, TorSharpSettings torSettings)
        {
            this.torProxy = torProxy ?? throw new ArgumentNullException(nameof(torProxy));
            this.torSettings = torSettings ?? throw new ArgumentNullException(nameof(torSettings));
        }
        public async Task<T> ExecuteAsync<T>(Func<HttpClient, Task<T>> action)
        {
            using (TorWebProxyHandlerExecutor handler = new TorWebProxyHandlerExecutor(this))
            {
                using (var httpClient = HttpClientFactory.Create(handler))
                {
                    var task = action.Invoke(httpClient);
                    var response = await task;
                    return response;
                }
            }
        }

        public async Task ExecuteAsync(Func<HttpClient, Task> action)
        {
            using (TorWebProxyHandlerExecutor handler = new TorWebProxyHandlerExecutor(this))
            {
                using (var httpClient = HttpClientFactory.Create(handler))
                {
                    await action.Invoke(httpClient);
                }
            }
        }

        public T Execute<T>(Func<HttpClient, T> action)
        {
            using (TorWebProxyHandlerExecutor handler = new TorWebProxyHandlerExecutor(this))
            {
                using (var httpClient = HttpClientFactory.Create(handler))
                {
                    var task = action.Invoke(httpClient);
                    return task;
                }
            }
        }

        public void Execute(Action<HttpClient> action)
        {
            using (TorWebProxyHandlerExecutor handler = new TorWebProxyHandlerExecutor(this))
            {
                using (var httpClient = HttpClientFactory.Create(handler))
                {
                    action.Invoke(httpClient);
                }
            }
        }

        public TorSharpProxy TorProxy { get => torProxy; }
        public TorSharpSettings TorSettings { get => torSettings; }

        private bool disposedValue;
        private readonly TorSharpProxy torProxy;
        private readonly TorSharpSettings torSettings;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    torProxy.Stop();
                    //torProxy.Stop();
                    torProxy.Dispose();
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

        public static async Task<TorHttpClientExecutor> LoadAndCreate(TorSharpSettings? torSharpSettings = null, HttpClient? httpClient = null)
        {
            bool log = torSharpSettings == null ? false : torSharpSettings.WriteToConsole;
            _executionNumber = torSharpSettings?.TorSettings?.SocksPort ?? 10000;
            int socksPort = _executionNumber;
            int controlPort = socksPort + 1;

            torSharpSettings ??= new();

            if (!Directory.Exists(torSharpSettings.ZippedToolsDirectory))
            {
                // Share the same downloaded tools with all instances.
                httpClient = httpClient ?? new();
                using (httpClient)
                {
                    var fetcher = new TorSharpToolFetcher(torSharpSettings, httpClient);
                    await fetcher.FetchAsync();
                }
            }

            lock (Locker)
            {
                RetrievePortsAvailable(out socksPort, out controlPort);

                string name = socksPort.ToString();

                var torSettings = new TorSharpSettings
                {
                    // The extracted tools directory must not be shared.
                    ExtractedToolsDirectory = Path.Combine(torSharpSettings.ExtractedToolsDirectory, name),

                    // The zipped tools directory can be shared, as long as the tool fetcher does not run in parallel.
                    ZippedToolsDirectory = torSharpSettings.ZippedToolsDirectory,

                    // https://github.com/joelverhagen/TorSharp/issues/100
                    ToolRunnerType = ToolRunnerType.Simple,

                    // TODO: change to be configurable
                    WaitForConnect = TimeSpan.FromSeconds(0),

                    // The ports should not overlap either.
                    TorSettings = { SocksPort = socksPort, ControlPort = controlPort },
                    PrivoxySettings = { Disable = true },

                    // write to console
                    WriteToConsole = log,
                };

                TorSharpProxy TorProxy = new TorSharpProxy(torSettings);
                TorProxy.ConfigureAndStartAsync().Wait();
                return new TorHttpClientExecutor(TorProxy, torSettings);
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
    }
}