using HKMP_C;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading.Channels;

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});
builder.Services.AddHostedService<MainService>();
var app = builder.Build();
await app.RunAsync();


namespace HKMP_C
{
    public class MainService : BackgroundService
    {
        public ILogger Logger { get; }
        public TimeSpan? TimeOut { get; }
        public IPEndPoint LocalPoint { get; }
        public Uri Server { get; }
        record class ClientRecord(
            string Name,
            WebSocket WebSocket,
            Socket Socket,
            EndPoint EndPoint,
            Channel<Memory<byte>> SocketChannel);
        private SemaphoreSlim slim = new(1, 1);
        private Dictionary<EndPoint, ClientRecord> sockets = [];
        public MainService(IServiceProvider serviceProvider)
        {
            this.Logger = serviceProvider.GetRequiredService<ILogger<MainService>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>();
            var udpIP = Dns.GetHostAddresses(config["Main:Host"] ?? "127.0.0.1")[0];
            var udpPort = int.Parse(config["Main:Port"] ?? "8000");
            var timeout = int.Parse(config["Main:TimeOut"] ?? "-1");
            this.TimeOut = timeout > 0 ? TimeSpan.FromSeconds(timeout) : null;
            this.LocalPoint = new IPEndPoint(udpIP, udpPort);
            this.Server = new Uri(config["Main:Server"] ?? "ws://127.0.0.1:8000/ws");
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var udpServer = new Socket(LocalPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            udpServer.Bind(LocalPoint);
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpServer.IOControl((int)SIO_UDP_CONNRESET, [0], null);
            var remote = new IPEndPoint(LocalPoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            var buffer = new Memory<byte>(new byte[65535]);
            while (!stoppingToken.IsCancellationRequested)
            {
                var receiveResult = await udpServer.ReceiveFromAsync(buffer, remote, stoppingToken);
                if (receiveResult.ReceivedBytes <= 0)
                {
                    throw new Exception($"[{remote}]UDP接收到0数据！");
                }
                try
                {
                    await DealReceiveAsync(udpServer, receiveResult.RemoteEndPoint, buffer[0..receiveResult.ReceivedBytes], stoppingToken);
                }
                catch (Exception ex)
                {
                    await SendToMonitorAsync($"[{remote}]UDP接收处理异常：{ex}！", CancellationToken.None);
                }
            }
            udpServer.Close();
        }

        private async ValueTask DealReceiveAsync(Socket udpServer, EndPoint remoteEndPoint, Memory<byte> memory, CancellationToken stoppingToken)
        {
            ClientRecord? record;
            var name = $"{remoteEndPoint}";
            await slim.WaitAsync(stoppingToken);
            try
            {
                if (!sockets.TryGetValue(remoteEndPoint, out record))
                {
                    var newWebSocket = new ClientWebSocket();
                    try
                    {
                        await newWebSocket.ConnectAsync(this.Server, stoppingToken);
                        var newRecord = new ClientRecord(
                            name,
                            newWebSocket,
                            udpServer,
                            remoteEndPoint,
                            Channel.CreateBounded<Memory<byte>>(new BoundedChannelOptions(100) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true })
                            );
                        sockets.Add(remoteEndPoint, newRecord);
                        StartLoop(newRecord, stoppingToken);
                        record = newRecord;
                    }
                    catch (Exception ex)
                    {
                        await SendToMonitorAsync($"[{name}]异常：{ex}！", CancellationToken.None);
                        newWebSocket.Dispose();
                        return;
                    }
                }
            }
            finally
            {
                slim.Release();
            }
            await record!.SocketChannel.Writer.WriteAsync(memory.ToArray(), stoppingToken);
        }

        private async void StartLoop(ClientRecord record, CancellationToken token)
        {
            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(token);
            var name = record.Name;
            var websocket = record.WebSocket;
            var remote = record.EndPoint;
            try
            {
                await SendToMonitorAsync($"[{name}]连接！", CancellationToken.None);
                var task1 = WebSocketLoop(record, tokenSource.Token);
                var task2 = SocketLoop(record, tokenSource.Token);
                await Task.WhenAny(task1, task2).Unwrap();
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"[{name}]异常：{ex}！", CancellationToken.None);
            }
            finally
            {
                await SendToMonitorAsync($"[{name}]断开！", CancellationToken.None);
                record.SocketChannel.Writer.TryComplete();
                if (websocket.State == WebSocketState.Open)
                {
                    try
                    {
                        await websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                    }
                    catch { }
                }
                websocket.Dispose();
                try
                {
                    tokenSource.Cancel();
                }
                catch { }
                await slim.WaitAsync(token);
                try
                {
                    sockets.Remove(remote);
                }
                finally
                {
                    slim.Release();
                }

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
            var websocket = record.WebSocket;
            var socket = record.Socket;
            var remote = record.EndPoint;
            var reader = record.SocketChannel.Reader;
            {
                if (TimeOut is TimeSpan newTs)
                {
                    cts.CancelAfter(newTs);
                }
            }
            try
            {
                await foreach (var memory in reader.ReadAllAsync(cts.Token))
                {
                    if (TimeOut is TimeSpan newTs)
                    {
                        cts.CancelAfter(newTs);
                    }
                    await websocket.SendAsync(memory, WebSocketMessageType.Binary, true, cts.Token);
                    await SendToMonitorAsync($"[{name}]UDP->WS:{BitConverter.ToString(memory.ToArray())}", CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                await SendToMonitorAsync($"[{name}]UDP->WS异常:{ex.Message}", CancellationToken.None);
            }
        }

        private ValueTask SendToMonitorAsync(string message, CancellationToken token)
        {
            Logger.LogInformation("{message}", message);
            return ValueTask.CompletedTask;
        }
    }
}