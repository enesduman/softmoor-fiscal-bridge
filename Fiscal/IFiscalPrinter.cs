// Yazıcı soyutlaması — sağlayıcıya göre implementasyon seçilir.

namespace SoftmoorFiscalBridge.Fiscal;

public interface IFiscalPrinter
{
    /// <summary>
    /// Satışı mali yazıcıya bas. Başarıda fiş no (ve varsa Z no) döner.
    /// Hata durumunda Ok=false + Error (bulutta Order.fiscal.error olarak görünür).
    /// </summary>
    Task<FiscalResult> ProcessAsync(FiscalJob job, DeviceConfig device, CancellationToken ct);
}
