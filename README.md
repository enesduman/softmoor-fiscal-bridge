# Softmoor Fiscal Bridge

Restorandaki **kasa bilgisayarında** çalışan küçük köprü ajanı. Softmoor Menu
panelinde hesap kapatılınca bulutta oluşan **fiş işlerini** çeker, yerel
ağdaki **Hugin YN ÖKC**'ye bastırır ve fiş numarasını panele geri bildirir.

```
[menu.softmoor.com backend] --poll/result (outbound HTTPS)--> [Bu ajan]
                                                                  |
                                                        TCP (yerel ağ)
                                                                  v
                                                     [Hugin ÖKC  192.168.x.x]
```

- Dışarıdan içeri bağlantı YOK (port yönlendirme gerekmez) — ajan buluta
  kendisi bağlanır.
- Bulut 2 dakikada sonucu almazsa işi yeniden kuyruğa alır (ajan yeniden
  başlasa bile iş kaybolmaz).

## Kurulum

### 1. Gereksinim
- Windows'lu kasa PC'si (ÖKC ile aynı yerel ağda)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (derlemek için)
  veya sadece .NET 8 Runtime (yayınlanmış exe için)

### 2. Panelde eşleştirme
1. menu.softmoor.com → **Ayarlar → Yazar Kasa (ÖKC)**
2. Sağlayıcı: **Hugin**, cihaz **IP** (cihaz ekranındaki *PC BAĞLANTISI*
   adresi), **port** ve **Fiscal ID** (cihaz altındaki 10 haneli sicil no) gir → Kaydet
3. **Anahtar Üret** → çıkan `RestaurantId` + `BridgeKey` değerlerini kopyala
   (anahtar yalnızca o an görünür)

### 3. Ajanı yapılandır ve çalıştır
```bash
cp appsettings.example.json appsettings.json
# RestaurantId + BridgeKey değerlerini yapıştır
dotnet run
```
İlk çalıştırma **Mock modundadır** (`UseMockPrinter: true`): gerçek fiş
basılmaz, fişler konsola yazılır ve panelde "Fiş #M..." görünür — uçtan uca
akışı cihazsız doğrulamak için. Panelde **Köprü: çevrimiçi** rozetini görün.

### 4. Hugin SDK'yı bağla (gerçek fiş)
1. `git clone https://github.com/huginsdk/fpu`
2. C# kütüphanesini bu projeye referans ver (`.csproj` içindeki nota bakın)
3. `Fiscal/HuginFpuPrinter.cs` içindeki **TODO** bölümlerini SDK çağrılarıyla
   doldurun (akış dosyada adım adım yorumlanmıştır:
   Connect → PrintDocumentHeader → PrintItem × n → PrintPayment × n → CloseReceipt)
4. `appsettings.json` → `"UseMockPrinter": false`

### 5. Windows servisi olarak (opsiyonel, önerilir)
```powershell
dotnet publish -c Release -r win-x64 --self-contained
sc.exe create SoftmoorFiscalBridge binPath= "C:\softmoor-bridge\SoftmoorFiscalBridge.exe" start= auto
sc.exe start SoftmoorFiscalBridge
```

## ⚠️ Canlı cihaz uyarısı
`UseMockPrinter: false` iken basılan **her fiş gerçek mali kayıttır** (Z
raporuna girer). İlk canlı testleri:
- restoran/muhasebe ile koordineli,
- düşük tutarlı bir sipariş ve **iptal/iade prosedürü hazırken**,
- tercihen gün sonuna yakın yapın.

## Sorun giderme
| Belirti | Muhtemel neden |
|---|---|
| Panelde köprü **çevrimdışı** | Ajan kapalı / internet yok / anahtar yenilenmiş (panelden yeni anahtar alın) |
| `poll 401` | RestaurantId/BridgeKey hatalı veya anahtar yenilenmiş |
| Fiş `failed: Cihaz IP/port tanımsız` | Panel ÖKC ayarlarında IP/port eksik |
| Fiş `failed: Hugin SDK henüz bağlanmadı` | Adım 4 tamamlanmamış (Mock'tan çıkılmış ama SDK yok) |
