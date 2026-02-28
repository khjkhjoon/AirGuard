using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AirGuard.Network
{
    /// <summary>
    /// UDP 클라이언트 구현
    /// </summary>
    public class UdpNetworkClient : INetworkClient
    {
        private UdpClient _client;
        private IPEndPoint _serverEndPoint;
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public event Action<string> OnStatusChanged;
        public event Action<string> OnDataReceived;
        public event Action<Exception> OnError;

        public Task<bool> ConnectAsync(string host, int port)
        {
            try
            {
                _client = new UdpClient();
                _serverEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
                _isConnected = true;

                OnStatusChanged?.Invoke($"UDP client ready for {host}:{port}");
                return Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnError?.Invoke(ex);
                OnStatusChanged?.Invoke($"UDP setup failed: {ex.Message}");
                return Task.FromResult(false);
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _client?.Close();
            OnStatusChanged?.Invoke("UDP client closed");
        }

        public Task SendAsync(string data)
        {
            if (!_isConnected || _client == null)
            {
                OnError?.Invoke(new InvalidOperationException("Not connected"));
                return Task.CompletedTask;
            }

            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(data);
                _client.Send(bytes, bytes.Length, _serverEndPoint);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex);
                return Task.CompletedTask;
            }
        }
    }
}