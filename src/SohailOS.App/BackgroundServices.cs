namespace SohailOS.App;

public sealed class InternetConnectivityService : IAsyncDisposable
{
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly Action<bool> _report;

    public InternetConnectivityService(HttpClient httpClient, Action<bool> report)
    {
        _httpClient = httpClient;
        _report = report;
        _loop = Task.Run(RunAsync);
    }

    private async Task RunAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            var online = false;
            try
            {
                using var response = await _httpClient.GetAsync(
                    "https://raw.githubusercontent.com/kaido6sanb6/SohailOS-Central/main/README.md",
                    HttpCompletionOption.ResponseHeadersRead,
                    _cts.Token);
                online = response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
            catch { }

            _report(online);
            try { await Task.Delay(TimeSpan.FromMinutes(5), _cts.Token); }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested) { }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        try { await _loop; } catch (OperationCanceledException) { }
        _cts.Dispose();
    }
}
