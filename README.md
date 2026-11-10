# Intern Backend İskeleti

Stajyerler için hazırlanmış .NET 9 Web API iskeleti. Veritabanı PostgreSQL'dir.
Katmanlar, adlar ve desenler şirketin ana backend'iyle aynıdır. Burada öğrendiğiniz yapı orada da geçerlidir.

İskelette üç örnek modül var. Yeni bir özelliği bu örneklerden birini kopyalayarak kurarsınız.

---

## 1. Katmanlar

```
  HTTP isteği
      │
      ▼
┌──────────────────┐  Controller: isteği alır, servisi çağırır, Response<T>'yi HTTP yanıtına çevirir.
│   Intern.API     │  Program.cs, ara katmanlar (middleware), DI kaydı (Modules/ServiceModule.cs).
└────────┬─────────┘
         ▼
┌──────────────────┐  İş kuralları. GenericService hazır CRUD verir.
│  Intern.Service  │  AutoMapper ile Entity ↔ DTO dönüşümü. Dış API çağrıları.
└────────┬─────────┘
         ▼
┌──────────────────┐  AppDbContext, Repository, UnitOfWork, Migration, tohum veri (SeedData).
│   Intern.Data    │
└────────┬─────────┘
         ▼
     PostgreSQL

 Ortak projeler (her katman kullanır):
   Intern.Entities  Entity (tablo) ve DTO (istek/yanıt) sınıfları
   Intern.Core      Arayüzler (IGenericRepository, IGenericService, IUnitOfWork), enum'lar, sayfalama
   Intern.Shared    Response<T>, ErrorDto, ayar sınıfları (Settings), istisnalar
```

Bağımlılık yönü tek yönlüdür: `API → Service → Data → Entities → Core → Shared`.
Alt katman üst katmanı tanımaz. Örneğin Data projesi Service projesine referans vermez.

---

## 2. Kurulum ve çalıştırma

1. .NET 9 SDK ve PostgreSQL kurun.
2. `Intern.API/.env.example` dosyasını aynı klasöre `.env` adıyla kopyalayın.
3. `.env` içindeki `DB_HOST`, `DB_PORT`, `DB_NAME`, `DB_USERNAME` ve `DB_PASSWORD` değerlerini kendi PostgreSQL bilgilerinize göre düzeltin.
   Alternatif olarak tek bir `CONNECTION_STRING` tanımlayabilirsiniz; tanımlıysa `DB_*` alanlarından önce kullanılır.
4. Depo kökünde çalıştırın:
   ```bash
   dotnet run --project Intern.API --launch-profile http
   ```
5. Tarayıcıda Swagger'ı açın: http://localhost:5100/swagger

Uygulama açılırken veritabanını kendisi oluşturur, migration'ları uygular ve tohum veriyi yazar.
Hızlı deneme için `Intern.API/Intern.API.http` dosyasındaki hazır istekleri kullanabilirsiniz.

> `.env` dosyası git'e girmez. Parola ve gizli anahtar yalnız bu dosyaya yazılır.

---

## 3. Örnek modüller

| Modül | Ne öğretir | Başlangıç dosyası |
|---|---|---|
| **Ability** | En basit örnek. GenericService'in hazır CRUD metodları. Tek satır kod yazmadan ekle/listele/güncelle/sil. | `Intern.API/Controllers/AbilityController.cs` |
| **Employee** | Özel servis metodu, sayfalama (`PagedList`), kodlu hata (`EmployeeNumberExists`), GenericService metodunu ezme (`override`). | `Intern.Service/Services/Concretes/EmployeeService.cs` |
| **ExchangeRate** | Veritabanı kullanmayan, dış bir API'yi (TCMB döviz kuru) çağıran özellik. `IHttpClientFactory`, yeniden deneme (retry), `IOptions` ayar sınıfı. | `Intern.Service/Services/Concretes/ExchangeRateService.cs` |

---

## 4. Yanıt zarfı: `Response<T>`

Her uç aynı biçimde yanıt döner. Ön yüz her zaman bu biçimi bekler.

Başarılı:
```json
{ "data": { "id": "…", "name": "CreateEmployee" }, "statusCode": 200 }
```

Beklenen hata (ör. aynı sicil numarası):
```json
{ "statusCode": 409,
  "error": { "errors": ["Bu sicil numarası zaten kullanılıyor."], "isShow": true, "code": "EmployeeNumberExists" } }
```

Kurallar:
1. Servis her zaman `Response<T>` döner. Başarıda `Response<T>.Success(data, 200)` kullanın.
2. Beklenen hatada `Response<T>.Fail("KodAdi", "Kullanıcıya gösterilecek mesaj", 4xx, true)` kullanın.
3. Controller yalnız `return ActionResultInstance(await _service.XAsync(...));` yazar.
4. Beklenmeyen hata için `try/catch` yazmayın. `GlobalExceptionHandlingMiddleware` hatayı yakalar, loglar ve 500 döner.

---

## 5. Yeni özellik ekleme

