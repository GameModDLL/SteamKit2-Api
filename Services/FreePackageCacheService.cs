namespace Steam_Nexus_API.Services
{
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class FreePackageCacheService : BackgroundService
    {
        private readonly ILogger<FreePackageCacheService> _logger;


        // Bellek içi önbellek. Bu liste, AddFreeGames metodu tarafından kullanılacak.

        private volatile HashSet<uint> _freePackageIds = new HashSet<uint>();
        public IReadOnlyCollection<uint> GetFreePackages() => _freePackageIds;
        private readonly SteamWebAPIService _webApiService; // 👈 Yeni alan
        public FreePackageCacheService(ILogger<FreePackageCacheService> logger, IConfiguration configuration, SteamWebAPIService webApiService)
        {
            // IHttpClientFactory artık doğrudan SteamWebAPIService'e enjekte ediliyor.
            _logger = logger;
            _webApiService = webApiService; // 👈 Atama
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Free Package Cache Service Başlatıldı.");

            try
            {
                // 🛑 KRİTİK: Servislerin tam olarak hazır olmasını beklemek için kısa bir gecikme ekliyoruz
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                _logger.LogWarning("Başlangıç Gecikmesi Tamamlandı. Cache Güncellemesi Başlıyor...");

                // İlk başlangıçta veriyi çek
                await UpdateCache();

                _logger.LogInformation("Cache İlk Yükleme Başarılı. Periyodik Güncelleme Döngüsü Başlatılıyor.");

                // Her gün (24 saatte bir) veriyi güncelle
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
                    await UpdateCache();
                }
            }
            catch (OperationCanceledException)
            {
                // Uygulama kapatıldığında beklenen iptal
                _logger.LogInformation("Free Package Cache Service İptal Edildi.");
            }
            catch (Exception ex)
            {
                // 🚀 KRİTİK: Herhangi bir hatayı yakalayıp loglayın.
                _logger.LogError(ex, "FATAL HATA: Free Package Cache Service Başlatılamadı veya Çalışma Sırasında Durdu. Bağımlılıkları kontrol edin (Örn: SteamWebAPIService).");
            }
        }

        private async Task UpdateCache()
        {
            _logger.LogInformation("Gerçek API üzerinden ücretsiz paketler taranıyor...");

            try
            {
                var freePackageIds = new HashSet<uint>();

                // 1. TÜM STEAM UYGULAMALARINI ÇEKME
                var allApps = await _webApiService.GetAllAppIdsAsync();
                _logger.LogInformation($"Steam'den {allApps.Count} adet uygulama ID'si alındı.");

                // 2. HER UYGULAMANIN FİYATINI KONTROL ETME
                // Steam'in fiyat API'si genellikle saniyede belirli sayıda istek sınırlar.
                // Bu yüzden, paketi almak için lisans API'sini kullanmadan önce fiyat kontrolünü atlayıp, 
                // doğrudan Steam API'sine paket lisanslarını soran daha hızlı bir metot kullanmak gerekebilir.

                // Şimdilik, sadece API'den gelen paketleri kullanmak için bu bölümü atlıyoruz
                // ve direkt lisans çekme API'sini kullanmaya odaklanıyoruz.

                // 🛑 ÖNEMLİ: Manuel test paketlerini temizliyoruz.
                // freePackageIds.Add(4294967200); // Test ID'si SİLİNDİ
                // freePackageIds.Add(377073);      // Test ID'si SİLİNDİ

                // Eğer GetFreeAppPackagesAsync metodunuz Steam API'den ücretsiz paket ID'lerini çekiyorsa:
                // var freePackages = await _webApiService.GetFreeAppPackagesAsync();
                // freePackageIds.UnionWith(freePackages);

                // Geçici olarak, sadece gerçek lisans çekme metodu üzerine yoğunlaşmak için 
                // bu API çağrılarını şimdilik bir kenara bırakıyoruz.

                if (freePackageIds.Count > 0)
                {
                    _freePackageIds = freePackageIds;
                    _logger.LogInformation($"Cache güncellendi. {freePackageIds.Count} adet ÜCRETSİZ lisans bulundu.");
                }
                else
                {
                    _logger.LogWarning("API taraması sonucunda güncel ücretsiz lisans bulunamadı.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ücretsiz paketleri tararken hata oluştu. Manuel test listesi de kullanılamadı.");
            }
        }
    }
}
