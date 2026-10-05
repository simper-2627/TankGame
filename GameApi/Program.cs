using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<Lobby>();
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
  opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
      ["application/octet-stream", "application/wasm"]);
});

var app = builder.Build();
app.UseResponseCompression();
app.UseStaticFiles();

app.MapHub<LobbyHub>("/api/gameHub");
app.MapFallbackToFile("index.html");

app.Run();
