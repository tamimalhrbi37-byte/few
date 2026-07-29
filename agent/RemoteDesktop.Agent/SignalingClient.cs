using System.Net.WebSockets;
using System.Text;

namespace RemoteDesktop.Agent;

/// <summary>
/// عميل WebSocket بسيط يتصل بسيرفر الـ Relay ويرسل/يستقبل رسائل الإشارات (signaling)
/// الخاصة بـ WebRTC (offer / answer / ice-candidate) بالإضافة لأي رسائل تحكم أخرى.
/// </summary>
public class SignalingClient
{
    private readonly Uri _serverUri;
    private readonly ClientWebSocket _socket = new();

    public event Func<string, Task>? OnMessageReceived;

    public SignalingClient(string serverUrl, string pairCode, string role)
    {
        var baseUri = new Uri(serverUrl);
        var uriBuilder = new UriBuilder(baseUri)
        {
            Query = $"role={role}&pairCode={pairCode}"
        };
        _serverUri = uriBuilder.Uri;
    }

    public async Task ConnectAsync()
    {
        await _socket.ConnectAsync(_serverUri, CancellationToken.None);
        _ = Task.Run(ReceiveLoopAsync);
    }

    public async Task SendAsync(string message)
    {
        var bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[64 * 1024];

        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                if (OnMessageReceived != null)
                    await OnMessageReceived(message);
            }
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"انقطع الاتصال بالسيرفر: {ex.Message}");
            // TODO: أضف منطق إعادة الاتصال التلقائي (retry with backoff)
        }
    }
}
