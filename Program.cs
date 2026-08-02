// Softmoor Fiscal Bridge — restorandaki kasa PC'sinde çalışan köprü ajanı.
//
// Görev: bulut (menu.softmoor.com backend) fiş işlerini kuyruğa yazar;
// bu ajan poll ile çeker, yerel ağdaki ÖKC'ye basar, sonucu bildirir.
//
//   [Bulut] --poll/result (outbound HTTPS)--> [Bu ajan] --HTTPS--> [Hugin ÖKC]
//
// Çalıştırma:  dotnet run   (veya yayınlanmış exe; Windows service için README)

using System.Text.Json;
using SoftmoorFiscalBridge;
using SoftmoorFiscalBridge.Fiscal;

// ── Config yükle ─────────────────────────────────────────────
var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
if (!File.Exists(configPath))
{
    // Geliştirme kolaylığı: çalışma dizininde de ara
    configPath = "appsettings.json";
}
if (!File.Exists(configPath))
{
    Console.Error.WriteLine(
        "appsettings.json bulunamadı. appsettings.example.json'u kopyalayıp " +
        "RestaurantId + BridgeKey değerlerini panelden alın " +
        "(Ayarlar → Yazar Kasa → Köprü eşleştirme).");
    return 1;
}

var cfg = JsonSerializer.Deserialize<BridgeConfig>(
    File.ReadAllText(configPath),
    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
if (cfg is null ||
    string.IsNullOrWhiteSpace(cfg.RestaurantId) ||
    string.IsNullOrWhiteSpace(cfg.BridgeKey))
{
    Console.Error.WriteLine("appsettings.json eksik: RestaurantId ve BridgeKey zorunlu.");
    return 1;
}

var client = new BridgeClient(cfg);
var outbox = new ResultOutbox(Path.Combine(AppContext.BaseDirectory, "result-outbox.json"));
IFiscalPrinter printer = cfg.UseMockPrinter
    ? new MockPrinter(cfg)
    : new HuginFpuPrinter(cfg);

Log($"Softmoor Fiscal Bridge başladı — yazıcı: " +
    (cfg.UseMockPrinter ? "MOCK (test)" : "Hugin") +
    $", poll: {cfg.PollIntervalSeconds}s");
if (cfg.UseMockPrinter)
    Log("UYARI: Mock modunda — gerçek fiş BASILMAZ. Canlı için UseMockPrinter=false.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

// ── Ana döngü ────────────────────────────────────────────────
var backoff = 0;
while (!cts.IsCancellationRequested)
{
    try
    {
        foreach (var pending in outbox.Pending())
        {
            await ReportWithRetryAsync(client, pending.Key, pending.Value, cts.Token);
            outbox.Remove(pending.Key);
            Log($"Bekleyen sonuç buluta iletildi: {pending.Key}");
        }

        var data = await client.PollAsync(cts.Token);
        backoff = 0;

        foreach (var job in data?.Jobs ?? new List<FiscalJob>())
        {
            Log($"İş alındı: {job.Id} (sipariş {job.Sale.OrderId}, " +
                $"{job.Sale.Items.Count} kalem, toplam ₺{job.Sale.Total / 100m:0.00})");
            FiscalResult result;
            try
            {
                result = await printer.ProcessAsync(job, data!.Device, cts.Token);
            }
            catch (Exception ex)
            {
                result = new FiscalResult(false, null, null, ex.Message);
            }

            // Tahsilat/fiş sonucu kaybolmasın: buluta göndermeden önce yerel
            // kalıcı kuyruğa yaz. Başarılı rapordan sonra silinir.
            outbox.Put(job.Id, result);

            try
            {
                await ReportWithRetryAsync(client, job.Id, result, cts.Token);
                outbox.Remove(job.Id);
                Log(result.Ok
                    ? job.JobType == "terminal_payment"
                        ? $"  ✓ POS onaylandı: {result.TransactionId ?? "işlem no yok"}"
                        : $"  ✓ Fiş basıldı: #{result.ReceiptNo}"
                    : $"  ✗ Başarısız: {result.Error}");
            }
            catch (Exception ex)
            {
                // Yerel outbox'ta kaldı; sonraki döngü/açılışta yeniden bildirilir.
                Log($"  ! Sonuç bildirilemedi: {ex.Message}");
            }
        }
    }
    catch (OperationCanceledException)
    {
        break;
    }
    catch (Exception ex)
    {
        backoff = Math.Min(backoff + 1, 5);
        Log($"Poll hatası: {ex.Message} (backoff x{backoff})");
    }

    try
    {
        await Task.Delay(
            TimeSpan.FromSeconds(cfg.PollIntervalSeconds * (1 + backoff)),
            cts.Token);
    }
    catch (OperationCanceledException) { break; }
}

Log("Köprü durduruldu.");
return 0;

static void Log(string msg) =>
    Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

static async Task ReportWithRetryAsync(
    BridgeClient client, string jobId, FiscalResult result, CancellationToken ct)
{
    Exception? last = null;
    for (var attempt = 1; attempt <= 3; attempt++)
    {
        try
        {
            await client.ReportAsync(jobId, result, ct);
            return;
        }
        catch (Exception ex) when (attempt < 3)
        {
            last = ex;
            await Task.Delay(TimeSpan.FromSeconds(attempt * 2), ct);
        }
    }
    throw last ?? new InvalidOperationException("Sonuç bildirilemedi");
}
