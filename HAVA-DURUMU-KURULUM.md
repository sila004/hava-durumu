# Hava durumu backend bağlantısı

Ön yüz `hava-durumu fr`, backend `InternRepository` içinde çalışır.
İstek akışı: tarayıcı → Intern API → Open-Meteo → Intern API → tarayıcı.

Mail ile giriş, her sabah 07:00 hava özeti, örnek mail tasarımı ve SMTP ayarları
`MAIL-KURULUM.md` dosyasında açıklanır. Hesap sayfası: `http://localhost:3000/hesabim`.

## Çalıştırma (PowerShell)

İlk terminal:

```powershell
cd "C:\Users\silan\OneDrive\Desktop\hava\InternRepository"
& "$env:LOCALAPPDATA\hava-dotnet\dotnet.exe" run --project Intern.API --launch-profile weather
```

Bu makineye .NET 9 SDK, `LOCALAPPDATA\hava-dotnet` içine kuruldu.
Sisteminizde .NET 9 SDK zaten varsa aynı komutun başında `dotnet` kullanabilirsiniz.

İkinci terminal:

```powershell
cd "C:\Users\silan\OneDrive\Desktop\hava\hava-durumu fr"
npm.cmd run dev
```

Site: http://localhost:3000 — API/Swagger: http://localhost:5100/swagger.
İki terminal de açık kalmalıdır. Ön yüz farklı port seçerse o adresi backend'in
`Intern.API/appsettings.json` dosyasındaki `Cors:AllowedOrigins` listesine ekleyin.

`weather` profili artık açılışta PostgreSQL migration'larını uygular ve başlangıç
yetki kayıtlarını ekler. Yerel PostgreSQL hizmeti çalışıyor olmalıdır.
`Intern.API/.env` içinde `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USERNAME` ve
`DB_PASSWORD` alanları kullanılır. `CONNECTION_STRING` tanımlıysa bu alanlardan önce gelir.
Veritabanı yoksa ve kullanıcının oluşturma izni varsa Entity Framework oluşturur.

Mevcut migration, `Abilities` ve `Employees` tablolarını oluşturur. Hava durumu ve
hava kalitesi Open-Meteo'dan gelir; bu bağlantı hava verilerini veritabanına kaydetmez.
Yalnız hava özelliğini veritabanı açılış işlemi olmadan denemek için PowerShell'de
`$env:Database__MigrateOnStartup = "false"` ayarlayıp `http` profilini kullanabilirsiniz.

## Hangi dosya ne yapıyor?

1. `Intern.Entities/DTOs/Weather/`: ön yüze gönderilen veri alanlarını tanımlar.
2. `Intern.Service/Services/Concretes/WeatherService.cs`: şehri doğrular, koordinatını
   bulur, Open-Meteo'yu çağırır ve cevabı `Response<T>` içine koyar.
3. `Intern.API/Controllers/WeatherController.cs` ve `AirQualityController.cs`:
   tarayıcıdan gelen HTTP isteğini servise iletir.
4. `Intern.API/Program.cs`: ayarları ve HTTP istemcisini kaydeder. `WeatherService`,
   mevcut Autofac kuralı sayesinde otomatik kaydolur.
5. Ön yüzde `app/lib/api.ts`: `.env.local` içindeki `NEXT_PUBLIC_API_URL` adresine
   istek atar; backend cevabındaki `data` alanını sayfalara döndürür.

Next.js içindeki eski `app/api/.../route.ts` dosyaları kaldırıldı. Ana sayfa,
harita, favoriler, karşılaştırma ve hava kalitesi sayfaları Intern API'yi kullanır.

## Uçlar

| Adres | Kullanım |
| --- | --- |
| `/api/hava-durumu?city=İstanbul` | Anlık özet |
| `/api/hava-durumu/tahmin?city=İstanbul` | Ana sayfanın anlık, günlük ve saatlik tahmini |
| `/api/hava-kalitesi?city=İstanbul` | AQI ve kirletici özeti |
| `/api/hava-kalitesi/detay?city=İstanbul` | Ana sayfanın kirleticileri ve birimleri |

Şehir verilmezse 400, desteklenmeyen şehirde 404, dış servis hatasında 502 döner.
Hazır deneme istekleri `Intern.API/Intern.API.http` dosyasındadır.

## Açık hava planı

Ana sayfada şehir listesinden seçim yapılarak hava tahminleri görüntülenir.

**Dışarı çıkmak için en iyi saat** kartı seçilen günün henüz başlamamış gündüz
saatlerini karşılaştırır. Hissedilen sıcaklık, yağış olasılığı/miktarı, rüzgâr ve
UV dikkate alınır. Fırtına, sis, yağmur ve kar kodları; %60 ve üzeri yağış ihtimali,
0,5 mm üzeri yağış, 35 km/sa ve üzeri rüzgâr, 8 ve üzeri UV, 5°C altı veya 32°C
üzeri hissedilen sıcaklık öneriden çıkarılır. Eksik veriler sıfır sayılmaz.
Uygun saat yoksa veya gündüz saatleri bittiyse kart bunu gösterir.
Gün seçimi değişince öneri de değişir; saatler seçilen konumun yerel saatidir.

Ön yüzün saat önerisi kontrolleri (Node.js 24):

```powershell
cd "C:\Users\silan\OneDrive\Desktop\hava\hava-durumu fr"
node --test tests/outdoor.test.mjs
```

`Intern.API/.env` ve ön yüzdeki `.env.local` yerel geliştirme için örneklerden
oluşturuldu; git'e eklenmezler. Yayında kendi ortam ayarlarınızı kullanın.

Ön yüz API adresini değiştirirseniz Next.js'i yeniden başlatın. Üretim için yeni
adresle yeniden build alın; `NEXT_PUBLIC_API_URL` tarayıcı paketine build sırasında yazılır.
