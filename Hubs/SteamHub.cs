namespace Steam_Nexus_API.Hubs
{
    using Microsoft.AspNetCore.SignalR;
    using System.Threading.Tasks;

    public class SteamHub : Hub
    {
        // Bu metot istemci tarafından çağrılmaz, sadece tanım için buradadır.
        // Asıl mesaj gönderme işlemi IHubContext (SteamCallbackHost içinde) tarafından yapılır.
    }
}
