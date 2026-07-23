// Softmoor Fiscal Bridge — veri modelleri
// Bulut API sözleşmesi: qrmenusoftmoor /fiscal-bridge/* uçları

using System.Text.Json.Serialization;

namespace SoftmoorFiscalBridge;

/// <summary>appsettings.json şeması</summary>
public sealed class BridgeConfig
{
    /// <example>https://api.rynaai.com/api/qrmenusoftmoor</example>
    public string CloudBaseUrl { get; set; } = "https://api.rynaai.com/api/qrmenusoftmoor";

    /// <summary>Panel → Ayarlar → Yazar Kasa → Köprü eşleştirme'den</summary>
    public string RestaurantId { get; set; } = "";
    public string BridgeKey { get; set; } = "";

    /// <summary>Poll aralığı (sn)</summary>
    public int PollIntervalSeconds { get; set; } = 3;

    /// <summary>true iken cihaz yerine sahte yazıcı (uçtan uca test)</summary>
    public bool UseMockPrinter { get; set; } = true;
}

// ── Bulut yanıt modelleri ────────────────────────────────────

public sealed class ApiEnvelope<T>
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("data")] public T? Data { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public sealed class PollData
{
    [JsonPropertyName("jobs")] public List<FiscalJob> Jobs { get; set; } = new();
    [JsonPropertyName("device")] public DeviceConfig Device { get; set; } = new();
}

public sealed class DeviceConfig
{
    [JsonPropertyName("provider")] public string Provider { get; set; } = "none";
    [JsonPropertyName("host")] public string? Host { get; set; }
    [JsonPropertyName("port")] public int? Port { get; set; }
    /// <summary>Hugin: cihaz altındaki 10 haneli Fiscal ID</summary>
    [JsonPropertyName("deviceSerial")] public string? DeviceSerial { get; set; }
}

public sealed class FiscalJob
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("provider")] public string Provider { get; set; } = "hugin";
    [JsonPropertyName("sale")] public Sale Sale { get; set; } = new();
}

// ── Normalize satış (backend buildSale çıktısı) ──────────────

public sealed class Sale
{
    [JsonPropertyName("orderId")] public string OrderId { get; set; } = "";
    [JsonPropertyName("tableNo")] public int? TableNo { get; set; }
    [JsonPropertyName("items")] public List<SaleItem> Items { get; set; } = new();
    /// <summary>kuruş</summary>
    [JsonPropertyName("serviceAmount")] public long ServiceAmount { get; set; }
    /// <summary>kuruş</summary>
    [JsonPropertyName("total")] public long Total { get; set; }
    [JsonPropertyName("payments")] public List<SalePayment> Payments { get; set; } = new();
}

public sealed class SaleItem
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("qty")] public int Qty { get; set; } = 1;
    /// <summary>birim fiyat, kuruş</summary>
    [JsonPropertyName("unitPrice")] public long UnitPrice { get; set; }
    /// <summary>KDV %: 0/1/10/20</summary>
    [JsonPropertyName("vatRate")] public int VatRate { get; set; } = 10;
}

public sealed class SalePayment
{
    /// <summary>"cash" | "card"</summary>
    [JsonPropertyName("type")] public string Type { get; set; } = "cash";
    /// <summary>kuruş</summary>
    [JsonPropertyName("amount")] public long Amount { get; set; }
}

// ── Fiş sonucu ───────────────────────────────────────────────

public sealed record FiscalResult(bool Ok, string? ReceiptNo, string? ZNo, string? Error);
