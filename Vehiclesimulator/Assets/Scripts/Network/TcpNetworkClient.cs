using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace AirGuard.Network
{
    /// <summary>
    /// TCP 클라이언트 — 길이 접두사 프로토콜
    /// [4바이트 길이][메시지 본문]
    /// </summary>
    public class TcpNetworkClient : INetworkClient
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private CancellationTokenSource _cts;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public event Action<string> OnStatusChanged;
        public event Action<string> OnDataReceived;
        public event Action<Exception> OnError;

        public async Task<bool> ConnectAsync(string host, int port)
        {
            try
            {
                _client = new TcpClient();
                await _client.ConnectAsync(host, port);
                _stream = _client.GetStream();
                _isConnected = true;
                OnStatusChanged?.Invoke($"Connected to {host}:{port}");
                _cts = new CancellationTokenSource();
                _ = ReceiveDataAsync(_cts.Token);
                return true;
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnError?.Invoke(ex);
                return false;
            }
        }

        public void Disconnect()
        {
            _isConnected = false;
            _cts?.Cancel();
            _stream?.Close();
            _client?.Close();
            OnStatusChanged?.Invoke("Disconnected");
        }

        public async Task SendAsync(string data)
        {
            if (!_isConnected || _stream == null) return;
            try
            {
                byte[] body = Encoding.UTF8.GetBytes(data);
                byte[] len = BitConverter.GetBytes(body.Length); // 4바이트 길이
                await _stream.WriteAsync(len, 0, len.Length);
                await _stream.WriteAsync(body, 0, body.Length);
                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                _isConnected = false;
                OnError?.Invoke(ex);
            }
        }

        private async Task ReceiveDataAsync(CancellationToken ct)
        {
            try
            {
                while (_isConnected && !ct.IsCancellationRequested)
                {
                    // 4바이트 길이 읽기
                    byte[] lenBuf = new byte[4];
                    if (!await ReadExactAsync(lenBuf, 4, ct)) break;
                    int msgLen = BitConverter.ToInt32(lenBuf, 0);
                    if (msgLen <= 0 || msgLen > 10 * 1024 * 1024) break; // 10MB 초과 차단

                    // 본문 읽기
                    byte[] body = new byte[msgLen];
                    if (!await ReadExactAsync(body, msgLen, ct)) break;

                    string msg = Encoding.UTF8.GetString(body);
                    OnDataReceived?.Invoke(msg);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (_isConnected) OnError?.Invoke(ex);
            }
            finally
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    OnStatusChanged?.Invoke("Connection lost");
                }
            }
        }

        // 정확히 n바이트 읽기
        private async Task<bool> ReadExactAsync(byte[] buf, int n, CancellationToken ct)
        {
            int total = 0;
            while (total < n)
            {
                int read = await _stream.ReadAsync(buf, total, n - total, ct);
                if (read == 0) return false;
                total += read;
            }
            return true;
        }
    }
}