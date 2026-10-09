using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using GameFrontend;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var hubUrl = builder.HostEnvironment.IsDevelopment()
    ? "http://localhost:5135/api/gameHub"
    : new Uri(new Uri(builder.HostEnvironment.BaseAddress), "api/gameHub").ToString();
builder.Services.AddSingleton(new SignalRService(hubUrl));
builder.Services.AddScoped<ProfileService>();
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var host = builder.Build();
DebugBridge.Service = host.Services.GetRequiredService<SignalRService>();
await host.RunAsync();
