namespace Steam_Nexus_API.Services
{
    using Steam_Nexus_API.Models;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;

    public class SessionManager
    {
        // Thread-safe Dictionary kullanarak aktif oturumları tutar.
        private readonly ConcurrentDictionary<Guid, SteamSession> _activeSessions = new();

        // Yeni bir oturum başlatır
        public SessionStartResult StartNewSession(string username, string password)
        {
            Guid sessionId = Guid.NewGuid();
            var newSession = new SteamSession(username, password);
            _activeSessions.TryAdd(sessionId, newSession);
            newSession.Start();
            // 1. Yeni SteamSession nesnesi oluşturulur.
            // NOT: SteamSession sınıfınızın (SteamKit ile çalışan) Constructor'ı (yapıcısı)
            //      username ve password almalıdır.

            // 2. 🚀 KRİTİK DÜZELTME: Oturumu Dictionary'ye ekleyin!
            //    Böylece GetSession metodu daha sonra bu oturumu bulabilir.
            _activeSessions[sessionId] = newSession;

            // 3. Steam bağlantı işlemini başlatın (SteamSession içinde Login/Connect çağrılmalı)
            // newSession.ConnectAndLogin(); // Örnek çağrı

            // 4. İlk durumu kontrol edin ve döndürün (Bu durum, SteamSession nesnesinden gelmelidir)
            // Şimdilik varsayılan durumu döndürüyoruz, ancak bu SteamSession'dan gelmeli.
            return new SessionStartResult
            {
                Success = true,
                SessionId = sessionId,
                // Bu değer, SteamSession nesnesinin ilk bağlantı durumunu yansıtmalıdır.
                Status = "REQUIRES_2FA",
                Message = "Oturum başlatıldı. Steam Guard kodu bekleniyor."
            };
        }

        // ID ile oturumu bulur (SubmitCode metodu için gerekli)
        public SteamSession GetSession(Guid sessionId)
        {
            // 🚀 Bu mantık zaten doğruydu, şimdi kayıt edildiği için çalışacak.
            _activeSessions.TryGetValue(sessionId, out var session);
            return session;
        }

        // Oturumu sonlandırır
        public void StopSession(Guid sessionId)
        {
            if (_activeSessions.TryRemove(sessionId, out var session))
            {
                // session.Client.Disconnect(); 
                Console.WriteLine($"[MANAGER] Oturum sonlandırıldı. ID: {sessionId}");
            }
        }

        // Tüm aktif session'ları döndürür
        public IEnumerable<SteamSession> GetAllSessions()
        {
            return _activeSessions.Values.ToList();
        }

        // 🚀 SubmitCode metodu için SessionManager'a eklenmesi gereken fonksiyon
        public bool SubmitCode(Guid sessionId, string code)
        {
            var session = GetSession(sessionId);

            if (session == null)
            {
                // Oturum bulunamadı, Controller'a hata döndürülmeli.
                return false;
            }

            // NOT: SteamSession sınıfınızın içinde SteamClient'a kodu gönderen bir metot olmalıdır.
            return session.SubmitTwoFactorCode(code);
        }
    }
}