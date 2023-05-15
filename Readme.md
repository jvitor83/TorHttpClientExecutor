# TorHttpClientExecutor

Make requests using Tor network.


## Usage

```csharp
await Parallel.ForEachAsync(Enumerable.Range(1, 3), async (count, cancellationToken) =>
{
    using (var executer = await TorHttpClientExecutor.LoadAndCreate())
    {
        var myIP = await executer.Execute(async httpClient => await httpClient.GetStringAsync("https://api.ipify.org"));
        System.Console.WriteLine($"IP: {myIP}");
    }
});
```