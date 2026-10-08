# ERP MVC giriş ve kayıt

Giriş/kayıt ekranları `ERP.Api` içine ASP.NET Core MVC olarak eklendi. Kullanıcı/parola modeli `Models`, SQL erişimi `Data`, HTTP akışı `Controllers`, ekranlar `Views` altındadır.

## Yerelde başlatma

`ERP` çözüm klasöründe PowerShell açın:

```powershell
dotnet restore .\ERP.Api\ERP.Api.csproj
dotnet run --project .\ERP.Api\ERP.Api.csproj --launch-profile http
```

Bağlantı `appsettings.Development.json` içindeki `ConnectionStrings:DefaultConnection` ayarından okunur. Sunucu başladıktan sonra Chrome'da `http://localhost:5038` adresini açın. Ekran görüntüsündeki `ERP.Web` eski .NET Framework 4.7.2 projesidir; MVC ekranları .NET 10 ile çalışan `ERP.Api` projesinde sunulur.

`Database/InitialTables.sql` şemayı başka bir veritabanına kurmak veya eksik tabloları oluşturmak için idempotent başlangıç scriptidir. STOK modülü için bu scripti kullandığınız veritabanında çalıştırın; `dbo.StockItems` tablosunu oluşturur ve `STOK` sekmesini ekler. `Tables` tablosuna STOK kaydını ayrıca eklediyseniz tekrar eklenmez.

Kayıt formu parolayı ASP.NET Core `PasswordHasher` ile hashler ve hash değerini `UserTable.[Password]` alanında saklar. Girişte `AktifPasif` kontrol edilir. E-postası `WhoIsAdmin.Eposta` listesinde bulunan aktif kullanıcı Admin rolünü alır. Admin, `/Admin/Permissions` ekranında kullanıcı ve tablo bazında Okuma, Yazma, Silme ve Önizleme izinlerini dağıtır. Admin menüsü ve endpoint'i Admin rolüyle korunur; diğer kullanıcılar erişim reddi sayfasını görür.

STOK, STOK RAPORU, AMBALAJ REÇETESİ, ÜRETİM KAYDI, DEPO TRANSFERİ ve STOK HAREKETLERİ sekmelerinin Okuma ve Yazma izinleri yönetici tarafından Yetki yönetimi ekranından verilir. Stok kayıtları ürün cinsi (Hammadde/Mamul), Adet/Kg/Metre/Litre birimi, depo ve düşük stok eşiğiyle tutulur. Stok raporu depo/ürün cinsi özetini, eşik altı kayıtları ve tarih aralığındaki hareketleri gösterir. Ambalaj reçeteleri cins ve birime göre filtrelenir; her reçete ambalaj içi miktar ve paletteki ambalaj adedini içerir. Üretim kaydı mamul stok miktarını artırır, üretim kaydını ve ilgili stok girişini tutar; hammadde tüketimini henüz düşmez. Depo transferi kaynak stoğu azaltıp hedef depoya ekler ve işlemi `dbo.StockTransfers` tablosunda kaydeder; iki yönlü stok hareketi de `dbo.StockMovements` tablosunda tutulur. İlk stok miktarı, manuel giriş ve çıkışlar da hareket geçmişine kaydedilir.

Tek şirket ve fabrika profili yönetici tarafından Ayarlar sayfasında düzenlenir; ayrı bir menü sekmesi yoktur. Profil bilgileri `dbo.CompanyProfile` tablosunda tek kayıt olarak tutulur. Müşteri kartları `dbo.Customers` tablosunda müşteri kodu, unvan, iletişim/adres ve aktiflik bilgileriyle saklanır.

> Kayıt şu anda açık olduğundan, canlı kullanıma geçmeden önce şirket daveti/yönetici onayı ekleyin ve yalnızca HTTPS kullanın.
