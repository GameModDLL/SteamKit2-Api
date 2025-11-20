using SteamKit2;
using SteamKit2.Internal;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;

public class SteamSession
{
    public Guid SessionId { get; } = Guid.NewGuid();

    private readonly SteamClient client;
    private readonly SteamUser user;
    private readonly CallbackManager manager;
    private readonly string username;
    private readonly string password;

    // Oturum Durumları
    public bool IsConnected { get; private set; } = false;
    public bool IsLoggedIn { get; private set; } = false;
    public bool NeedsCode { get; private set; } = false;
    public bool NeedsTwoFactor { get; private set; } = false;
    public bool LoginInProgress { get; private set; } = false; // Bağlantı döngüsünü kontrol eder

    // Kodlar bir sonraki LogOn için bekletilir
    private string pendingAuthCode = null;
    private string pendingTwoFactorCode = null;

    private HashSet<uint> OwnedPackageIds { get; set; } = new HashSet<uint>();

    // Arkaplan callback döngüsü için görev
    private Task callbackTask;
    private System.Threading.CancellationTokenSource cts = new();

    public SteamSession(string username, string password)
    {
        this.username = username;
        this.password = password;

        var config = SteamConfiguration.Create(builder => { });
        client = new SteamClient(config);
        user = client.GetHandler<SteamUser>();
        manager = new CallbackManager(client);

        // Gerekli Callback'lere Abone Olma
        manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);
        manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);
        manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);
        manager.Subscribe<SteamApps.LicenseListCallback>(OnLicenseList);
        // Daha fazlası eklenebilir: (RequestFreeLicense için LicenseListCallback'e abone olmak yeterlidir)
    }

    // 🛑 DÜZELTME 1: Bağlantı ve callback döngüsünü başlatır
    public void Start()
    {
        if (LoginInProgress) return; // Zaten çalışıyorsa tekrar başlatma

        Console.WriteLine($"[{username} - {SessionId.ToString().Substring(0, 4)}] Oturum Başlatılıyor...");
        LoginInProgress = true;

        // Callback döngüsünü ayrı bir görevde başlat
        callbackTask = Task.Run(() => RunCallbacksLoop(cts.Token));

        // Bağlantıyı başlat
        client.Connect();
    }

    // 🛑 DÜZELTME 3: Güvenli çıkış için metot
    public void Disconnect()
    {
        // Callback döngüsünü iptal et
        cts.Cancel();

        // Steam bağlantısını kes
        if (client.IsConnected)
        {
            client.Disconnect();
        }
        IsConnected = false;
        IsLoggedIn = false;
        LoginInProgress = false;
        Console.WriteLine($"[{username}] Oturum Kapatıldı ve Bağlantı Kesildi.");
    }

    // 🛑 DÜZELTME 2: 2FA Kodunu gönderen mantık (mevcut bağlantıyı keser)
    public bool SubmitTwoFactorCode(string code)
    {
        if (!LoginInProgress) return false;

        pendingTwoFactorCode = code;
        NeedsTwoFactor = false;

        Console.WriteLine($"[{username}] Mobil Kod Alındı. Yeniden Bağlanma Deneniyor...");

        // Kodu LogOn'a göndermek için bağlantıyı kesip tekrar bağlayın
        client.Disconnect();
        // Disconnect Callback'i çalışacak ve ardından OnConnected, yeni kodu kullanarak LogOn'u deneyecek.
        client.Connect();

        return true;
    }

    // AuthCode için benzer mantık
    public bool SubmitAuthCode(string code)
    {
        if (!LoginInProgress) return false;

        pendingAuthCode = code;
        NeedsCode = false;
        client.Disconnect();
        client.Connect();

        return true;
    }

    // Callback Metotları

    private void OnConnected(SteamClient.ConnectedCallback callback)
    {
        IsConnected = true;
        Console.WriteLine($"[{username}] -> Steam Ağına Bağlandı. Giriş Deneniyor...");

        var details = new SteamUser.LogOnDetails
        {
            Username = username,
            Password = password,
            AuthCode = pendingAuthCode,
            TwoFactorCode = pendingTwoFactorCode,
        };

        user.LogOn(details);

        // Kodlar kullanıldıktan sonra temizlenmeli
        pendingAuthCode = null;
        pendingTwoFactorCode = null;
    }

    private void OnDisconnected(SteamClient.DisconnectedCallback callback)
    {
        IsConnected = false;
        IsLoggedIn = false; // Bağlantı kesilince oturum da kapanmıştır

        // Kodu beklerken bağlantı kesilirse, yeniden bağlanmayı denememeliyiz.
        if (!NeedsCode && !NeedsTwoFactor)
        {
            Console.WriteLine($"[{username}] Bağlantı Kesildi.");
        }
    }
    public void RunCallbacks() // <--- Bu metodu yeniden ekleyin
     {
         manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
     }
    private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            IsLoggedIn = true;
            NeedsCode = false;
            NeedsTwoFactor = false;
            Console.WriteLine($"[{username}] ✅ Giriş Başarılı. Lisanslar çekiliyor...");

            // Lisansları hemen talep et (LicenseListCallback'i tetiklemek için)
            var steamApps = client.GetHandler<SteamApps>();

            // 🚀 DÜZELTME: packageIds koleksiyonunu RequestFreeLicense metoduna argüman olarak verin.
            // Metot, 'uint[]' (uint dizisi) bekler.
            steamApps.RequestFreeLicense([]);
        }
        else if (callback.Result == EResult.AccountLogonDenied)
        {
            client.Disconnect();
            NeedsCode = true;
            Console.WriteLine($"[{username}] ⚠️ 2FA Kodu (Email) Gerekli. Web'e bildiriliyor.");
        }
        else if (callback.Result == EResult.AccountLoginDeniedNeedTwoFactor)
        {
            client.Disconnect();
            NeedsTwoFactor = true;
            Console.WriteLine($"[{username}] 📱 2FA Kodu (Mobil) Gerekli. Web'e bildiriliyor.");
        }
        else
        {
            Console.WriteLine($"[{username}] ❌ Giriş Başarısız: {callback.Result}");
            client.Disconnect();
            LoginInProgress = false; // Başarısız giriş, döngüyü sonlandır
        }

    }

    private void OnLicenseList(SteamApps.LicenseListCallback callback)
    {
        if (callback.Result == EResult.OK)
        {
            OwnedPackageIds.Clear();
            foreach (var license in callback.LicenseList)
            {
                OwnedPackageIds.Add(license.PackageID);
            }
            Console.WriteLine($"[{username}] Kütüphane önbelleği güncellendi. {OwnedPackageIds.Count} lisans kayıtlı.");
        }
        else
        {
            Console.WriteLine($"[{username}] Lisans listesi alınamadı: {callback.Result}");
        }
    }

    // Callback Döngüsü
    private async Task RunCallbacksLoop(System.Threading.CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            manager.RunWaitCallbacks(TimeSpan.FromMilliseconds(100));
            // CPU kullanımını azaltmak için kısa bir gecikme
            await Task.Delay(50, token);
        }
    }

    public IEnumerable<uint> GetOwnedLicenses()
    {
        return OwnedPackageIds.ToList();
    }

    public void AddFreeLicenses(IEnumerable<uint> packageIds)
    {
        // ... (Kodunuz aynı kalabilir) ...
        if (!client.IsConnected || !IsLoggedIn)
        {
            Console.WriteLine($"[{username}] ❌ Lisans eklenemedi: Oturum bağlı değil veya giriş başarılı değil.");
            return;
        }

        Console.WriteLine($"[{username}] 🛒 {packageIds.Count()} adet ücretsiz lisans ekleniyor...");
        var steamApps = client.GetHandler<SteamApps>();
        steamApps.RequestFreeLicense(packageIds.ToArray());
    }
}