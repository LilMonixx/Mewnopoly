using UnityEngine;

// Định nghĩa lại enum TileType để BoardGenerator không bị lỗi
public enum TileType
{
    Start,
    Property,
    Corner,
    Special
}

public class TileData : MonoBehaviour
{
    public int tileIndex;
    public string tileName;
    public TileType tileType = TileType.Property; // Khớp với BoardGenerator
    public int price = 200;
    public int rentPrice = 50; // Khớp với BoardGenerator (rentPrice)
    public int rent => rentPrice; // Getter phụ trợ để MultiplayerGameManager gọi rent vẫn hiểu

    public int ownerClientId = -1; // -1: chưa ai sở hữu
    public GameObject currentHouse;

    // Đổi màu nền ô đất khi có người mua
    public void SetOwnerVisual(Color ownerColor)
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.color = Color.Lerp(Color.white, ownerColor, 0.35f);
        }
    }

    // Spawn cái tô nội thất vào mép trong bàn cờ
    public void SpawnHouseVisual(GameObject housePrefab, string ownerName)
    {
        if (housePrefab != null && currentHouse == null)
        {
            Vector3 centerDir = (Vector3.zero - transform.position).normalized;
            Vector3 spawnPos = transform.position + centerDir * 0.75f;

            currentHouse = Instantiate(housePrefab, spawnPos, Quaternion.identity);
            currentHouse.name = $"House_{tileIndex}_{ownerName}";
            currentHouse.transform.localScale = new Vector3(0.4f, 0.4f, 1f);

            SpriteRenderer houseSr = currentHouse.GetComponent<SpriteRenderer>();
            if (houseSr != null)
            {
                houseSr.sortingOrder = 3;
            }
        }
    }
}