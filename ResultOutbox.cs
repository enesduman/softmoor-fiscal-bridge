using System.Text.Json;

namespace SoftmoorFiscalBridge;

/// <summary>
/// Cihaz sonucu buluta gönderilmeden önce diske yazılır. İnternet veya süreç
/// kesilirse aynı tahsilat yeniden başlatılmadan sonuç sonraki açılışta iletilir.
/// </summary>
public sealed class ResultOutbox
{
    private readonly string _path;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = true };
    private Dictionary<string, FiscalResult> _pending;

    public ResultOutbox(string path)
    {
        _path = path;
        _pending = Load(path);
    }

    public IReadOnlyList<KeyValuePair<string, FiscalResult>> Pending() =>
        _pending.ToList();

    public void Put(string jobId, FiscalResult result)
    {
        _pending[jobId] = result;
        Persist();
    }

    public void Remove(string jobId)
    {
        if (!_pending.Remove(jobId)) return;
        Persist();
    }

    private void Persist()
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_pending, _json));
        File.Move(tempPath, _path, true);
    }

    private static Dictionary<string, FiscalResult> Load(string path)
    {
        if (!File.Exists(path)) return new();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, FiscalResult>>(
                File.ReadAllText(path),
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Sonuç kuyruğu okunamadı ({path}). Dosyayı silmeden önce yedekleyin: {ex.Message}", ex);
        }
    }
}
