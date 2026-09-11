using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class RemoveResultPanelInset
{
    private const string InputPath = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Cropped.png";
    private const string OutputPath = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Open.png";
    private const string PieceRoot = "Assets/Art/UI/GeneratedUI/NanoBanana_Refined/TitlePanel/TitlePanel_Frame_";

    public static void Run()
    {
        var input = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        if (!ImageConversion.LoadImage(input, File.ReadAllBytes(InputPath), false))
            throw new Exception("Could not decode result panel source: " + InputPath);

        var pixels = input.GetPixels32();
        var width = input.width;
        var height = input.height;
        // The generated artwork contains a gray-blue rectangle from x=299..1067,
        // y=93..364 in top-left image coordinates. Remove that inset completely.
        // The result panel receives one clean, opaque text field in Unity while
        // the ornate gold/cyan perimeter remains untouched.
        var left = Mathf.RoundToInt(width * (299f / 1367f));
        var right = Mathf.RoundToInt(width * (1067f / 1367f));
        var top = Mathf.RoundToInt(height * (64f / 459f));
        var bottom = Mathf.RoundToInt(height * (364f / 459f));
        for (var topY = top; topY <= bottom; topY++)
        {
            var y = height - 1 - topY;
            for (var x = left; x <= right; x++)
            {
                pixels[y * width + x] = new Color32(0, 0, 0, 0);
            }
        }

        var output = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
        output.SetPixels32(pixels);
        output.Apply(false, false);
        File.WriteAllBytes(OutputPath, output.EncodeToPNG());
        WritePiece(input, new RectInt(0, height - top, width, top), PieceRoot + "Top.png");
        WritePiece(input, new RectInt(0, 0, width, height - bottom), PieceRoot + "Bottom.png");
        WritePiece(input, new RectInt(0, height - bottom, left, bottom - top), PieceRoot + "Left.png");
        WritePiece(input, new RectInt(right + 1, height - bottom, width - right - 1, bottom - top), PieceRoot + "Right.png");
        UnityEngine.Object.DestroyImmediate(input);
        UnityEngine.Object.DestroyImmediate(output);
        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceSynchronousImport);

        var importer = AssetImporter.GetAtPath(OutputPath) as TextureImporter;
        if (importer == null) throw new Exception("Could not import cleaned result panel: " + OutputPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.spritePixelsPerUnit = 100f;
        importer.isReadable = true;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        Debug.Log("Clean result panel written: " + OutputPath);
    }

    private static void WritePiece(Texture2D source, RectInt sourceRect, string path)
    {
        var piece = new Texture2D(sourceRect.width, sourceRect.height, TextureFormat.RGBA32, false, false);
        piece.SetPixels(source.GetPixels(sourceRect.x, sourceRect.y, sourceRect.width, sourceRect.height));
        piece.Apply(false, false);
        File.WriteAllBytes(path, piece.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(piece);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new Exception("Could not import result frame piece: " + path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 2048;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
    }
}
