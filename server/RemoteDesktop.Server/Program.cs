using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// نخلي السيرفر يسمع على كل الشبكات (يفيد وقت النشر على استضافة سحابية)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5080); // http (للتجربة محلياً فقط - استخدم HTTPS خلف reverse proxy عند النشر)
});

var app = builder.Build();

app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30)
});

// =========================================================
// PairSessionManager: يربط كل "جهاز كمبيوتر" (Agent) بـ "جوال" (App)
// عن طريق كود اقتران (PairCode) يتفق عليه الطرفين مسبقاً.
// السيرفر هنا مجرد Relay - ما يفهم محتوى الرسائل، بس يمررها.
// =========================================================
var sessionManager = new PairSessionManager();

app.Map("/ws", async (HttpContext context) =>
{
    if (!context.WebSockets.IsWebSocketRequest)
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        return;
    }

    var role = context.Request.Query["role"].ToString();       // "agent" أو "app"
    var pairCode = context.Request.Query["pairCode"].ToString(); // كود الاقتران السري

    if (string.IsNullOrWhiteSpace(pairCode) || (role != "agent" && role != "app"))
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("role يجب أن يكون agent أو app، و pairCode مطلوب");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    await sessionManager.HandleConnectionAsync(pairCode, role, socket, context.RequestAborted);
});

app.MapGet("/", () => "Remote Desktop Relay Server - يعمل بنجاح ✅");

app.Run();

// =========================================================
// إدارة جلسات الاقتران والـ Relay بين الطرفين
// =========================================================
public class PairSession
{
    public WebSocket? AgentSocket { get; set; }
    public WebSocket? AppSocket { get; set; }
    public readonly SemaphoreSlim Lock = new(1, 1);
}

public class PairSessionManager
{
    private readonly ConcurrentDictionary<string, PairSession> _sessions = new();

    private PairSession GetOrCreate(string pairCode) =>
        _sessions.GetOrAdd(pairCode, _ => new PairSession());

    public async Task HandleConnectionAsync(string pairCode, string role, WebSocket socket, CancellationToken ct)
    {
        var session = GetOrCreate(pairCode);

        await session.Lock.WaitAsync(ct);
        try
        {
            if (role == "agent") session.AgentSocket = socket;
            else session.AppSocket = socket;
        }
        finally
        {
            session.Lock.Release();
        }

        Console.WriteLine($"[+] اتصال جديد: role={role}, pairCode={pairCode}");

        var buffer = new byte[64 * 1024]; // 64KB يكفي لرسائل SDP/ICE النصية

        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "إغلاق عادي", ct);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                // نمرر الرسالة للطرف الآخر فقط (agent <-> app)، السيرفر لا يفهم محتواها
                var target = role == "agent" ? session.AppSocket : session.AgentSocket;

                if (target is { State: WebSocketState.Open })
                {
                    var bytes = Encoding.UTF8.GetBytes(message);
                    await target.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
                }
            }
        }
        catch (WebSocketException)
        {
            // انقطاع اتصال - طبيعي عند إغلاق التطبيق أو فقدان الشبكة
        }
        finally
        {
            await session.Lock.WaitAsync(CancellationToken.None);
            try
            {
                if (role == "agent") session.AgentSocket = null;
                else session.AppSocket = null;
            }
            finally
            {
                session.Lock.Release();
            }

            Console.WriteLine($"[-] انقطع الاتصال: role={role}, pairCode={pairCode}");
        }
    }
}
