namespace Steam_Nexus_API.Manager
{
    using Microsoft.AspNetCore.SignalR; // 👈 Bu using'i ekleyin
    using Microsoft.Extensions.Hosting;
    using Steam_Nexus_API.Services;
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Steam_Nexus_API.Hubs;

    public class SteamCallbackHost : BackgroundService
    {
        private readonly SessionManager _manager;
        private readonly IHubContext<SteamHub> _hubContext; // SignalR için HubContext

        // Kurucu metot (Constructor) güncellendi:
        // IHubContext, Program.cs'deki builder.Services.AddSignalR(); sayesinde DI tarafından sağlanır.
        public SteamCallbackHost(SessionManager manager, IHubContext<SteamHub> hubContext)
        {
            _manager = manager;
            _hubContext = hubContext;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Console.WriteLine("[MANAGER] Steam Callback Host Başlatıldı...");

            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var session in _manager.GetAllSessions())
                {
                    session.RunCallbacks();

                    // 🚀 Durum Güncellemelerini Kontrol Etme ve Web Sitesine Gönderme

                    string statusMessage = string.Empty;

                    if (session.NeedsCode)
                    {
                        statusMessage = "2FA Kodu Gerekli (Email)";
                    }
                    else if (session.NeedsTwoFactor)
                    {
                        statusMessage = "2FA Kodu Gerekli (Mobil)";
                    }
                    else if (session.IsConnected)
                    {
                        // Bağlı ama kod beklemiyorsa, başarılı sayılabilir
                        statusMessage = "Giriş Başarılı veya Bağlantı Kuruluyor";
                    }

                    if (!string.IsNullOrEmpty(statusMessage))
                    {
                        // Web sitesindeki ReceiveStatus metodunu çağırır. 
                        // Session ID'si ile belirli bir kullanıcıya mesaj göndermek için Clients.User(sessionId.ToString()) kullanılabilir,
                        // ancak basit tutmak için Clients.All kullanıyoruz.
                        await _hubContext.Clients.All.SendAsync(
                            "ReceiveStatus",
                            session.SessionId.ToString(),
                            statusMessage
                        );
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(50), stoppingToken);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("[MANAGER] Steam Arka Plan Hizmeti Durduruluyor...");
            return base.StopAsync(cancellationToken);
        }
    }
}
