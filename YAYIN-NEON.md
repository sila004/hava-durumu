# Vercel + Render + Neon

## Backend (Render Web Service)

GitHub'daki backend deposunu seçin. Docker, Frankfurt ve Free kullanın.
Root Directory boş, Dockerfile yolu `./Dockerfile`, Health Check `/health/ready`.

Render Environment bölümüne:

| Anahtar | Değer |
| --- | --- |
| `DATABASE_URL` | Neon'daki doğrudan `postgresql://...` adresi; `DATABASE_URL=` öneki ve dış tırnaklar olmadan |
| `JWT_SECRET` | Render Generate ile oluşturulan en az 32 karakterlik rastgele sır |
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_HTTP_PORTS` | `10000` |
| `ASPNETCORE_HTTPS_PORT` | `443` |
| `ReverseProxy__Enabled` | `true` |
| `Features__EnableSampleControllers` | `false` |
| `Database__MigrateOnStartup` | `true` |
| `Cors__AllowedOrigins__0` | Vercel'in gerçek HTTPS site adresi |
| `WeatherAdmin__Emails__0` | Yetkili yönetici e-posta adresi |
| `Mail__Enabled` | `false` (mail API'si bağlanana kadar) |

Alternatif olarak Blueprint kurulumunda `render.neon.yaml` dosyasını seçin.
Bu dosya ek Render veritabanı oluşturmaz. Neon URL içindeki SSL ve channel binding
ayarları korunur. Npgsql biçimindeki `CONNECTION_STRING` de desteklenir ve önceliklidir.
Yerel `DB_*` alanlarını uzak sunucuya kopyalamayın.

İlk açılışta migration'lar uygulanır. `/health/ready` 200 döndüğünde API hazırdır.
Yerel hesaplar otomatik taşınmaz; Neon ayrı bir veritabanıdır.

## Ön yüz (Vercel)

Ön yüz deposunu Next.js projesi olarak ekleyin. Environment Variables bölümünde:

- `INTERNAL_API_URL`: Render'ın gerçek `https://...onrender.com` kök adresi.
- `NEXT_PUBLIC_API_URL`: eklemeyin veya boş bırakın; tarayıcı aynı sitenin `/api` yolunu kullanır.

Yayına çıkınca hava sayfalarını, `/api/weather-account/status` ve `/admin` girişini kontrol edin.
Tüm `.env`, PostgreSQL parolaları, Gmail uygulama şifresi, mail dosyaları ve loglar kaynak
deposunun dışında kalmalıdır. Gizli değerler yalnız hosting ortam ayarlarına girilir.

## Ücretsiz plan sınırları

Render Free uykuya geçer ve SMTP 587 bağlantısını engeller. Mevcut Gmail SMTP girişi bu
planda kullanılamaz. Mail ile giriş/Yönetim Merkezi ve günlük mail için HTTPS mail API'si
kurulumu ayrıca tamamlanmalıdır. Günlük işçi uyuyan sunucuda çalışmaz; ücretsiz planda
tam saat garantisi yoktur. Yalnız ön yüzün yayına alınması bu özellikleri çalıştırmaz.

Resmî kaynaklar: https://render.com/docs/free ve https://vercel.com/docs/deployments
