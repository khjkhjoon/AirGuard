using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AirGuard.Server
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("  AirGuard Server - 차량/드론 관제 서버");
            Console.WriteLine("===========================================\n");
            var server = new CommunicationServer();
            await server.StartAsync();
            Console.WriteLine("\n아무 키나 눌러 종료...");
            Console.ReadKey();
            server.Stop();
        }
    }

    public class CommunicationServer
    {
        private TcpListener? _tcpListener;
        private readonly List<TcpClient> _wpfClients = new();
        private readonly List<TcpClient> _unityClients = new();
        private bool _isRunning;
        private bool _mapRequested = false;
        private const int TCP_PORT = 9000;

        public async Task StartAsync()
        {
            _isRunning = true;
            _tcpListener = new TcpListener(IPAddress.Any, TCP_PORT);
            _tcpListener.Start();
            Console.WriteLine($"✅ TCP 서버 시작: 포트 {TCP_PORT}");
            _ = Task.Run(AcceptTcpClientsAsync);
            Console.WriteLine("대기 중...");
            await Task.Delay(-1);
        }

        public void Stop()
        {
            _isRunning = false;
            _tcpListener?.Stop();
            foreach (var c in _wpfClients) c.Close();
            foreach (var c in _unityClients) c.Close();
        }

        private async Task AcceptTcpClientsAsync()
        {
            while (_isRunning && _tcpListener != null)
            {
                try
                {
                    var client = await _tcpListener.AcceptTcpClientAsync();
                    Console.WriteLine($"📱 연결: {client.Client.RemoteEndPoint}");
                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (Exception ex)
                {
                    if (_isRunning) Console.WriteLine($"❌ 수락 오류: {ex.Message}");
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            var stream = client.GetStream();
            bool isWpf = false;

            try
            {
                // 첫 메시지로 클라이언트 판별
                string first = await ReadMessageAsync(stream);
                if (first == null) return;

                if (first.Contains("WPF_CLIENT"))
                {
                    isWpf = true;
                    lock (_wpfClients) _wpfClients.Add(client);
                    Console.WriteLine($"🖥️  WPF 등록 (총 {_wpfClients.Count}개)");
                }
                else
                {
                    lock (_unityClients) _unityClients.Add(client);
                    Console.WriteLine($"🎮 Unity 등록 (총 {_unityClients.Count}개)");

                    if (_mapRequested)
                    {
                        await Task.Delay(300);
                        await SendMessageAsync(stream, "MAP_REQUEST");
                        Console.WriteLine("📨 Unity → MAP_REQUEST 전달");
                    }

                    // 유니티 첫 메시지 처리
                    ProcessUnityMessage(first);
                }

                // 메인 루프
                while (_isRunning && client.Connected)
                {
                    string msg = await ReadMessageAsync(stream);
                    if (msg == null) break;

                    if (isWpf)
                    {
                        Console.WriteLine($"📨 WPF 명령: {msg}");
                        if (msg.Trim() == "MAP_REQUEST") _mapRequested = true;
                        ForwardToUnity(msg);
                    }
                    else
                    {
                        ProcessUnityMessage(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ 오류: {ex.Message}");
            }
            finally
            {
                if (isWpf)
                {
                    lock (_wpfClients) _wpfClients.Remove(client);
                    Console.WriteLine($"🖥️  WPF 해제");
                }
                else
                {
                    lock (_unityClients) _unityClients.Remove(client);
                    Console.WriteLine($"🎮 Unity 해제");
                }
                client.Close();
            }
        }

        private void ProcessUnityMessage(string msg)
        {
            if (msg.Contains("\"type\":\"map\"") || msg.Contains("originX"))
            {
                Console.WriteLine($"🗺️  맵 데이터 → WPF ({msg.Length}바이트)");
            }
            else
            {
                LogDroneData(msg);
            }
            BroadcastToWpf(msg);
        }

        // 길이 접두사 방식으로 메시지 읽기
        private static async Task<string?> ReadMessageAsync(NetworkStream stream)
        {
            byte[] lenBuf = new byte[4];
            if (!await ReadExactAsync(stream, lenBuf, 4)) return null;
            int len = BitConverter.ToInt32(lenBuf, 0);
            if (len <= 0 || len > 10 * 1024 * 1024) return null;

            byte[] body = new byte[len];
            if (!await ReadExactAsync(stream, body, len)) return null;
            return Encoding.UTF8.GetString(body);
        }

        // 길이 접두사 방식으로 메시지 쓰기
        private static async Task SendMessageAsync(NetworkStream stream, string msg)
        {
            byte[] body = Encoding.UTF8.GetBytes(msg);
            byte[] len = BitConverter.GetBytes(body.Length);
            await stream.WriteAsync(len, 0, len.Length);
            await stream.WriteAsync(body, 0, body.Length);
            await stream.FlushAsync();
        }

        private static async Task<bool> ReadExactAsync(NetworkStream stream, byte[] buf, int n)
        {
            int total = 0;
            while (total < n)
            {
                int read = await stream.ReadAsync(buf, total, n - total);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }

        private void ForwardToUnity(string msg)
        {
            lock (_unityClients)
            {
                var dead = new List<TcpClient>();
                foreach (var c in _unityClients)
                {
                    try { _ = SendMessageAsync(c.GetStream(), msg); }
                    catch { dead.Add(c); }
                }
                foreach (var c in dead) { _unityClients.Remove(c); c.Close(); }
            }
        }

        private void BroadcastToWpf(string msg)
        {
            lock (_wpfClients)
            {
                var dead = new List<TcpClient>();
                foreach (var c in _wpfClients)
                {
                    try { _ = SendMessageAsync(c.GetStream(), msg); }
                    catch { dead.Add(c); }
                }
                foreach (var c in dead) { _wpfClients.Remove(c); c.Close(); }
            }
        }

        private void LogDroneData(string json)
        {
            try
            {
                var d = JsonSerializer.Deserialize<VehicleData>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (d == null) return;
                Console.WriteLine($"✈️  [{d.VehicleId}] {d.Name} 배터리:{d.Battery:F1}%");
            }
            catch { }
        }
    }

    public class VehicleData
    {
        public string VehicleId { get; set; } = "";
        public string Name { get; set; } = "";
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public double Speed { get; set; }
        public double Battery { get; set; }
        public string Status { get; set; } = "Idle";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}