using UnityEngine;
using System.Collections.Generic;

public class BoardGenerator : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject regularTilePrefab;
    public GameObject cornerTilePrefab;

    [Header("Kích thước (Units)")]
    public float regularWidth = 2f;    // Bề ngang ô thường
    public float tileHeight = 3f;      // Chiều dài ô thường
    public float cornerSize = 3f;       // Cạnh ô vuông góc

    [Header("Danh sách 40 ô trên bàn cờ")]
    public List<Transform> waypoints = new List<Transform>();

    [ContextMenu("Generate Board")]
    public void GenerateBoard()
    {
        ClearBoard();
        waypoints.Clear();

        GameObject boardHolder = new GameObject("Generated_Board_Holder");
        boardHolder.transform.SetParent(this.transform);
        boardHolder.transform.localPosition = Vector3.zero;

        float totalSide = 9 * regularWidth + cornerSize;
        float halfSide = totalSide / 2f;
        float cornerPosVal = halfSide - (cornerSize / 2f);
        float regularCenterY = halfSide - (tileHeight / 2f);

        for (int side = 0; side < 4; side++)
        {
            // 1. Sinh ô góc (Corner)
            GameObject cornerTile = Instantiate(cornerTilePrefab, boardHolder.transform);
            cornerTile.name = $"Tile_Corner_{side * 10}";

            Vector3 cPos = Vector3.zero;
            switch (side)
            {
                case 0: cPos = new Vector3(-cornerPosVal, -cornerPosVal, 0); break; // Đỉnh Dưới
                case 1: cPos = new Vector3(-cornerPosVal, cornerPosVal, 0); break;  // Đỉnh Trái
                case 2: cPos = new Vector3(cornerPosVal, cornerPosVal, 0); break;   // Đỉnh Trên
                case 3: cPos = new Vector3(cornerPosVal, -cornerPosVal, 0); break;  // Đỉnh Phải
            }

            cornerTile.transform.localPosition = cPos;
            cornerTile.transform.localRotation = Quaternion.identity;
            waypoints.Add(cornerTile.transform);

            // GẮN DỮ LIỆU TILE DATA CHO Ô GÓC Ở ĐÂY:
            TileData cornerData = cornerTile.AddComponent<TileData>();
            cornerData.tileIndex = side * 10;
            cornerData.tileName = $"Góc {side * 10}";
            cornerData.tileType = TileType.Corner;

            // 2. Sinh 9 ô thường nối tiếp
            for (int i = 1; i <= 9; i++)
            {
                GameObject regTile = Instantiate(regularTilePrefab, boardHolder.transform);
                int tileIndex = side * 10 + i;
                regTile.name = $"Tile_{tileIndex}";

                float offset = -halfSide + cornerSize + (i - 0.5f) * regularWidth;
                Vector3 rPos = Vector3.zero;
                float rotZ = 0f;

                switch (side)
                {
                    case 0: // Dưới lên Trái
                        rPos = new Vector3(-regularCenterY, offset, 0);
                        rotZ = -90f;
                        break;
                    case 1: // Trái sang Trên
                        rPos = new Vector3(offset, regularCenterY, 0);
                        rotZ = 180f;
                        break;
                    case 2: // Trên xuống Phải
                        rPos = new Vector3(regularCenterY, -offset, 0);
                        rotZ = 90f;
                        break;
                    case 3: // Phải về Dưới
                        rPos = new Vector3(-offset, -regularCenterY, 0);
                        rotZ = 0f;
                        break;
                }

                regTile.transform.localPosition = rPos;
                regTile.transform.localRotation = Quaternion.Euler(0, 0, rotZ);
                waypoints.Add(regTile.transform);

                // GẮN DỮ LIỆU TILE DATA CHO Ô THƯỜNG Ở ĐÂY:
                TileData regData = regTile.AddComponent<TileData>();
                regData.tileIndex = tileIndex;
                regData.tileName = $"Khu đất {tileIndex}";
                regData.tileType = TileType.Property;
                regData.price = 100 + (tileIndex * 15);
                regData.rentPrice = Mathf.RoundToInt(regData.price * 0.25f);
            }
        }

        boardHolder.transform.localRotation = Quaternion.Euler(0, 0, 45f);
    }

    [ContextMenu("Clear Board")]
    public void ClearBoard()
    {
        waypoints.Clear();
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }
}