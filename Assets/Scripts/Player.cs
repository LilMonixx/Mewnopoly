using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class Player : NetworkBehaviour
{
    // Đồng bộ vị trí ô đất và số tiền qua mạng
    public NetworkVariable<int> currentTileIndex = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> money = new NetworkVariable<int>(1500, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public string playerName;
    public Color playerColor;
    public int playerIndex;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    public override void OnNetworkSpawn()
    {
        // Gán màu và index dựa trên ClientId của người chơi
        playerIndex = (int)OwnerClientId;
        playerName = $"Mèo {playerIndex + 1}";
        playerColor = (playerIndex == 0) ? Color.white : new Color(1f, 0.6f, 0.6f);

        if (sr != null)
        {
            sr.color = playerColor;
            sr.sortingOrder = 5;
        }

        // Đăng ký vào GameManager
        if (MultiplayerGameManager.Instance != null)
        {
            MultiplayerGameManager.Instance.RegisterPlayer(this);
        }

        // Lắng nghe thay đổi biến vị trí ô đất
        currentTileIndex.OnValueChanged += (oldVal, newVal) =>
        {
            UpdateVisualPosition();
        };

        UpdateVisualPosition();
    }

    public void UpdateVisualPosition()
    {
        if (MultiplayerGameManager.Instance != null && MultiplayerGameManager.Instance.boardGenerator != null)
        {
            var waypoints = MultiplayerGameManager.Instance.boardGenerator.waypoints;
            if (waypoints != null && waypoints.Count > currentTileIndex.Value)
            {
                transform.position = GetOffsetPosition(waypoints[currentTileIndex.Value].position);
            }
        }
    }

    public Vector3 GetOffsetPosition(Vector3 tilePos)
    {
        Vector3 outwardDir = (tilePos - Vector3.zero).normalized;
        if (outwardDir == Vector3.zero) outwardDir = Vector3.down;
        Vector3 basePos = tilePos + outwardDir * 0.45f;
        Vector3 sideDir = new Vector3(-outwardDir.y, outwardDir.x, 0);
        float spread = (playerIndex == 0) ? -0.2f : 0.2f;
        return basePos + sideDir * spread;
    }
}