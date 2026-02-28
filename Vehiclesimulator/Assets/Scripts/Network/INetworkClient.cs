using System;
using System.Threading.Tasks;

namespace AirGuard.Network
{
    /// <summary>
    /// 네트워크 통신 인터페이스
    /// </summary>
    public interface INetworkClient
    {
        bool IsConnected { get; }

        Task<bool> ConnectAsync(string host, int port);
        void Disconnect();
        Task SendAsync(string data);

        event Action<string> OnStatusChanged;
        event Action<string> OnDataReceived;
        event Action<Exception> OnError;
    }
}