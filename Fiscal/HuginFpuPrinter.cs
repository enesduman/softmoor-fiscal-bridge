// Hugin PC Link REST adaptörü.
// Resmi akış: POST /v1/documents -> PUT /v1/documents/{id}
// terminal_payment işinde ödeme tipi EFT_POS'tur ve kart ekranını cihaz açar.

using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace SoftmoorFiscalBridge.Fiscal;

public sealed class HuginFpuPrinter : IFiscalPrinter
{
    private readonly BridgeConfig _config;

    public HuginFpuPrinter(BridgeConfig config) => _config = config;

    public async Task<FiscalResult> ProcessAsync(
        FiscalJob job, DeviceConfig device, CancellationToken ct)
    {
        if (!string.Equals(device.Provider, "hugin", StringComparison.OrdinalIgnoreCase))
            return Failed($"'{device.Provider}' cihaz adaptörü bu bridge sürümünde yok");
        if (string.IsNullOrWhiteSpace(device.Host) || device.Port is null or 0)
            return Failed("Cihaz IP/port tanımsız — Panel → Ayarlar → Yazar Kasa");
        if (!IPAddress.TryParse(device.Host, out _))
            return Failed("Hugin cihaz adresi geçerli bir IP olmalı");
        if (string.IsNullOrWhiteSpace(_config.SoftwareId))
            return Failed("SoftwareId tanımsız — appsettings.json içine entegrasyon VKN bilgisini girin");

        // Eski kart ödemeleri panelde zaten paid yapılmıştır. Bunları EFT_POS'a
        // çevirmek ikinci kez kart çekebilir; bu nedenle yalnız yeni terminal
        // ödeme işi cihazda kart tahsilatı başlatabilir.
        if (job.JobType != "terminal_payment" &&
            job.Sale.Payments.Any(p => p.Type.Equals("card", StringComparison.OrdinalIgnoreCase)))
        {
            return Failed(
                "Güvenlik: daha önce kartla ödendi işaretlenen fiş için yeniden EFT-POS tahsilatı başlatılmadı");
        }

        var hardwareId = string.IsNullOrWhiteSpace(_config.HardwareId)
            ? ResolveHardwareId()
            : _config.HardwareId.Trim();
        if (string.IsNullOrWhiteSpace(hardwareId))
            return Failed("HardwareId üretilemedi; appsettings.json içinde HardwareId tanımlayın");

        var handler = new HttpClientHandler();
        if (_config.AllowInvalidDeviceCertificate)
        {
            // PC Link cihazları yerel ağda üretici/self-signed sertifika kullanabilir.
            // Bu istisna yalnız bu cihaz HttpClient'ına uygulanır.
            handler.ServerCertificateCustomValidationCallback = (_, _, _, _) => true;
        }
        using var http = new HttpClient(handler)
        {
            BaseAddress = new UriBuilder("https", device.Host, device.Port.Value).Uri,
            Timeout = TimeSpan.FromSeconds(Math.Clamp(_config.DeviceTimeoutSeconds, 30, 300)),
        };
        http.DefaultRequestHeaders.Add("X-SoftwareId", _config.SoftwareId.Trim());
        http.DefaultRequestHeaders.Add("X-HardwareId", hardwareId);
        if (!string.IsNullOrWhiteSpace(device.DeviceSerial))
            http.DefaultRequestHeaders.Add("X-SerialNo", device.DeviceSerial.Trim());

        var documentOpened = false;
        try
        {
            using var opened = await http.PostAsJsonAsync(
                "/v1/documents", new { docCategory = "SALE" }, ct);
            var openedBody = await opened.Content.ReadAsStringAsync(ct);
            if (!opened.IsSuccessStatusCode)
                return FromDeviceError(opened.StatusCode, openedBody);
            using var openedJson = JsonDocument.Parse(openedBody);
            var documentId = FindString(openedJson.RootElement, "documentId", "id", "documentNo");
            if (string.IsNullOrWhiteSpace(documentId))
                return Failed("Hugin belge kimliği dönmedi");
            documentOpened = true;

            var items = job.Sale.Items.Select(item => new
            {
                name = item.Name,
                quantity = item.Qty.ToString(CultureInfo.InvariantCulture),
                unitPrice = Money(item.UnitPrice),
                amount = Money(item.UnitPrice * item.Qty),
                vatRate = item.VatRate,
            }).ToList<object>();
            if (job.Sale.ServiceAmount > 0)
            {
                items.Add(new
                {
                    name = "Servis",
                    quantity = "1",
                    unitPrice = Money(job.Sale.ServiceAmount),
                    amount = Money(job.Sale.ServiceAmount),
                    vatRate = 10,
                });
            }

            var paymentType = job.JobType == "terminal_payment" ? "EFT_POS" : "CASH";
            var payments = new[]
            {
                new
                {
                    type = paymentType,
                    amount = Money(job.Sale.Total),
                    detailedResponse = job.JobType == "terminal_payment",
                },
            };
            HttpResponseMessage? closed = null;
            string closedBody = "";
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                closed?.Dispose();
                using var closeRequest = new HttpRequestMessage(
                    HttpMethod.Put, $"/v1/documents/{Uri.EscapeDataString(documentId)}")
                {
                    Content = JsonContent.Create(new { items, payments }),
                };
                closed = await http.SendAsync(closeRequest, ct);
                closedBody = await closed.Content.ReadAsStringAsync(ct);
                if (closed.StatusCode != HttpStatusCode.PartialContent) break;
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            using (closed)
            {
                if (closed is null) return Failed("Hugin belge sonlandırma yanıtı alınamadı");
                if (closed.StatusCode == HttpStatusCode.PartialContent)
                {
                    using var partialJson = JsonDocument.Parse(closedBody);
                    return new FiscalResult(
                        true, null, null,
                        "Kart tahsilatı onaylandı fakat mali belge kapatılamadı; cihazdaki belgeyi tamamlayın",
                        "approved",
                        FindString(partialJson.RootElement, "transactionId", "transactionRef", "paymentId"),
                        FindString(partialJson.RootElement, "authCode", "authorizationCode"),
                        FindString(partialJson.RootElement, "rrn", "retrievalReferenceNumber"),
                        FindString(partialJson.RootElement, "cardMask", "maskedPan", "maskedCardNumber"));
                }
                if (!closed.IsSuccessStatusCode)
                    return FromDeviceError(closed.StatusCode, closedBody);
                using var closedJson = JsonDocument.Parse(closedBody);
                var status = FindString(closedJson.RootElement, "status", "result", "paymentStatus");
                if (LooksDeclined(status) || LooksDeclined(closedBody))
                    return new FiscalResult(false, null, null, DeviceMessage(closedBody), "declined");
                if (LooksFailed(status))
                    return Failed(DeviceMessage(closedBody));

                return new FiscalResult(
                    true,
                    FindString(closedJson.RootElement, "receiptNo", "fiscalReceiptNo", "documentNo", "documentNumber"),
                    FindString(closedJson.RootElement, "zNo", "zNumber"),
                    null,
                    job.JobType == "terminal_payment" ? "approved" : null,
                    FindString(closedJson.RootElement, "transactionId", "transactionRef", "paymentId"),
                    FindString(closedJson.RootElement, "authCode", "authorizationCode"),
                    FindString(closedJson.RootElement, "rrn", "retrievalReferenceNumber"),
                    FindString(closedJson.RootElement, "cardMask", "maskedPan", "maskedCardNumber"));
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FiscalResult(
                false, null, null,
                "Terminal zaman aşımı: işlem sonucu belirsiz; yeniden çekim yapmadan cihazı kontrol edin",
                "unknown");
        }
        catch (HttpRequestException ex)
        {
            return documentOpened
                ? new FiscalResult(
                    false, null, null,
                    $"Hugin bağlantısı işlem sırasında koptu; terminali kontrol edin: {ex.Message}",
                    "unknown")
                : Failed($"Hugin PC Link bağlantı hatası: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return Failed($"Hugin yanıtı okunamadı: {ex.Message}");
        }
    }

    private static string Money(long kurus) =>
        (kurus / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static FiscalResult Failed(string error) =>
        new(false, null, null, error, "failed");

    private static FiscalResult FromDeviceError(HttpStatusCode code, string body)
    {
        var message = $"Hugin HTTP {(int)code}: {DeviceMessage(body)}";
        return new FiscalResult(false, null, null, message,
            LooksDeclined(body) ? "declined" : "failed");
    }

    private static string DeviceMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "Cihaz boş hata yanıtı döndürdü";
        try
        {
            using var json = JsonDocument.Parse(body);
            return FindString(json.RootElement, "message", "errorMessage", "detail", "error")
                ?? Truncate(body, 300);
        }
        catch (JsonException) { return Truncate(body, 300); }
    }

    private static string? FindString(JsonElement element, params string[] names)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (names.Any(name => property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    if (property.Value.ValueKind == JsonValueKind.String)
                        return property.Value.GetString();
                    if (property.Value.ValueKind == JsonValueKind.Number)
                        return property.Value.GetRawText();
                }
            }
            foreach (var property in element.EnumerateObject())
            {
                var nested = FindString(property.Value, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindString(item, names);
                if (!string.IsNullOrWhiteSpace(nested)) return nested;
            }
        }
        return null;
    }

    private static bool LooksDeclined(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("declin", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("redd", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("onaylanmad", StringComparison.OrdinalIgnoreCase));

    private static bool LooksFailed(string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        (value.Contains("fail", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("error", StringComparison.OrdinalIgnoreCase) ||
         value.Contains("hata", StringComparison.OrdinalIgnoreCase));

    private static string ResolveHardwareId()
    {
        var nic = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(candidate =>
            candidate.OperationalStatus == OperationalStatus.Up &&
            candidate.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
            candidate.GetPhysicalAddress().GetAddressBytes().Length > 0);
        var bytes = nic?.GetPhysicalAddress().GetAddressBytes() ?? Array.Empty<byte>();
        return string.Join(":", bytes.Select(value => value.ToString("X2")));
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
