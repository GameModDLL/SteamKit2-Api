using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Steam_Nexus_API.Hubs;
using Steam_Nexus_API.Manager;
using Steam_Nexus_API.Services;

var builder = WebApplication.CreateBuilder(args);

// Gerekli Hizmetleri Ekleme (Dependency Injection)

// 1. SessionManager'ı tekil (Singleton) olarak ekliyoruz. Tüm uygulama tek bir yönetici kullanmalı.
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<FreePackageCacheService>();

// 2. SteamCallbackHost'u kalıcı arka plan hizmeti olarak ekliyoruz.
builder.Services.AddHostedService<SteamCallbackHost>();
builder.Services.AddHostedService<FreePackageCacheService>(sp => sp.GetRequiredService<FreePackageCacheService>());
// 3. API Kontrolcüler (Controllers) ve Swagger'ı ekleme
builder.Services.AddControllers();
builder.Services.AddSignalR(); // 👈 SignalR servisi eklenmeli
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 4. SignalR'ı ekleme (Web sitesiyle anlık iletişim için)
builder.Services.AddSignalR();
builder.Services.AddHttpClient<SteamWebAPIService>();
builder.Services.AddHttpClient(); // 👈 HttpClientFactory'yi ekler
//builder.Services.AddSingleton<FreePackageCacheService>(); // 👈 Yeni cache servisini ekler


var app = builder.Build();

// HTTP İstek İşlem Hattı (Middleware) Ayarları

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ⚠️ Güvenlik: Web sitesinden gelecek CORS istekleri için bu ayar gereklidir.
app.UseCors(policy => policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());

app.UseAuthorization();
app.MapHub<SteamHub>("/steamhub");
// 1. Kontrolcüleri (API uç noktalarını) haritalama
app.MapControllers();

// 2. SignalR Hub'ını haritalama (Buraya daha sonra SteamHub sınıfı gelecek)
// app.MapHub<SteamHub>("/steamhub"); 

app.Run();