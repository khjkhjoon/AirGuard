using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AirGuard.WPF.Services
{
    public class TcpClientService : IDisposable
    {
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private bool _isConnected;

        public event Action<string>? MessageReceived;
        public event Action? Disconnected;
        public event Action<string>? ErrorOccurred;

        public bool IsConnected => _isConnected;

        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();

            // 핸드셰이크
            await SendAsync("WPF_CLIENT");
            _isConnected = true;
            _ = Task.Run(ReceiveDataAsync);

            // 맵 요청
            await Task.Delay(300);
            await SendAsync("MAP_REQUEST");
        }

        public async Task SendAsync(string message)
        {
            if (_stream == null) return;
            try
            {
                byte[] body = Encoding.UTF8.GetBytes(message);
                byte[] len = BitConverter.GetBytes(body.Length);
                await _stream.WriteAsync(len, 0, len.Length);
                await _stream.WriteAsync(body, 0, body.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
        }

        private async Task ReceiveDataAsync()
        {
            try
            {
                while (_isConnected && _stream != null)
                {
                    // 4바이트 길이 읽기
                    byte[] lenBuf = new byte[4];
                    if (!await ReadExactAsync(lenBuf, 4)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 10 * 1024 * 1024) break;

                    // 본문 읽기
                    byte[] body = new byte[msgLen];
                    if (!await ReadExactAsync(body, msgLen)) break;

                    string msg = Encoding.UTF8.GetString(body);
                    MessageReceived?.Invoke(msg);
                }
            }
            catch { }
            finally
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    Disconnected?.Invoke();
                }
            }
        }

        private async Task<bool> ReadExactAsync(byte[] buf, int n)
        {
            int total = 0;
            while (total < n && _stream != null)
            {
                int read = await _stream.ReadAsync(buf, total, n - total);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }

        public void Dispose() => Disconnect();
    }
}