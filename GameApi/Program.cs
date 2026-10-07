using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddSingleton<Lobby>();
builder.Services.AddSignalR();

var app = builder.Build();
// Locally the frontend runs on its own dev server (:3000), a different origin from this API (:5135).
// The deployed image serves the frontend from here, so it never needs this
if (app.Environment.IsDevelopment())
  app.UseCors(policy => policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseStaticFiles(new StaticFileOptions
{
  ServeUnknownFileTypes = true,
  DefaultContentType = "application/octet-stream"
});

app.MapHub<LobbyHub>("/api/gameHub");
app.MapFallbackToFile("index.html");

app.Run();
