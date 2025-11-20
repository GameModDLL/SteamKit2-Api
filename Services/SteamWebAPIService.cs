namespace Steam_Nexus_API.Services
{
    using System.Net.Http;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Collections.Generic;
    using System.Threading;
    using Microsoft.Extensions.Logging;

    public class SteamWebAPIService
    {
        public class AppIdEntry
        {
            public uint AppId { get; set; }
            public string Name { get; set; }
        }

        private readonly HttpClient _httpClient;
        private readonly ILogger<SteamWebAPIService> _logger;

        public SteamWebAPIService(HttpClient httpClient, ILogger<SteamWebAPIService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }
        public class PriceCheckResult
        {
            public bool IsFree { get; set; }
            public uint? PackageId { get; set; } // Package ID (Lisans ID)
        }

        
        public async Task<List<AppIdEntry>> GetAllAppIdsAsync()
        {
            string url = $"https://partner.steam-api.com/ISteamApps/GetPartnerAppListForWebAPIKey/v2/?key=59725A917A9585EA8AF44104FAE83BAE";

            try
            {
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode(); // 👈 Başarısızsa exception fırlatır.

                // 🚀 KONTROL LOGU EKLE
                _logger.LogInformation($"Steam AppList API'den başarılı yanıt alındı. Durum kodu: {response.StatusCode}");

                var json = await response.Content.ReadAsStringAsync();
            var appList = new List<AppIdEntry>(); // 👈 Buraya taşıdık.
        
                // ... (HTTP isteği ve JSON işleme kodları)

                // Başarılı olursa listeyi döndürür.
                return appList;
            }
            catch (Exception ex) // 👈 Hata yakalandığında
            {
                _logger.LogError(ex, "App ID listesi çekilirken hata oluştu.");
   

                return new List<AppIdEntry>();
            }


        }
    }
}
