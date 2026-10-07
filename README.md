# KYS

Bağımsız Kalite Yönetim Sistemi. API ve Blazor Web arayüzü aynı çözüm içinde ayrı projelerdir.

## Projeler

- `KYS/KYS`: .NET 10, Carter ve Entity Framework Core kullanan API.
- `KYS.Web`: .NET 10 Blazor Web App (Interactive Server).
- Blazor düzeni: `Components/Pages`, `Components/Layout`, `Shared`, `Models`, `Services`, `wwwroot`.

## Çalıştırma

API'nin mevcut `ConnectionStrings:SqlServer` ayarının doğru olması ve veritabanı migration'ının uygulanmış olması gerekir.

İki ayrı terminalde, çözüm klasöründen:

```powershell
dotnet run --project KYS/KYS --launch-profile Develoment
dotnet run --project KYS.Web --launch-profile http
```

- Giriş ekranı: http://localhost:5281/login
- API / Scalar: http://localhost:5280/scalar/v1
- Visual Studio'da birden fazla başlangıç projesi olarak `KYS` ve `KYS.Web` seçilebilir.
- API adresi değişirse `KYS.Web/appsettings.json` içindeki `Api:BaseUrl` güncellenir.

## Giriş

`user01` tablosunda bulunan, `IsActive = true` ve `IsDeleted = false` olan bir kullanıcıyla giriş yapılır. İlk kullanıcı mevcut `/users` CRUD API'sinden oluşturulabilir. Örnek veya otomatik yönetici hesabı eklenmez.

- `POST /auth/login`: kullanıcı adı/şifre kontrolü ve ASP.NET Core bearer erişim token'ı üretimi.
- `GET /auth/me`: bearer token ile mevcut aktif kullanıcıyı döndürür.
- Web projesinin `/auth/login` endpoint'i API'ye bağlanır ve HttpOnly oturum cookie'si oluşturur.
- Başarılı giriş ana sayfaya veya yerel `ReturnUrl` adresine yönlendirir.
- Beni hatırla seçilirse cookie kalıcı olur. Her iki durumda da oturum en fazla 8 saat geçerlidir.
- Çıkış formu oturum cookie'sini kaldırır. Formlar antiforgery token ile korunur.
- Yeni/güncellenen şifreler hash olarak saklanır. Önceden düz metin saklanan şifreler ilk başarılı girişte hash'e dönüştürülür. Kullanıcı liste/detay yanıtlarında `Password` bulunmaz.

Mevcut `/users` CRUD endpoint'lerinin anonim erişim davranışı korunmuştur. Kullanıcı yönetimi yayına alınmadan önce ayrıca yönetici yetkisiyle sınırlandırılmalıdır. Bu ilk adım giriş ekranı ve oturum akışını kapsar.

## Doğrulama

```powershell
dotnet run --project tests/KYS.AuthChecks
```

Kontroller API ve Web uygulamalarının gerçek başlangıç akışını kullanır. Kullanıcı verileri bellek içi test veritabanında tutulur; mevcut SQL Server veritabanına erişilmez. Hatalı/başarılı giriş, aktiflik, şifre hash geçişi, bearer token, oturum cookie'si, yerel yönlendirme ve antiforgery kontrolleri doğrulanır.
