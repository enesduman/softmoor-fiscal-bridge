// Hugin YN ÖKC yazıcısı — huginsdk/fpu tabanlı ŞABLON.
//
// SDK: https://github.com/huginsdk/fpu  (C# — LAN/TCP veya RS232)
// Wiki: https://github.com/huginsdk/fpu/wiki/Technical-Documentation
//
// Kurulum (bkz. README):
//   1. huginsdk/fpu repo'sunu klonla, C# kütüphanesini bu projeye referans ver.
//   2. Aşağıdaki TODO bölümlerini SDK çağrılarıyla doldur.
//   3. appsettings: "UseMockPrinter": false yap.
//
// Cihaz gereksinimleri:
//   - Cihaz "PC BAĞLANTISI" modunda, Ethernet (cihaz ekranında IP görünür).
//   - Panel → ÖKC ayarlarında IP + port + Fiscal ID (10 hane) girilmiş olmalı
//     (bu değerler her poll'da DeviceConfig ile buraya gelir).
//
// SDK akışı (wiki'deki sıra):
//   Connect(tcpSocket, deviceInfo{fiscalId, ip})   -> eşleşme
//   SignInCashier(id, sifre)                       -> gerekiyorsa kasiyer girişi
//   PrintDocumentHeader()                          -> fiş başlat
//   her kalem: PrintItem(pluNo/dept, qty, price, vat)
//   her ödeme: PrintPayment(tip: 0=Nakit 1=Kart, tutar)  <- bölünmüş ödeme = birden çok çağrı
//   CloseReceipt()                                 -> yanıt: <hata>|<durum>|<FisNo>|<ZNo>|...
//
// ÖNEMLİ: Canlı cihazda basılan her fiş GERÇEK mali kayıttır (Z raporuna girer).
// İlk testleri restoran/muhasebe ile koordineli, düşük tutarla ve iptal
// prosedürü hazırken yapın.

namespace SoftmoorFiscalBridge.Fiscal;

public sealed class HuginFpuPrinter : IFiscalPrinter
{
    public Task<FiscalResult> PrintSaleAsync(
        Sale sale, DeviceConfig device, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(device.Host) || device.Port is null or 0)
        {
            return Task.FromResult(new FiscalResult(
                false, null, null,
                "Cihaz IP/port tanımsız — Panel → Ayarlar → Yazar Kasa"));
        }
        if (string.IsNullOrWhiteSpace(device.DeviceSerial))
        {
            return Task.FromResult(new FiscalResult(
                false, null, null,
                "Fiscal ID tanımsız — cihaz altındaki 10 haneli sicil no"));
        }

        // ── TODO (SDK): aşağıyı huginsdk/fpu ile doldurun ──────────────
        //
        // using var socket = new System.Net.Sockets.TcpClient();
        // await socket.ConnectAsync(device.Host!, device.Port!.Value, ct);
        // var fpu = new Hugin.Fpu(...);                 // SDK giriş sınıfı
        // fpu.Connect(socket, new DeviceInfo {
        //     FiscalId = device.DeviceSerial, Ip = device.Host });
        //
        // fpu.PrintDocumentHeader();
        // foreach (var i in sale.Items)
        //     fpu.PrintItem(name: i.Name, qty: i.Qty,
        //         price: i.UnitPrice / 100m, vatRate: i.VatRate);
        // // Servis bedeli ayrı kalem olarak (KDV %10):
        // if (sale.ServiceAmount > 0)
        //     fpu.PrintItem("Servis", 1, sale.ServiceAmount / 100m, 10);
        // foreach (var p in sale.Payments)
        //     fpu.PrintPayment(type: p.Type == "cash" ? 0 : 1,
        //         amount: p.Amount / 100m);
        // var resp = fpu.CloseReceipt();               // "<hata>|<durum>|<FisNo>|<ZNo>|..."
        // var parts = resp.Split('|');
        // if (parts[0] != "0")
        //     return new FiscalResult(false, null, null, $"ÖKC hata kodu {parts[0]}");
        // return new FiscalResult(true, parts[2], parts[3], null);
        // ────────────────────────────────────────────────────────────────

        return Task.FromResult(new FiscalResult(
            false, null, null,
            "Hugin SDK henüz bağlanmadı — README'deki kurulum adımlarını izleyin " +
            "(huginsdk/fpu referansı + HuginFpuPrinter TODO bölümleri)"));
    }
}
