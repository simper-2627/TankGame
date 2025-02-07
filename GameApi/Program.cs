using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.SignalR;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors();
builder.Services.AddSingleton<Lobby>();
builder.Services.AddSignalR();
builder.Services.AddResponseCompression(opts =>
{
  opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
      ["application/octet-stream"]);
});

var app = builder.Build();
app.UseCors(policy =>
  policy
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader()
);
app.MapGet("/", () => "Hello World!");

app.MapHub<LobbyHub>("/api/gameHub");

app.Run();
