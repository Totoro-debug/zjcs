using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class CurrentResourceShaderRenderValidation
{
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        string path = File.ReadAllText(Path.Combine(work, "render-shader-path.txt")).Trim();
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (!shader) throw new InvalidDataException("Converted shader did not import.");
        var material = new Material(shader);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var expected = new[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255), new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 255) };
        source.SetPixels32(expected); source.Apply();
        material.SetTexture("_MainTex", source); material.SetColor("_Color", Color.white);
        var target = RenderTexture.GetTemporary(64, 64, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active; RenderTexture.active = target;
        GL.Clear(false, true, Color.black); GL.PushMatrix(); GL.LoadOrtho();
        if (!material.SetPass(0)) throw new InvalidDataException("Converted UI shader pass is unavailable.");
        GL.Begin(GL.QUADS); GL.Color(Color.white);
        GL.TexCoord2(0, 0); GL.Vertex3(0, 0, 0);
        GL.TexCoord2(1, 0); GL.Vertex3(1, 0, 0);
        GL.TexCoord2(1, 1); GL.Vertex3(1, 1, 0);
        GL.TexCoord2(0, 1); GL.Vertex3(0, 1, 0);
        GL.End(); GL.PopMatrix();
        var image = new Texture2D(64, 64, TextureFormat.RGBA32, false, true);
        image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); image.Apply();
        var actual = image.GetPixels32();
        File.WriteAllBytes(Path.Combine(work, "converted-shader-render.png"), image.EncodeToPNG());
        for (int y = 0; y < 2; y++)
            for (int x = 0; x < 2; x++)
            {
                Color32 pixel = actual[(16 + y * 32) * 64 + 16 + x * 32];
                if (!pixel.Equals(expected[y * 2 + x]))
                    throw new InvalidDataException("Converted shader pixel differs at " + x + "," + y + ": " + pixel);
            }
        RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(image);
        File.WriteAllText(Path.Combine(work, "render-result.json"), "{\"shader\":\"UI/Default\",\"sampled_pixels\":4,\"success\":true}\n");
        EditorApplication.Exit(0);
    }
}
