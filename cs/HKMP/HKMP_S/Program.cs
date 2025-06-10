using HKMP_S;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;

var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
{
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddHostedService<MainService>();
builder.WebHost.UseIIS().UseIISIntegration();
var app = builder.Build();
app.UseWebSockets();
MainService? mainService = null;
app.Map("/ws", async context =>
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("请使用Websocket请求！");
            return;
        }
        var websocket = await context.WebSockets.AcceptWebSocketAsync();
        mainService ??= (MainService)context.RequestServices.GetServices<IHostedService>().First(s => s is MainService);
        await mainService.DealWebsocketAsync($"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}", websocket, context.RequestAborted);
    }
);
await app.RunAsync();



namespace HKMP_S
{
    public class MainService : BackgroundService
    {
        public ILogger Logger { get; }
        public TimeSpan? TimeOut { get; }
        public IPEndPoint RemotePoint { get; }
        record class ClientRecord(
            string Name,
            WebSocket WebSocket,
            Socket Socket,
            EndPoint EndPoint);

        public MainService(IServiceProvider serviceProvider)
        {
            this.Logger = serviceProvider.GetRequiredService<ILogger<MainService>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var udpIP = Dns.GetHostAddresses(config["Main:Host"] ?? "127.0.0.1")[0];
            var udpPort = int.Parse(config["Main:Port"] ?? "8000");
            var timeout = int.Parse(config["Main:TimeOut"] ?? "-1");
            this.TimeOut = timeout > 0 ? TimeSpan.FromSeconds(timeout) : null;
            this.RemotePoint = new IPEndPoint(udpIP, udpPort);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(-1, stoppingToken);
        }

        internal async Task DealWebsocketAsync(string name, WebSocket websocket, CancellationToken token)
        {
            using var newSocket = new Socket(RemotePoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            try
            {
                await newSocket.ConnectAsync(RemotePoint, token);
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"[{name}]连接异常:{ex}", CancellationToken.None);
                newSocket.Dispose();
                return;
            }

            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            try
            {
                await SendToMonitorAsync($"[{name}]连接！", CancellationToken.None);
                var record = new ClientRecord(name, websocket, newSocket, RemotePoint);
                var task1 = SocketLoop(record, tokenSource.Token);
                var task2 = WebSocketLoop(record, tokenSource.Token);
                await Task.WhenAny(task1, task2).Unwrap();
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"[{name}]异常：{ex}！", CancellationToken.None);
            }
            finally
            {
                await SendToMonitorAsync($"[{name}]断开！", CancellationToken.None);
                try
                {
                    newSocket.Close();
                }
                catch { }
                newSocket.Dispose();
                try
                {
                    tokenSource.Cancel();
                }
                catch { }
                try
                {
                    if (websocket.State == WebSocketState.Open)
                    {
                        await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default);
                    }
                    websocket.Dispose();
                }
                catch { }
            }

        }
        private async Task WebSocketLoop(ClientRecord record, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var name = record.Name;
            var webSocket = record.WebSocket;
            var socket = record.Socket;
            var remote = record.EndPoint;
            if (TimeOut is TimeSpan span)
            {
                cts.CancelAfter(span);
            }
            try
            {
                var buffer = new Memory<byte>(new byte[65535]);
                while (!cts.IsCancellationRequested)
                {
                    var result = await webSocket.ReceiveAsync(buffer, cts.Token);
                    if (result.Count <= 0)
                    {
                        throw new Exception($"[{name}]WS接收到0数据：{result.MessageType}");
                    }
                    if (TimeOut is TimeSpan ts)
                    {
                        cts.CancelAfter(ts);
                    }
                    var memory = buffer[0..result.Count];
                    await socket.SendToAsync(memory, remote, cts.Token);
                    await SendToMonitorAsync($"WS->UDP[{name}]{BitConverter.ToString(memory.ToArray())}", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"WS->UDP异常[{name}]{ex}", CancellationToken.None);
            }
        }
        private async Task SocketLoop(ClientRecord record, CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
            var name = record.Name;
            var webSocket = record.WebSocket;
            var socket = record.Socket;
            var remote = record.EndPoint;
            {
                if (TimeOut is TimeSpan newTs)
                {
                    cts.CancelAfter(newTs);
                }
            }
            try
            {
                var buffer = new Memory<byte>(new byte[65535]);
                while (!cts.IsCancellationRequested)
                {
                    var result = await socket.ReceiveFromAsync(buffer, remote, cts.Token);
                    if (result.ReceivedBytes <= 0)
                    {
                        throw new Exception($"[{name}]UDP接收到0数据!");
                    }
                    if (TimeOut is TimeSpan ts)
                    {
                        cts.CancelAfter(ts);
                    }
                    var memory = buffer[0..result.ReceivedBytes];
                    await webSocket.SendAsync(memory, WebSocketMessageType.Binary, true, cts.Token);
                    await SendToMonitorAsync($"UDP->WS[{name}]{BitConverter.ToString(memory.ToArray())}", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"UDP->WS异常[{name}]{ex}", CancellationToken.None);
            }
        }

        private ValueTask SendToMonitorAsync(string message, CancellationToken token)
        {
            Logger.LogInformation("{message}", message);
            return ValueTask.CompletedTask;
        }
    }
}