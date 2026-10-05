using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Lobby>();
builder.Services.AddSignalR();

var app = builder.Build();
app.UseStaticFiles();

app.MapHub<LobbyHub>("/api/gameHub");
app.MapFallbackToFile("index.html");

app.Run();