### 5A. Veritabanında tutulan özellik (Ability / Employee gibi)

1. **Entity** — `Intern.Entities/Concretes/<Ad>.cs`. `BaseEntity`'den türetin. `Id` alanını Ability'deki gibi yazın.
2. **DbSet** — `Intern.Data/AppDbContext.cs` içine `public DbSet<Ad> Adlar { get; set; }` ekleyin.
3. **DTO'lar** — `Intern.Entities/DTOs/<Ad>/` altına `<Ad>Dto` (yanıt) ve `Create<Ad>Dto` (istek) yazın.
4. **Eşleme** — `Intern.Service/AutoMapper/DtoMapper.cs` içine iki `CreateMap<…>().ReverseMap();` satırı ekleyin.
5. **Repository** — `Intern.Data/Repositories/Abstract/I<Ad>Repository.cs` ve `Concretes/<Ad>Repository.cs`.
   İkisi de boş kalabilir; `GenericRepository` her şeyi verir.
6. **Servis** — `Intern.Service/Services/Abstracts/I<Ad>Service.cs` ve `Concretes/<Ad>Service.cs`.
   `GenericService<Entity, Dto, CreateDto>`'dan türetin. Özel kural gerekiyorsa metot ekleyin ya da `override` edin.
7. **Controller** — `Intern.API/Controllers/<Ad>Controller.cs`. `BaseController`'dan türetin.
8. **Migration** — depo kökünde:
   ```bash
   dotnet ef migrations add <Ad>Eklendi --project Intern.Data --startup-project Intern.API
   ```
9. Uygulamayı yeniden başlatın. Migration açılışta kendiliğinden uygulanır.

> Sonu `Service` ya da `Repository` ile biten sınıf DI'a kendiliğinden kaydolur. `Program.cs`'e satır eklemeyin.

### 5B. Dış bir API'den veri çeken özellik (ExchangeRate gibi)

1. **Ayar sınıfı** — `Intern.Shared/Settings/<Ad>Settings.cs`. Adres, zaman aşımı gibi değerler burada durur.
   `appsettings.json` içine aynı adlı bölümü ekleyin. API anahtarı varsa `appsettings.json`'a değil `.env`'e yazın.
2. **Ayarı bağlayın** — `Program.cs` içinde `builder.Services.Configure<…Settings>(…)` satırını kopyalayın.
3. **HTTP istemcisi** — `Program.cs` içindeki `AddHttpClient(ExchangeRateService.HttpClientName, …)` bloğunu
   kopyalayın. Yeniden deneme ve zaman aşımı bu blokta tanımlıdır.
4. **DTO** — dış API'nin cevabını değil, sizin ön yüze döneceğiniz biçimi tanımlayın.
5. **Servis** — `IHttpClientFactory`, `IOptions<…Settings>` ve `ILogger<…>` alın.
   Dış API'ye ulaşılamazsa isteği düşürmeyin. ExchangeRate'teki gibi boş değer ve uyarı dönün, hatayı loglayın.
6. **Controller** — `CancellationToken` parametresini alıp servise geçirin.
7. **İsteğe bağlı: önbellek** — aynı veriyi her istekte dış API'den çekmeyin. `IMemoryCache` hazır kayıtlıdır.
8. **İsteğe bağlı: kayıt** — sonuçları ya da kullanıcı tercihlerini saklamak gerekiyorsa 5A adımlarını uygulayın.

---

## 6. Yazım kuralları

1. Silme işlemi kaydı silmez. `SoftDelete` durumu `Deleted` yapar. Listelerde `Status == Active` süzülür.
2. `CreatedBy`, `CreatedDate`, `ModifiedBy`, `ModifiedDate` alanlarını elle doldurmayın. `AppDbContext` doldurur.
3. Zaman için `DateTimeOffset.UtcNow` kullanın.
4. Log için `ILogger<T>` kullanın. `Console.WriteLine` kullanmayın.
5. Parola, bağlantı dizesi ve API anahtarını koda ya da `appsettings.json`'a yazmayın. `.env`'e yazın.
6. Okuma sorgularında `AsNoTracking()` kullanın.
7. Yetki: JWT doğrulaması `Program.cs`'te hazırdır. Bir uca `[Authorize]` eklerseniz o uç token ister.
   İskelette giriş (login) ucu yoktur; örnek uçlar bu yüzden açıktır.

---

## 7. Sık kullanılan komutlar

| İş | Komut |
|---|---|
| Derle | `dotnet build Intern.sln` |
| Çalıştır | `dotnet run --project Intern.API --launch-profile http` |
| Migration ekle | `dotnet ef migrations add <Ad> --project Intern.Data --startup-project Intern.API` |
| Son migration'ı geri al (uygulanmadıysa) | `dotnet ef migrations remove --project Intern.Data --startup-project Intern.API` |
| Migration'ı elle uygula | `dotnet ef database update --project Intern.Data --startup-project Intern.API` |
| EF aracını kur | `dotnet tool install --global dotnet-ef` |
