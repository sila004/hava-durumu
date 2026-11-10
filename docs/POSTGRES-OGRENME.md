# Projede PostgreSQL nerede kullanılıyor?

PostgreSQL veriyi tutan sunucudur. pgAdmin bu sunucudaki kayıtları görmeni sağlayan araçtır.
Projede bir düğmeye basınca istek önce .NET API'ye gider. API gerekiyorsa veritabanına yazar.
pgAdmin'i kapatmak veritabanını veya projeyi durdurmaz.

## Menüde ne değişti?

| Menü | Saklanan bilgi | Tablo |
| --- | --- | --- |
| Ana Sayfa | Hesabınla son seçtiğin şehir (`home`) | `WeatherPagePreferences` |
| Türkiye Haritası | Haritada son seçtiğin şehir (`map`) | `WeatherPagePreferences` |
| Hava Kalitesi | Son seçtiğin şehir (`air-quality`) | `WeatherPagePreferences` |
| Favorilerim | Hesabına eklediğin favori şehirler | `WeatherFavorites` |
| Karşılaştırma | İki şehir, o işlemdeki sıcaklık/nem/rüzgâr/hissedilen değerler ve kayıt zamanı | `WeatherComparisons` |
| Hesabım ve Hava Postası | Mail şehri, saati ve açık/kapalı tercihi (zaten vardı) | `WeatherMailAccounts` |
| Yönetim Merkezi | Hesaplar ve mail gönderim sonuçları (zaten vardı) | `WeatherMailAccounts`, `WeatherMailDeliveries` |
| Meteoroloji Rehberi | Sabit bilgilendirme metni | Veritabanı kaydı gerekmez |

Ana sayfanın şehri ile Hava Postası şehri farklı tercihlerdir. Haritada Ankara seçmek, mail şehrini değiştirmez.
Şehir tercihlerinde her tıklamaya yeni satır açılmaz; o hesap ve sayfanın mevcut satırı güncellenir.
Favorilerde aynı şehir bir hesaba iki kez eklenmez. Favoriden çıkarma o kaydı siler.
Karşılaştırma her başarılı işlemde yeni kayıt oluşturur; hesabın son 50 kaydı tutulur, daha eskiler otomatik silinir.
Hava sağlayıcısı hata verirse başarılı bir karşılaştırma kaydı oluşturulmaz.
Geçmişteki bir kaydı görüntülemek yeni kayıt veya hava isteği oluşturmaz.
Geçmişteki değerler canlı hava durumu değildir; yenilemek için tekrar karşılaştırmalısın.

Bu kayıtlar için Hesabım ve Hava Postası sayfasından giriş yapmalısın. Giriş yapmak günlük maili kendiliğinden açmaz.
Giriş yapmadan favoriler eskisi gibi yalnız tarayıcıda tutulur. Giriş yaptıktan sonra eski yerel favoriler varsa
**Favorilerimi hesabıma aktar** düğmesi görünür. Aktarım hesabındaki mevcut favorileri koruyup eksikleri ekler.
Giriş yapmadan hava durumu, harita, hava kalitesi ve karşılaştırma kullanılabilir; kişisel geçmiş kaydedilmez.
Her hesap API üzerinden yalnız kendi favorilerine, tercihlerine ve karşılaştırmalarına erişir.

## pgAdmin'de nasıl görürüm?

Yerel proje: `http://localhost:3000`. Yerel API: `http://localhost:5100`.
pgAdmin: **Servers → PostgreSQL → Databases → InternDb → Schemas → public → Tables**.
Yeni tablolar görünmüyorsa **Tables** üzerine sağ tıklayıp **Refresh** seç.
`InternDb` üzerinde **Query Tool** aç. Aşağıdaki sorgular yalnızca okur; veriyi değiştirmez.

### 1. Favoriye şehir ekle

Projede giriş yap, Favorilerim'de Ankara'yı ekle. Şu sorguyu çalıştır:

```sql
SELECT a."Email", f."City", f."CreatedUtc" AT TIME ZONE 'Europe/Istanbul' AS "EklenmeZamani"
FROM public."WeatherFavorites" f
JOIN public."WeatherMailAccounts" a ON a."Id" = f."AccountId"
WHERE a."Email" = 'ornek@example.com'
ORDER BY f."CreatedUtc";
```

Ankara'yı favoriden çıkarıp sorguyu yeniden çalıştır: Ankara satırı kaybolur.

### 2. Sayfalarda farklı şehirler seç

Ana sayfada Ankara, haritada Bursa, hava kalitesinde İzmir seç. Kaydedildi mesajını bekle.

```sql
SELECT a."Email", p."Section", p."City", p."UpdatedUtc" AT TIME ZONE 'Europe/Istanbul' AS "GuncellenmeZamani"
FROM public."WeatherPagePreferences" p
JOIN public."WeatherMailAccounts" a ON a."Id" = p."AccountId"
WHERE a."Email" = 'ornek@example.com'
ORDER BY p."Section";
```

Üç farklı sayfanın üç ayrı tercihini görürsün. Ana sayfayı yeniden açınca Ankara seçili gelir.
Henüz şehir seçmediğin bir sayfa için satır oluşmaz. Varsayılan şehrin gösterilmesi tek başına kayıt oluşturmaz.

### 3. Karşılaştırma yap

İki farklı şehir seçip **Şehirleri Karşılaştır** düğmesine bas. Hava verileri başarıyla gelirse kayıt oluşur.

```sql
SELECT c."FirstCity", c."FirstTemperature", c."SecondCity", c."SecondTemperature",
       c."ComparedUtc" AT TIME ZONE 'Europe/Istanbul' AS "KarsilastirmaZamani"
FROM public."WeatherComparisons" c
JOIN public."WeatherMailAccounts" a ON a."Id" = c."AccountId"
WHERE a."Email" = 'ornek@example.com'
ORDER BY c."ComparedUtc" DESC
LIMIT 20;
```

Yeni karşılaştırmalar en üstte görünür. Uygulamadaki **Karşılaştırma Geçmişim** bölümü aynı kayıtları gösterir.
**Geçmişimi temizle → Evet, geçmişimi sil** kendi karşılaştırma kayıtlarını siler; favorilerini veya mail tercihlerini etkilemez.

### 4. Hava Postası tercihini kontrol et

```sql
SELECT "Email", "City", "DeliveryMinutes", "DailyEnabled"
FROM public."WeatherMailAccounts"
WHERE "Email" = 'ornek@example.com';
```

`DeliveryMinutes` gece yarısından sonraki dakikadır: **08:08 → 8 × 60 + 8 = 488**.
`DailyEnabled = false` günlük maillerin kapalı olduğunu söyler.

## Teknik not

Üç yeni tablo `WeatherUserLibrary` EF Core migration'ıyla eklenir; mevcut tablolar ve verileri korunur.
Yerel API yeniden başladığında açılıştaki migration bu eklemeyi uygular.
Yeni kayıtlar oturumdaki hesap kimliğiyle ilişkilendirilir; istemci başka bir hesabın kimliğini göndererek kayıt seçemez.
Güncel hava verileri Open-Meteo'dan alınır ve kısa süre bellekte önbelleğe konur; her sayfa ziyaretinde hava verisi PostgreSQL'e yazılmaz.
