using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public class BorderTextureCreator : MonoBehaviour
{
    [MenuItem("Tools/Create Border Sprite")]
    public static void CreateBorderSprite()
    {
        int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        Color border = Color.black;
        Color inside = Color.white;

        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // Nếu ở mép ngoài cùng (1 pixel) thì tô màu đen, ở trong tô màu trắng
                if (x == 0 || x == size - 1 || y == 0 || y == size - 1)
                    tex.SetPixel(x, y, border);
                else
                    tex.SetPixel(x, y, inside);
            }
        }
        tex.Apply();

        byte[] bytes = tex.EncodeToPNG();
        string path = Application.dataPath + "/Sprites/Tile_Border_Gen.png";
        File.WriteAllBytes(path, bytes);

        AssetDatabase.Refresh();

        // Tự động cấu hình 9-slice border
        TextureImporter importer = AssetImporter.GetAtPath("Assets/Sprites/Tile_Border_Gen.png") as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spriteBorder = new Vector4(1, 1, 1, 1); // Cắt 1px ở 4 cạnh
            importer.SaveAndReimport();
        }

        Debug.Log("Đã tạo xong ảnh viền: Assets/Sprites/Tile_Border_Gen.png!");
    }
}
#endif