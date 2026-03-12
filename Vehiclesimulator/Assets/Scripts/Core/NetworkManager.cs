using UnityEngine;
using System.Threading.Tasks;
using AirGuard.Data;


namespace AirGuard.Network
{
    /// <summary>
    /// 서버와의 통신 담당
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        [Header("Connection Settings")]
        [SerializeField] private string serverIP = "127.0.0.1";
        [SerializeField] private int tcpPort = 9000;
        [SerializeField] private int udpPort = 9001;
        [SerializeField] private bool useTCP = true;
        [SerializeField] private bool useUDP = false;

        [Header("Transmission Settings")]
        [SerializeField] private float sendInterval = 0.1f;

        private readonly System.Threading.SemaphoreSlim _sendLock = new System.Threading.SemaphoreSlim(1, 1);
        private INetworkClient _tcpClient;
        private INetworkClient _udpClient;
        private float _lastSendTime;

        public static event System.Action<string>? OnCommandReceived;

        public bool IsConnected =>
            (useTCP && _tcpClient?.IsConnected == true) ||
            (useUDP && _udpClient?.IsConnected == true);

        public string ConnectionStatus { get; private set; } = "Not Connected";

        private async void Start()
        {
            await InitializeNetworkAsync();
        }
        
        private async Task InitializeNetworkAsync()
        {
            if(useTCP)
            {
                _tcpClient = new TcpNetworkClient();
                _tcpClient.OnStatusChanged += OnStatusChanged;
                _tcpClient.OnDataReceived += (data) => OnDataReceived(data);
                _tcpClient.OnError += OnError;
            }

            bool connected = await _tcpClient.ConnectAsync(serverIP, tcpPort);
            if (connected)
            {
                Debug.Log($"TCP Connected: {serverIP}:{tcpPort}");
            }

            if (useUDP)
            {
                _udpClient = new UdpNetworkClient();
                _udpClient.OnStatusChanged += OnStatusChanged;
                _udpClient.OnError += OnError;

                bool ready = await _udpClient.ConnectAsync(serverIP, udpPort);
                if (ready)
                {
                    Debug.Log($"UDP Ready: {serverIP}:{udpPort}");
                }
            }
        }

        public async void SendVehicleData(VehicleData data)
        {
            if (Time.time - _lastSendTime < sendInterval) return;
            _lastSendTime = Time.time;
            await SendLockedAsync(data.ToJson());
        }

        public async void SendRawData(string json)
        {
            await SendLockedAsync(json);
        }

        private void OnStatusChanged(string status)
        {
            ConnectionStatus = status;
            Debug.Log($"[Network] {status}");
        }

        private void OnDataReceived(string data)
        {
            Debug.Log($"[Network] Received: {data}");
            OnCommandReceived?.Invoke(data);
        }

        private void OnError(System.Exception ex)
        {
            Debug.LogError($"[Network] Error: {ex.Message}");
        }

        private async System.Threading.Tasks.Task SendLockedAsync(string json)
        {
            await _sendLock.WaitAsync();
            try
            {
                if (useTCP && _tcpClient?.IsConnected == true)
                    await _tcpClient.SendAsync(json);
                if (useUDP && _udpClient?.IsConnected == true)
                    await _udpClient.SendAsync(json);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private void OnDestroy()
        {
            _tcpClient?.Disconnect();
            _udpClient?.Disconnect();
        }

        // Inspector에서 설정 변경용
        public void SetServerIP(string ip) => serverIP = ip;
        public void SetTCPPort(int port) => tcpPort = port;
        public void SetUDPPort(int port) => udpPort = port;
        public void SetSendInterval(float interval) => sendInterval = interval;
    }
}