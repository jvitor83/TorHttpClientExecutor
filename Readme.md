# TorHttpClientExecutor

Make requests using Tor network.


## Usage

### Simple:
```csharp
await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
{
    using (var executer = await TorHttpClientExecutor.LoadAndCreate())
    {
        var myIP = await executer.ExecuteAsync(async httpClient => await httpClient.GetStringAsync("https://api.ipify.org"));
        System.Console.WriteLine($"IP: {myIP}");
    }
});
```

### Retry
```csharp

// polly - retry policy (optional, but to make sure to all requests works)
var pollyPolicy = HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(6, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

// make 3 request in parallel
await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
{
    // create the executor
    using (var executor = await TorHttpClientExecutor.LoadAndCreate())
    {
        // use the policy to retry
        var response = await pollyPolicy.ExecuteAsync(async () =>
        {
            // execute using the httpClient from the lambda parameter
            var response = await executor.ExecuteAsync(async httpClient => await httpClient.GetAsync("https://api.ipify.org"));
            return response;
        });
    }
    // read the content as string
    var responseString = await response.Content.ReadAsStringAsync();
    // each ip is different here
    System.Console.WriteLine($"IP: {responseString}");
});

```

> Note: You might consider switching the polly by the executor if want the retry to use a different IP.
