using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TcpChatManager : MonoBehaviour
{
    public static TcpChatManager Instance;

    [Header("Cấu hình TCP")]
    public int tcpPort = 8888; // Cổng TCP chạy song song

    [Header("Giao diện Chat UI")]
    public GameObject chatPanel;
    public TextMeshProUGUI chatLogText;
    public TMP_InputField chatInputField;
    public Button sendButton;

    // Thành phần Socket TCP
    private TcpListener tcpListener;
    private TcpClient localTcpClient;
    private NetworkStream clientStream;
    private List<TcpClient> connectedClients = new List<TcpClient>();
    private Thread serverThread;
    private Thread clientReceiveThread;
    private bool isRunning = false;

    // Queue để đưa tin nhắn từ background thread về main thread Unity
    private readonly Queue<string> messageQueue = new Queue<string>();

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (sendButton != null) sendButton.onClick.AddListener(SendMessageFromInput);
    }

    void Update()
    {
        // Rút tin nhắn từ hàng đợi để render lên UI (vì luồng mạng không tương tác trực tiếp được với UI Unity)
        lock (messageQueue)
        {
            while (messageQueue.Count > 0)
            {
                string msg = messageQueue.Dequeue();
                if (chatLogText != null)
                {
                    chatLogText.text += "\n" + msg;
                }
            }
        }

        // Nhấn Enter để gửi nhanh
        if (Input.GetKeyDown(KeyCode.Return) && chatInputField != null && chatInputField.isFocused)
        {
            SendMessageFromInput();
        }
    }

    #region PHÍA SERVER (TCP LISTENER)
    public void StartTcpServer()
    {
        isRunning = true;
        serverThread = new Thread(ListenForClients);
        serverThread.IsBackground = true;
        serverThread.Start();
    }

    private void ListenForClients()
    {
        try
        {
            // Khởi tạo Socket lắng nghe TCP trên tất cả các card mạng của máy
            tcpListener = new TcpListener(IPAddress.Any, tcpPort);
            tcpListener.Start();
            QueueLog($"[TCP Server] Đang lắng nghe trên cổng {tcpPort}...");

            while (isRunning)
            {
                // Bắt tay kết nối (TCP Handshake) khi có Client kết nối tới
                TcpClient client = tcpListener.AcceptTcpClient();
                lock (connectedClients)
                {
                    connectedClients.Add(client);
                }

                QueueLog($"[TCP Server] Client mới đã kết nối: {client.Client.RemoteEndPoint}");

                // Tạo một Thread riêng để đọc dữ liệu từ Client này
                Thread clientThread = new Thread(HandleClientComm);
                clientThread.IsBackground = true;
                clientThread.Start(client);
            }
        }
        catch (SocketException ex)
        {
            if (isRunning) QueueLog($"[TCP Server Lỗi]: {ex.Message}");
        }
    }

    private void HandleClientComm(object clientObj)
    {
        TcpClient tcpClient = (TcpClient)clientObj;
        NetworkStream stream = tcpClient.GetStream();
        byte[] buffer = new byte[4096];
        int bytesRead;

        while (isRunning && tcpClient.Connected)
        {
            bytesRead = 0;
            try
            {
                bytesRead = stream.Read(buffer, 0, buffer.Length);
            }
            catch
            {
                break;
            }

            if (bytesRead == 0) break; // Client ngắt kết nối

            string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
            QueueLog(message);

            // Server Broadcast tin nhắn cho tất cả các Client khác
            BroadcastMessage(message, tcpClient);
        }

        lock (connectedClients)
        {
            connectedClients.Remove(tcpClient);
        }
        tcpClient.Close();
    }

    private void BroadcastMessage(string message, TcpClient senderClient)
    {
        byte[] broadcastBuffer = Encoding.UTF8.GetBytes(message);
        lock (connectedClients)
        {
            foreach (var client in connectedClients)
            {
                if (client != senderClient && client.Connected)
                {
                    try
                    {
                        NetworkStream stream = client.GetStream();
                        stream.Write(broadcastBuffer, 0, broadcastBuffer.Length);
                        stream.Flush();
                    }
                    catch { }
                }
            }
        }
    }
    #endregion

    #region PHÍA CLIENT (TCP CLIENT)
    public void ConnectToTcpServer(string hostIp)
    {
        try
        {
            localTcpClient = new TcpClient();
            localTcpClient.Connect(hostIp, tcpPort); // Bắt tay 3 bước với Server
            clientStream = localTcpClient.GetStream();
            isRunning = true;

            clientReceiveThread = new Thread(ReceiveServerData);
            clientReceiveThread.IsBackground = true;
            clientReceiveThread.Start();

            QueueLog($"[TCP Client] Đã kết nối TCP tới Host ({hostIp}:{tcpPort}) thành công!");
        }
        catch (Exception ex)
        {
            QueueLog($"[TCP Client] Không thể kết nối: {ex.Message}");
        }
    }

    private void ReceiveServerData()
    {
        byte[] buffer = new byte[4096];
        int bytesRead;

        while (isRunning && localTcpClient.Connected)
        {
            try
            {
                bytesRead = clientStream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                QueueLog(message);
            }
            catch
            {
                break;
            }
        }
    }

    public void SendTcpMessage(string msg)
    {
        if (clientStream != null && clientStream.CanWrite)
        {
            byte[] data = Encoding.UTF8.GetBytes(msg);
            clientStream.Write(data, 0, data.Length);
            clientStream.Flush();
        }
    }
    #endregion

    public void SendMessageFromInput()
    {
        if (chatInputField == null || string.IsNullOrWhiteSpace(chatInputField.text)) return;

        string senderName = (MultiplayerGameManager.Instance != null && Unity.Netcode.NetworkManager.Singleton != null)
            ? $"Mèo {(int)Unity.Netcode.NetworkManager.Singleton.LocalClientId + 1}"
            : "Player";

        string fullMsg = $"[{senderName}]: {chatInputField.text}";

        // Hiển thị lên khung chat của máy mình
        QueueLog(fullMsg);

        // Gửi qua socket TCP
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
        {
            BroadcastMessage(fullMsg, null);
        }
        else
        {
            SendTcpMessage(fullMsg);
        }

        chatInputField.text = "";
    }

    private void QueueLog(string msg)
    {
        lock (messageQueue)
        {
            messageQueue.Enqueue(msg);
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        try { tcpListener?.Stop(); } catch { }
        try { localTcpClient?.Close(); } catch { }
        try { serverThread?.Abort(); } catch { }
        try { clientReceiveThread?.Abort(); } catch { }
    }
}