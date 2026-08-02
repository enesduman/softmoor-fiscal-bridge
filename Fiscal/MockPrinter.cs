// Sahte yazıcı — cihaz olmadan uçtan uca akış testi.
// appsettings: "UseMockPrinter": true iken kullanılır.
// Fişi konsola yazar, sahte fiş no üretir; panelde "Fiş #M..." görünür.

namespace SoftmoorFiscalBridge.Fiscal;

public sealed class MockPrinter : IFiscalPrinter
{
    private readonly string _paymentOutcome;

    public MockPrinter(BridgeConfig config) =>
        _paymentOutcome = config.MockPaymentOutcome.Trim().ToLowerInvariant();

    public Task<FiscalResult> ProcessAsync(
        FiscalJob job, DeviceConfig device, CancellationToken ct)
    {
        var sale = job.Sale;
        Console.WriteLine("┌─────── MOCK FİŞ ───────");
        Console.WriteLine($"│ Sipariş: {sale.OrderId}  Masa: {sale.TableNo?.ToString() ?? "-"}");
        foreach (var i in sale.Items)
            Console.WriteLine($"│ {i.Qty} x {i.Name}  {Tl(i.UnitPrice)}  (KDV %{i.VatRate})");
        if (sale.ServiceAmount > 0)
            Console.WriteLine($"│ Servis: {Tl(sale.ServiceAmount)}");
        Console.WriteLine($"│ TOPLAM: {Tl(sale.Total)}");
        foreach (var p in sale.Payments)
            Console.WriteLine($"│ Ödeme [{(p.Type == "cash" ? "NAKİT" : "KART")}]: {Tl(p.Amount)}");
        Console.WriteLine("└────────────────────────");

        if (job.JobType == "terminal_payment" && _paymentOutcome != "approved")
        {
            var declined = _paymentOutcome == "declined";
            return Task.FromResult(new FiscalResult(
                false, null, null,
                declined ? "MOCK: banka işlemi reddetti" : "MOCK: terminal bağlantı hatası",
                declined ? "declined" : "failed"));
        }
        var stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var receiptNo = "M" + stamp % 100000;
        return Task.FromResult(new FiscalResult(
            true, receiptNo, null, null,
            job.JobType == "terminal_payment" ? "approved" : null,
            job.JobType == "terminal_payment" ? $"MOCK-{stamp}" : null,
            job.JobType == "terminal_payment" ? "000001" : null,
            job.JobType == "terminal_payment" ? (stamp % 1000000000000).ToString("000000000000") : null,
            job.JobType == "terminal_payment" ? "**** **** **** 4242" : null));
    }

    private static string Tl(long kurus) => $"₺{kurus / 100m:0.00}";
}
