using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AirGuard.WPF.Services
{
    /// <summary>
    /// TCP 서버와 통신하여 드론 텔레메트리 및 메시지를 송수신하는 서비스
    /// </summary>
    public class TcpClientService : IDisposable
    {
        private TcpClient? _tcpClient;      // TCP 클라이언트
        private NetworkStream? _stream;     // 네트워크 데이터 스트림
        private bool _isConnected;          // 연결 상태

        public event Action<string>? MessageReceived; // 메시지 수신 이벤트
        public event Action? Disconnected;             // 연결 종료 이벤트
        public event Action<string>? ErrorOccurred;    // 오류 발생 이벤트

        public bool IsConnected => _isConnected;       // 현재 연결 여부

        /// <summary>
        /// 서버에 비동기 연결 후 초기 핸드셰이크 및 맵 요청 수행
        /// </summary>
        public async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            _stream = _tcpClient.GetStream();

            // 클라이언트 타입 알림 (핸드셰이크)
            await SendAsync("WPF_CLIENT");

            _isConnected = true;

            // 수신 루프 시작
            _ = Task.Run(ReceiveDataAsync);

            // 서버에 맵 데이터 요청
            await Task.Delay(300);
            await SendAsync("MAP_REQUEST");
        }

        /// <summary>
        /// 서버로 메시지 전송 (길이 + 메시지 데이터 구조)
        /// </summary>
        public async Task SendAsync(string message)
        {
            if (_stream == null) return;

            try
            {
                byte[] body = Encoding.UTF8.GetBytes(message);
                byte[] len = BitConverter.GetBytes(body.Length);

                // 메시지 길이 전송
                await _stream.WriteAsync(len, 0, len.Length);

                // 메시지 본문 전송
                await _stream.WriteAsync(body, 0, body.Length);

                await _stream.FlushAsync();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        /// <summary>
        /// 서버 연결 종료
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            _stream?.Close();
            _tcpClient?.Close();
        }

        /// <summary>
        /// 서버로부터 데이터를 지속적으로 수신하는 루프
        /// </summary>
        private async Task ReceiveDataAsync()
        {
            try
            {
                while (_isConnected && _stream != null)
                {
                    byte[] lenBuf = new byte[4];

                    // 메시지 길이 읽기
                    if (!await ReadExactAsync(lenBuf, 4)) break;

                    int msgLen = BitConverter.ToInt32(lenBuf, 0);

                    // 메시지 크기 검증
                    if (msgLen <= 0 || msgLen > 10 * 1024 * 1024) break;

                    byte[] body = new byte[msgLen];

                    // 메시지 본문 읽기
                    if (!await ReadExactAsync(body, msgLen)) break;

                    string msg = Encoding.UTF8.GetString(body);

                    // 메시지 이벤트 전달
                    MessageReceived?.Invoke(msg);
                }
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke($"수신 오류: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                if (_isConnected)
                {
                    _isConnected = false;
                    ErrorOccurred?.Invoke("수신 루프 종료됨");
                    Disconnected?.Invoke();
                }
            }
        }

        /// <summary>
        /// 지정된 크기만큼 정확히 데이터를 읽어오는 함수
        /// </summary>
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

        /// <summary>
        /// 리소스 정리 및 연결 종료
        /// </summary>
        public void Dispose() => Disconnect();
    }
}