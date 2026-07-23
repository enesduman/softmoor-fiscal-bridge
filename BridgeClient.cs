// Softmoor Fiscal Bridge — bulut API istemcisi
// Kimlik: x-restaurant-id + x-bridge-key header'ları (panelden üretilir)

using System.Net.Http.Json;
using System.Text.Json;

namespace SoftmoorFiscalBridge;

public sealed class BridgeClient
{
    private readonly HttpClient _http;

    public BridgeClient(BridgeConfig cfg)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(cfg.CloudBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(20),
        };
        // ÖNEMLİ: .NET HttpClient varsayılan User-Agent GÖNDERMEZ; backend'in
        // bot koruması UA'sız istekleri 404 "Not found" ile reddediyor.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SoftmoorFiscalBridge/1.0");
        _http.DefaultRequestHeaders.Add("x-restaurant-id", cfg.RestaurantId);
        _http.DefaultRequestHeaders.Add("x-bridge-key", cfg.BridgeKey);
    }

    /// <summary>Bekleyen işleri çek (aynı zamanda heartbeat görevi görür).</summary>
    public async Task<PollData?> PollAsync(CancellationToken ct)
    {
        using var res = await _http.PostAsJsonAsync("fiscal-bridge/poll", new { }, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"poll {(int)res.StatusCode}: {Truncate(body, 200)}");
        }
        var env = await res.Content.ReadFromJsonAsync<ApiEnvelope<PollData>>(ct);
        return env?.Data;
    }

    /// <summary>İş sonucunu bildir.</summary>
    public async Task ReportAsync(string jobId, FiscalResult r, CancellationToken ct)
    {
        var payload = new
        {
            jobId,
            ok = r.Ok,
            receiptNo = r.ReceiptNo,
            zNo = r.ZNo,
            error = r.Error,
        };
        using var res = await _http.PostAsJsonAsync("fiscal-bridge/result", payload, ct);
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"result {(int)res.StatusCode}: {Truncate(body, 200)}");
        }
    }

    private static string Truncate(string s, int n) =>
        s.Length <= n ? s : s[..n];
}
