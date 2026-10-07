using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Lobby>();
builder.Services.AddSignalR();

var app = builder.Build();
app.UseStaticFiles(new StaticFileOptions
{
  ServeUnknownFileTypes = true,
  DefaultContentType = "application/octet-stream"
});

app.MapHub<LobbyHub>("/api/gameHub");
app.MapFallbackToFile("index.html");

app.Run();
