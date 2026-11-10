# Mail ile giriş ve günlük hava postası

Site menüsündeki **Mail ile giriş** veya **Hesabım ve Hava Postası** bağlantısı
`http://localhost:3000/hesabim` sayfasını açar. Bu sayfada giriş ve günlük mail
tercihleri bulunur. Örnek mail tasarımı sitede gösterilmez; geliştirme örneği
`docs/mail-onizleme.html` dosyasındadır. Gerçek gönderici kurulmadan giriş
açıkmış veya mail gönderilmiş gibi gösterilmez.

## Gönderici hesabını bağlama

### Gmail ile kurulum

Yerel `Intern.API/.env` dosyasında Gmail sunucusu `smtp.gmail.com`, STARTTLS
portu `587` ve `Smtp` modu hazırlandı. Gönderim, hesap bilgileri eklenene kadar
kapalıdır.

1. Google hesabınızda [2 Adımlı Doğrulama](https://myaccount.google.com/signinoptions/two-step-verification)
   açık olmalıdır.
2. [Uygulama şifreleri](https://myaccount.google.com/apppasswords) sayfasında
   `Meteoroloji` adıyla bir uygulama şifresi oluşturun. Bu işlem Google hesabında
   sizin tarafınızdan tamamlanmalıdır. Hesap türü veya yönetici kısıtlamaları
   nedeniyle seçenek görünmeyebilir; bu durumda güvenlik ayarlarını gevşetmeyin.
3. `.env` içinde `Mail__Username` ve `Mail__FromAddress` alanlarına aynı tam
   Gmail adresini, `Mail__Password` alanına yeni uygulama şifresini yazın.
   Şifreyi boşluk bırakmadan girin; normal Gmail giriş şifresini kullanmayın.
4. `Mail__Enabled=true` yapın. Şifreyi sohbete veya ekran görüntüsüne eklemeyin.
5. Backend'i durdurup yeniden başlatın. `/hesabim` sayfasından kendi mail
   adresinizle giriş kodu isteyerek bağlantıyı deneyin. Kod ulaştıktan sonra
   günlük hava özeti için şehri seçip 07:00 aboneliğini açın.

Kaynaklar: [Google uygulama şifresi açıklaması](https://support.google.com/accounts/answer/185833),
[Gmail SMTP ve STARTTLS](https://developers.google.com/workspace/gmail/imap/imap-smtp).

Yapılandırılmış Gmail adresine bağlantı testi göndermek için (tek gerçek mail
gönderir; parolayı/adresi/ham SMTP yanıtını konsola yazmaz):

```powershell
dotnet build tests/WeatherMail.Checks/WeatherMail.Checks.csproj
cd Intern.API
& ../tests/WeatherMail.Checks/bin/Debug/net9.0/WeatherMail.Checks.exe --smtp-test
```

`SMTP_TEST_ACCEPTED`, SMTP sunucusunun maili kabul ettiğini gösterir; gelen
kutusu ve spam klasörünü alıcı hesabından kontrol edin.

### Başka bir SMTP sağlayıcısı

STARTTLS destekleyen bir SMTP hesabı kullanın. Sağlayıcınızın panelinden SMTP
sunucusu, kullanıcı adı ve SMTP parolası / uygulama parolası alın. Normal hesap
şifrenizi sohbete veya kaynak koda yazmayın.

`Intern.API/.env` dosyasına aşağıdaki alanları ekleyin. Bu dosya git tarafından
yoksayılır. Örnek değerleri kendi hesabınızla değiştirin:

```dotenv
Mail__Enabled=true
Mail__DeliveryMode=Smtp
Mail__Host=smtp.saglayiciniz.com
Mail__Port=587
Mail__Username="smtp-kullanici-adiniz"
Mail__Password="smtp-veya-uygulama-parolaniz"
Mail__FromAddress="dogrulanmis-gonderici@alanadiniz.com"
Mail__FromName="Meteoroloji"
Mail__FrontendUrl=http://localhost:3000
Mail__ApiUrl=http://localhost:5100
```

Gönderici adresi SMTP sağlayıcısında doğrulanmış olmalıdır. Kendi alan adınızı
kullanıyorsanız sağlayıcının verdiği SPF/DKIM kayıtlarını da tamamlayın. Bu
uygulama STARTTLS kullanır; doğrudan SSL isteyen 465 yerine sağlayıcının
STARTTLS portunu (genellikle 587) seçin.

Backend'i yeniden başlatın. `weather` profili yeni `WeatherMailAccounts` ve
`WeatherLoginChallenges` tablolarının migration'ını uygular; PostgreSQL açık
olmalıdır. Çalıştırma yöntemi `HAVA-DURUMU-KURULUM.md` dosyasındadır.

## Kullanım

1. `/hesabim` sayfasında mail adresinizi yazıp giriş kodunu isteyin.
2. Gelen 6 haneli kodla giriş yapın. Kod 10 dakika geçerli, tek kullanımlık ve
   en fazla 5 denemeliktir. Yeniden kod isteme aralığı 60 saniyedir.
3. Yaşadığınız şehri seçin. Varsayılan gönderim saati **07:00, Türkiye saati**.
4. **Her gün hava özeti almak istiyorum** kutusunu seçip tercihleri kaydedin.
   Giriş yapmak tek başına abonelik açmaz.
5. **Bana bir test maili gönder** ile kaydettiğiniz şehir için bugünün özetini
   hemen deneyin. Bu işlem günlük gönderim kaydını değiştirmez.

Günlük özette şehrin en yüksek/en düşük sıcaklığı, hava koşulu, yağış olasılığı,
gün doğumu/batımı, günün kısa notu ve siteye dönüş bağlantısı bulunur. Tasarım
sitenin lacivert, açık gri ve yeşil kart renklerini kullanır. Gösterilen büyük
sıcaklık günün en yüksek tahminidir; güncel ölçüm olarak sunulmaz.

Her mailin altındaki bağlantı aboneliği durdurur. Bağlantıyı açmak bir onay
sayfası gösterir; butona basınca günlük mailler kapanır. Mail istemcilerinin
standart tek tıklamalı abonelik iptal POST isteği de desteklenir. Hesap
sayfasındaki kutuyu kapatıp kaydetmek de aynı tercihi değiştirir.

## Her gün otomatik çalışması

Gönderim işçisi API içinde çalışır ve dakikada bir kontrol eder. **Günlük mailin
bilgisayar kapalıyken de gelmesi için API ve PostgreSQL sürekli çalışan bir
sunucuda barındırılmalıdır.** Visual Studio veya backend terminali kapanırsa
gönderim çalışmaz. Yeni açılan abonelik bir sonraki seçilmiş saatte başlar.
Sunucu aynı gün geç açılırsa o günün henüz gönderilmemiş özetini tamamlar;
geçmiş günleri topluca göndermez.

Normal çalışmada aynı hesap için günde tek gönderim yapılır. İşçi kayıtları
veritabanında 10 dakikalık kilitle alır. Tahmin/mail sağlayıcısı hatasında 15
dakika sonra tekrar dener. SMTP, sağlayıcı tarafından kabul edilme ile
veritabanı kaydı arasındaki sunucu çökmesinde kesin tek-sefer garantisi sunmaz;
bu dar durumda tekrar mail gelebilir.

Yayında `Mail__FrontendUrl` ve `Mail__ApiUrl` değerlerini gerçek HTTPS
adreslerine, CORS listesini ön yüz adresine ayarlayın. Ön yüz ile API aynı site
altında (örneğin `hava.example.com` / `api.example.com`) çalışmalıdır.
Oturum HttpOnly, SameSite=Lax çerezidir; yayında Secure zorunludur.
Birden çok API örneği kullanılıyorsa cookie Data Protection anahtarlarını
paylaşılan, kalıcı ve korunan bir konumda tutun. Reverse proxy arkasında IP
oran sınırlamasının doğru çalışması için yalnız güvenilen proxy'leri yapılandırın.

## Gerçek mail göndermeden geliştirme testi

Yalnız `Development` ortamında `Mail__DeliveryMode=Pickup` kullanılırsa
mailler `.eml` dosyalarına yazılır. Giriş kodları HTTP yanıtlarında ve loglarda
gösterilmez; yerel `.eml` dosyasından okunur. Bu modu yayında kullanmayın.
`App_Data/` git dışında tutulur; gerçek mail ve giriş kodları içeren dosyaları
paylaşmayın.

```powershell
$env:Mail__Enabled = "true"
$env:Mail__DeliveryMode = "Pickup"
dotnet run --project Intern.API --launch-profile weather
```

Bu ortam değişkenlerini denemeden sonra kaldırın. `.env` içinde aynı isimler
varsa dosyanın değerleri yüklenir; gerçek SMTP'ye geçerken `.env` ayarlarını
esas alın.

Otomatik kontroller:

```powershell
dotnet run --project tests/WeatherMail.Checks
```

Uçtan uca test, PostgreSQL 18 programları ve Python 3 ile ayrı geçici bir
PostgreSQL kümesi, sahte hava sağlayıcısı ve yerel mail klasörü oluşturur.
Mevcut veritabanına bağlanmaz, gerçek mail göndermez. Giriş/kod tekrarı/kilitlenme,
cookie, abonelik, test maili, iptal ve zamanlanmış gönderim kontrollerini çalıştırır:

```powershell
dotnet build tests/WeatherMail.Checks/WeatherMail.Checks.csproj
python tests/test_weather_mail.py
```
