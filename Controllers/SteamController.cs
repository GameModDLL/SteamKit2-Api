using Microsoft.AspNetCore.Mvc;
using Steam_Nexus_API.Models;
using Steam_Nexus_API.Services;

[ApiController]
[Route("api/[controller]")] // Uç nokta yolu: /api/steam
public class SteamController : ControllerBase
{
    private readonly SessionManager _sessionManager;
    private readonly FreePackageCacheService _cacheService; // 👈 Yeni
    // Dependency Injection (Bağımlılık Enjeksiyonu) ile SessionManager'ı alıyoruz
    public SteamController(SessionManager sessionManager, FreePackageCacheService cacheService)
    {
        _sessionManager = sessionManager;
        _cacheService = cacheService; // 👈 Atama
    }

    // Kullanıcı Giriş İsteğini İşleme Uç Noktası
    // POST /api/steam/login
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new { Message = "Kullanıcı adı ve şifre gereklidir." });
        }

        // 🛑 DÜZELTME 1: Fazladan Guid oluşturma satırı kaldırıldı.
        // GUID'nin oluşturulması ve Steam oturumuyla ilişkilendirilmesi StartNewSession içinde yapılmalıdır.

        // SessionManager'dan oturum başlatma sonucunu alınır.
        SessionStartResult startResult = _sessionManager.StartNewSession(
            request.Username,
            request.Password
        );

        // Oturum başlatılamazsa veya hata dönerse
        if (!startResult.Success)
        {
            // 400 Bad Request, başarısız giriş denemesini temsil eder.
            return BadRequest(new
            {
                Success = false,
                // 🛑 DÜZELTME 2: SessionId başarısız durumda boş Guid olacağı için göndermeye gerek yok.
                Message = startResult.Message ?? "Giriş denemesi başarısız oldu.",
                Status = startResult.Status // Genellikle "FAILURE"
            });
        }

        // Başarılı (SUCCESS) veya 2FA Gerekli (REQUIRES_2FA) durumu
        // Front-end'in 2FA akışını başlatması için bu yanıt önemlidir.
        return Ok(new
        {
            Success = true,
            SessionId = startResult.SessionId,
            Status = startResult.Status, // 🛑 Bu alanın büyük harfle 'Status' olduğundan emin olun!
            Message = startResult.Message
        });
    }

    // 2FA Kodu Gönderme Uç Noktası (Email veya Mobil Kod)
    // POST /api/steam/submitcode
    [HttpPost("submitcode")]
    public IActionResult SubmitCode([FromBody] SubmitCodeRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId) || string.IsNullOrEmpty(request.Code))
        {
            return BadRequest(new { Message = "Session ID ve Kod gereklidir." });
        }

        if (!Guid.TryParse(request.SessionId, out Guid sessionId))
        {
            return BadRequest(new { Message = "Geçersiz Session ID formatı." });
        }

        var session = _sessionManager.GetSession(sessionId);

        if (session == null)
        {
            return NotFound(new { Message = "Oturum bulunamadı veya süresi doldu." });
        }

        // Hangi kodun beklendiğine bağlı olarak metodu çağır
        if (session.NeedsCode)
        {
            session.SubmitAuthCode(request.Code);
            Console.WriteLine("2FA KOD GÖNDERİLDİ");
            return Ok(new { status = "REQUIRES_2FA", message = "Steam Guard kodu gerekiyor. Lütfen kodunuzu girin." });
        }
        else if (session.NeedsTwoFactor)
        {
            session.SubmitTwoFactorCode(request.Code);
            Console.WriteLine("2FA KOD GÖNDERİLDİ");
            return Ok(new { status = "REQUIRES_2FA", message = "Steam Guard kodu gerekiyor. Lütfen kodunuzu girin." });
        }
        else
        {
            return BadRequest(new { status = "SUCCESS", message = "Sistem Sizden Kod Beklemiyor" });
        }
    }
    [HttpGet("licenses")]
    public IActionResult GetLicenses([FromQuery] GetLicensesRequest request)
    {
        if (string.IsNullOrEmpty(request.SessionId))
        {
            return BadRequest(new { Message = "Session ID gereklidir." });
        }

        if (!Guid.TryParse(request.SessionId, out Guid sessionId))
        {
            return BadRequest(new { Message = "Geçersiz Session ID formatı." });
        }

        var session = _sessionManager.GetSession(sessionId);

        if (session == null)
        {
            return NotFound(new { Message = "Oturum bulunamadı veya süresi doldu." });
        }

        // Oturumun bağlı olup olmadığını kontrol edelim
        if (!session.IsConnected)
        {
            return StatusCode(409, new { Message = "Steam oturumu bağlı değil. Lütfen önce giriş yapın ve 2FA kodunu tamamlayın." });
        }

        // Lisans listesini döndür
        var licenses = session.GetOwnedLicenses();

        return Ok(new
        {
            SessionId = sessionId,
            LicenseCount = licenses.Count(),
            PackageIds = licenses // Paket ID'lerinin listesi
        });
    }
    // 📄 Controllers/SteamController.cs (AddFreeGames metodu)

    [HttpPost("addfreegames")]
    public IActionResult AddFreeGames([FromBody] GetLicensesRequest request)
    {
        // 🛑 CS0103 ve CS0246 HATA ÇÖZÜMÜ: sessionId'yi kapsam dışında tanımlayın.
        // 🛑 CS0215 HATA ÇÖZÜMÜ: TryParse metodu kullanılırken 'out' anahtar kelimesi ile atanır.
        Guid sessionId;

        // 1. GUID Kontrolü
        if (!Guid.TryParse(request.SessionId, out sessionId))
        {
            return BadRequest(new { Message = "Geçersiz Session ID formatı." }); // 🚀 CS0161 HATA ÇÖZÜMÜ
        }

        var session = _sessionManager.GetSession(sessionId);

        // 2. Oturum ve Bağlantı Kontrolü
        if (session == null || !session.IsLoggedIn)
        {
            return StatusCode(409, new { Message = "Oturum bulunamadı veya giriş yapılmadı." }); // 🚀 CS0161 HATA ÇÖZÜMÜ
        }

        // Yalnızca bir kez cache'ten çekelim
        var freePackagesInCache = _cacheService.GetFreePackages();

        // 🚀 KRİTİK KONTROL LOGU 🚀
        // Logger'ınızın adını doğru kullandığınızdan emin olun (_controllerLogger veya _logger)
        Console.WriteLine($"[CACHE KONTROL] Cache'ten Alınan Paket Sayısı: {freePackagesInCache.Count}");
        Console.WriteLine($"[CACHE KONTROL] Cache İçeriği: {string.Join(", ", freePackagesInCache)}");

        var packagesToAdd = new List<uint>();

        // 🛑 ZORLAYICI MANTIK: freePackagesInCache boş değilse, bu döngü kesinlikle çalışmalıdır.
        foreach (uint packageId in freePackagesInCache)
        {
            packagesToAdd.Add(packageId);
        }

        if (packagesToAdd.Count == 0)
        {
            // 🛑 BU HATA ALINIYORSA: Cache Service'de sorun var.
            return Ok(new
            {
                message = "HATA: Cache boş geliyor. FreePackageCacheService tekrar kontrol edilmeli.",
                cacheCount = freePackagesInCache.Count
            });
        }

        // 3. Ekleme ve Başarı Yanıtı (CS0161'in son parçasını tamamlar)
        session.AddFreeLicenses(packagesToAdd);

        return Ok(new
        {
            message = $"{packagesToAdd.Count} adet ücretsiz lisans ekleniyor.",
            addedPackageIds = packagesToAdd,
            ownedLicenseCount = session.GetOwnedLicenses().Count()
        });
    }
}