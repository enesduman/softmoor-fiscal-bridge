# Softmoor Fiscal Bridge

Restorandaki **kasa bilgisayarında** çalışan küçük köprü ajanı. Softmoor Menu
panelinden oluşan **fiş ve terminal ödeme işlerini** çeker, yerel ağdaki
**Hugin YN ÖKC** ile HTTPS üzerinden haberleşir ve sonucu panele geri bildirir.

```
[menu.softmoor.com backend] --poll/result (outbound HTTPS)--> [Bu ajan]
                                                                  |
                                                      HTTPS (yerel ağ)
                                                                  v
                                                     [Hugin ÖKC  192.168.x.x]
```

- Dışarıdan içeri bağlantı YOK (port yönlendirme gerekmez) — ajan buluta
  kendisi bağlanır.
- Fiş işleri zaman aşımında yeniden kuyruğa alınır. Terminal ödeme işi zaman
  aşımına girerse ikinci çekimi önlemek için `unknown` olur ve cihaz kontrolü ister.
- Sipariş, kart tahsilatı cihaz tarafından onaylanmadan `paid` yapılmaz.
- Cihaz sonucu önce `result-outbox.json` dosyasına yazılır; internet veya bridge
  yeniden başlatılsa bile kesin sonuç buluta ulaşana kadar saklanır.

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
# RestaurantId + BridgeKey + SoftwareId değerlerini yapıştır
dotnet run
```
İlk çalıştırma **Mock modundadır** (`UseMockPrinter: true`): gerçek fiş
basılmaz, işlemler konsola yazılır — uçtan uca akışı cihazsız doğrulamak için.
`MockPaymentOutcome` ile `approved`, `declined` ve `failed` senaryoları
denenebilir. Panelde **Köprü: çevrimiçi** rozetini görün.

### 4. Hugin PC Link'i aç (gerçek cihaz)
1. Hugin'den PC Link entegrasyon/aktivasyon bilgisini alın.
2. Cihazda PC Link'i açın ve kasa PC'sinden `https://CIHAZ-IP:4443` erişimini doğrulayın.
3. `SoftwareId` alanına Hugin entegrasyonunda tanımlanan VKN'yi girin.
4. `HardwareId` boşsa aktif ağ kartının MAC'i kullanılır; Hugin eşleşmesinde
   sabit MAC gerekiyorsa değeri açıkça yazın.
5. `appsettings.json` → `"UseMockPrinter": false`.

Bridge resmi iki adımlı PC Link akışını kullanır: `POST /v1/documents`, ardından
ürünler ve `EFT_POS` ödeme ile `PUT /v1/documents/{id}`. DLL/SDK gerekmez.

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
| `SoftwareId tanımsız` | Hugin entegrasyonunda tanımlanan VKN appsettings'e girilmemiş |
| `Terminali kontrol et` | Ödeme sırasında zaman aşımı oldu; yeni çekim başlatmadan son işlemi cihazdan kontrol edin |
| HTTP 206 / mali belge kapanmadı | Kart tahsil edilmiştir; cihazdaki kağıt/pil sorununu giderip açık belgeyi tamamlayın |
