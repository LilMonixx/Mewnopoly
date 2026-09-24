using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MultiplayerGameManager : NetworkBehaviour
{
    public static MultiplayerGameManager Instance;

    public BoardGenerator boardGenerator;
    public GameObject housePrefab; // Kéo prefab cái tô vào đây
    public List<Player> players = new List<Player>();

    public NetworkVariable<int> currentTurnIndex = new NetworkVariable<int>(0);

    [Header("UI Game")]
    public TextMeshProUGUI turnInfoText;
    public TextMeshProUGUI diceResultText;
    public Button rollDiceButton;

    [Header("Popup Mua Đất")]
    public GameObject buyPropertyPopup;
    public TextMeshProUGUI popupPromptText;
    public Button buyButton;
    public Button passButton;

    [Header("UI Kết Nối Mạng")]
    public GameObject networkUIPanel;
    public Button hostButton;
    public Button clientButton;
    public TMP_InputField ipInputField;

    private int pendingTileIndex = -1;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (rollDiceButton != null)
        {
            rollDiceButton.interactable = false;
            rollDiceButton.onClick.AddListener(OnRollClicked);
        }

        if (hostButton != null) hostButton.onClick.AddListener(StartHostGame);
        if (clientButton != null) clientButton.onClick.AddListener(StartClientGame);

        if (buyButton != null) buyButton.onClick.AddListener(() => RespondBuyProperty(true));
        if (passButton != null) passButton.onClick.AddListener(() => RespondBuyProperty(false));

        if (buyPropertyPopup != null) buyPropertyPopup.SetActive(false);

        currentTurnIndex.OnValueChanged += (oldVal, newVal) => UpdateUI();
    }

    public void StartHostGame()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            if (transport != null)
            {
                // Cho phép Host lắng nghe trên mọi card mạng (gồm cả Radmin VPN và LAN)
                transport.SetConnectionData("0.0.0.0", 7777, "0.0.0.0");
            }

            NetworkManager.Singleton.StartHost();
            if (networkUIPanel != null) networkUIPanel.SetActive(false);

            // Bật TCP Server song song trên port 8888
            if (TcpChatManager.Instance != null)
            {
                TcpChatManager.Instance.StartTcpServer();
            }
        }
    }

    public void StartClientGame()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
        {
            var transport = NetworkManager.Singleton.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>();
            
            // Lấy IP từ ô input (nếu rỗng thì mặc định 127.0.0.1)
            string targetIP = (ipInputField != null && !string.IsNullOrWhiteSpace(ipInputField.text)) 
                            ? ipInputField.text.Trim() 
                            : "127.0.0.1";

            if (transport != null)
            {
                // Client trỏ đúng vào IP Radmin của Host
                transport.SetConnectionData(targetIP, 7777);
            }

            NetworkManager.Singleton.StartClient();
            if (networkUIPanel != null) networkUIPanel.SetActive(false);

            // Kết nối TCP Client
            if (TcpChatManager.Instance != null)
            {
                TcpChatManager.Instance.ConnectToTcpServer(targetIP);
            }
        }
    }

    public void RegisterPlayer(Player p)
    {
        if (!players.Contains(p))
        {
            players.Add(p);
            UpdateUI();
        }
    }

    void OnRollClicked()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

        int localClientId = (int)NetworkManager.Singleton.LocalClientId;
        if (currentTurnIndex.Value == localClientId)
        {
            RequestRollDiceRpc();
        }
    }

    [Rpc(SendTo.Server)]
    void RequestRollDiceRpc(RpcParams rpcParams = default)
    {
        int rollerId = (int)rpcParams.Receive.SenderClientId;
        if (rollerId != currentTurnIndex.Value) return;

        int diceRoll = Random.Range(1, 7);
        UpdateDiceRpc(diceRoll);

        StartCoroutine(ServerProcessTurn(diceRoll));
    }

    [Rpc(SendTo.Everyone)]
    void UpdateDiceRpc(int dice)
    {
        if (diceResultText != null) diceResultText.text = $"Xúc xắc: {dice}";
    }

    IEnumerator ServerProcessTurn(int diceRoll)
    {
        if (players.Count == 0 || boardGenerator == null) yield break;

        Player currentPlayer = players[currentTurnIndex.Value];
        
        for (int i = 0; i < diceRoll; i++)
        {
            currentPlayer.currentTileIndex.Value = (currentPlayer.currentTileIndex.Value + 1) % boardGenerator.waypoints.Count;
            yield return new WaitForSeconds(0.2f);
        }

        // Kiểm tra ô vừa đáp xuống
        int targetTileIndex = currentPlayer.currentTileIndex.Value;
        TileData tile = boardGenerator.waypoints[targetTileIndex].GetComponent<TileData>();

        if (tile != null)
        {
            if (tile.ownerClientId == -1) // Ô chưa có chủ -> Hỏi mua
            {
                pendingTileIndex = targetTileIndex;
                AskBuyPropertyRpc(targetTileIndex, tile.price, RpcTarget.Single((ulong)currentPlayer.OwnerClientId, RpcTargetUse.Temp));
                yield break; // Đợi người chơi quyết định Mua hoặc Bỏ qua
            }
            else if (tile.ownerClientId != (int)currentPlayer.OwnerClientId) // Ô của đối thủ -> Thu tiền thuê
            {
                Player ownerPlayer = players.Find(p => (int)p.OwnerClientId == tile.ownerClientId);
                if (ownerPlayer != null)
                {
                    int rentAmount = Mathf.Min(tile.rent, currentPlayer.money.Value);
                    currentPlayer.money.Value -= rentAmount;
                    ownerPlayer.money.Value += rentAmount;
                }
            }
        }

        EndTurn();
    }

    [Rpc(SendTo.SpecifiedInParams)]
    void AskBuyPropertyRpc(int tileIdx, int price, RpcParams rpcParams = default)
    {
        pendingTileIndex = tileIdx;
        if (buyPropertyPopup != null)
        {
            if (popupPromptText != null)
            {
                popupPromptText.text = $"Bạn có muốn mua ô này với giá {price}$ không?";
            }
            buyPropertyPopup.SetActive(true);
        }
    }

    void RespondBuyProperty(bool wantsToBuy)
    {
        if (buyPropertyPopup != null) buyPropertyPopup.SetActive(false);
        ConfirmBuyPropertyRpc(pendingTileIndex, wantsToBuy);
    }

    [Rpc(SendTo.Server)]
    void ConfirmBuyPropertyRpc(int tileIdx, bool wantsToBuy, RpcParams rpcParams = default)
    {
        Player currentPlayer = players[currentTurnIndex.Value];
        TileData tile = boardGenerator.waypoints[tileIdx].GetComponent<TileData>();

        if (wantsToBuy && tile != null && tile.ownerClientId == -1)
        {
            if (currentPlayer.money.Value >= tile.price)
            {
                currentPlayer.money.Value -= tile.price;
                tile.ownerClientId = (int)currentPlayer.OwnerClientId;

                // Báo cho toàn bộ Client hiển thị cái tô và màu ô đất
                SyncHousePlacementRpc(tileIdx, (int)currentPlayer.OwnerClientId);
            }
        }

        EndTurn();
    }

    [Rpc(SendTo.Everyone)]
    void SyncHousePlacementRpc(int tileIdx, int ownerId)
    {
        TileData tile = boardGenerator.waypoints[tileIdx].GetComponent<TileData>();
        Player ownerP = players.Find(p => (int)p.OwnerClientId == ownerId);
        if (tile != null && ownerP != null)
        {
            tile.SetOwnerVisual(ownerP.playerColor);
            tile.SpawnHouseVisual(housePrefab, ownerP.playerName);
        }
    }

    void EndTurn()
    {
        currentTurnIndex.Value = (currentTurnIndex.Value + 1) % players.Count;
        UpdateUI();
    }

    void UpdateUI()
    {
        if (turnInfoText != null && players.Count > 0)
        {
            int currentTurn = currentTurnIndex.Value;
            bool isMyTurn = (NetworkManager.Singleton != null && (int)NetworkManager.Singleton.LocalClientId == currentTurn);
            string info = $"LƯỢT CỦA: MÈO {currentTurn + 1} " + (isMyTurn ? "(LƯỢT CỦA BẠN!)" : "") + "\n";
            
            foreach (var p in players)
            {
                info += $"{p.playerName}: {p.money.Value}$  |  ";
            }
            turnInfoText.text = info;
        }

        if (rollDiceButton != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            rollDiceButton.interactable = ((int)NetworkManager.Singleton.LocalClientId == currentTurnIndex.Value);
        }
    }
}