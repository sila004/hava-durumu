# Yönetim Merkezi

Ön yüz adresi: `/admin`. Panel mevcut e-posta koduyla giriş sistemini kullanır.

## Erişim

Backend'in `Intern.API/.env` dosyasına veya hosting ortam değişkenlerine ekleyin:

```dotenv
WeatherAdmin__Emails__0=yonetici@example.com
# Birden fazla yönetici için:
# WeatherAdmin__Emails__1=ikinci@example.com
```

API'yi yeniden başlatın. `/hesabim` sayfasında bu adrese gelen kodla giriş yapın.
Ana menüdeki **Yönetim Merkezi**, hesap ekranındaki **Yönetim Merkezi’ni aç** bağlantısı
veya doğrudan `/admin` adresini kullanın. Panelden giriş ekranına geçildiğinde,
yetkili hesabın doğrulanmasının ardından otomatik olarak panele dönülür.
Boş liste hiçbir hesaba yönetici yetkisi vermez. Yetki her API isteğinde sunucuda doğrulanır.
Adresleri yalnız sunucu ortamında tutun; `NEXT_PUBLIC_` değişkenine koymayın.

## Veritabanı ve özellikler

`WeatherAdminDeliveries` migration'ı gönderim geçmişini saklayan tabloyu ekler.
Varsayılan `Database:MigrateOnStartup=true` ayarında API açılışında uygulanır.
Bu ayar kapalıysa mevcut dağıtım prosedürünüzle migration'ı ayrıca uygulayın.
Yerel PostgreSQL de çalışmalıdır; API'nin `/health/ready` adresi 200 döndürmelidir.

- Türkiye gününe göre toplam hesap, aktif abonelik, günlük başarılı/başarısız deneme sayıları.
- E-posta/şehir arama, abonelik filtresi ve sayfalama.
- Şehir, Türkiye saatine göre gönderim saati ve günlük abonelik tercihini düzenleme.
- Günlük ve test gönderim sonuçları; güvenli hata kategorileri.
- Başarısız günlük gönderimi yeniden sıraya alma. Kapalı abonelik, devam eden gönderim
  veya aynı gün tamamlanmış gönderim yeniden gönderilemez.

Geçmiş kayıtları bu özellikten sonraki gönderimlerle oluşur; eski gönderimler geri doldurulmaz.
Başarılı kayıt SMTP sunucusunun kabulünü veya geliştirmede yerel `.eml` yazımını gösterir;
alıcının gelen kutusuna teslim edildiğini garanti etmez.
Genel bakış yalnız günlük gönderimleri sayar; test mailleri geçmişte ayrıca gösterilir.
Yeniden deneme, dakikada bir çalışan mevcut zamanlayıcıya bırakılır ve seçili gönderim saatine uyar.
SMTP sırları, giriş kodları ve abonelik iptal token'ları admin API yanıtlarına eklenmez.

## Kontroller

```powershell
dotnet build tests/WeatherMail.Checks/WeatherMail.Checks.csproj
python tests/test_weather_mail.py
```

İkinci komut izole PostgreSQL, sahte hava sağlayıcısı ve yerel posta kutusu kullanır.
Gerçek e-posta göndermez; yönetici erişimi, yetkisiz erişim, abonelik düzenleme,
gönderim geçmişi, tekrar deneme sınırları ve mevcut hesap akışı kontrol edilir.
