
using Microsoft.AspNetCore.SignalR.Client;

public class SignalRService
{
  public HubConnection? HubConnection;

  public async Task EnsureConnected()
  {
    string hubUrl = "http://localhost:5135/api/gameHub";
    if (HubConnection != null)
    {
      Console.WriteLine("Hub already connected, not reconnecting");
    }

    HubConnection = new HubConnectionBuilder()
      .WithUrl(hubUrl)
      .WithAutomaticReconnect()
      .Build();

    try
    {
      await HubConnection.StartAsync();
      Console.WriteLine("SignalR connection started.");
    }
    catch (Exception ex)
    {
      Console.WriteLine($"Error starting SignalR connection: {ex.Message}");
      throw;
    }
  }

  public HubConnection? GetConnection() => HubConnection;

  public async Task StopConnectionAsync()
  {
    if (HubConnection == null)
    {
      Console.WriteLine("Hub not connected, not disconnecting");
      return;
    }
    await HubConnection.StopAsync();
    HubConnection = null;
  }
}
