// Sahte yazıcı — cihaz olmadan uçtan uca akış testi.
// appsettings: "UseMockPrinter": true iken kullanılır.
// Fişi konsola yazar, sahte fiş no üretir; panelde "Fiş #M..." görünür.

namespace SoftmoorFiscalBridge.Fiscal;

public sealed class MockPrinter : IFiscalPrinter
{
    public Task<FiscalResult> PrintSaleAsync(
        Sale sale, DeviceConfig device, CancellationToken ct)
    {
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

        var receiptNo = "M" + DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 100000;
        return Task.FromResult(new FiscalResult(true, receiptNo, null, null));
    }

    private static string Tl(long kurus) => $"₺{kurus / 100m:0.00}";
}
