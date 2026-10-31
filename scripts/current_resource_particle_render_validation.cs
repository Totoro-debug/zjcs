using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

public static class CurrentResourceParticleRenderValidation
{
    [Serializable] public sealed class Result { public string sha256; public bool success; public Vector3 expected, actual; public float error; }
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        string path = "Assets/Shaders/7e1b03a63ab132d27607fc701170aa049a19e191402f4d85f644aaea9223605b.shader";
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
        if (!shader) throw new InvalidDataException("Particle shader did not import.");
        var material = new Material(shader); material.SetInt("_Cull", 0);
        var normal = new Vector3(.2f, .4f, .9f).normalized;
        var mesh = new Mesh { vertices = new[] { new Vector3(-.7f, -.7f, .5f), new Vector3(.7f, -.7f, .5f), new Vector3(0, .7f, .5f) }, normals = new[] { normal, normal, normal }, triangles = new[] { 0, 1, 2 } };
        var matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0, 0, 23), new Vector3(.8f, .6f, 1));
        var expected = matrix.inverse.transpose.MultiplyVector(normal).normalized;
        var target = RenderTexture.GetTemporary(64, 64, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active; RenderTexture.active = target;
        GL.Clear(true, true, new Color(9, 9, 9, 9)); GL.PushMatrix(); GL.LoadProjectionMatrix(Matrix4x4.identity); GL.modelview = Matrix4x4.identity;
        if (!material.SetPass(2)) throw new InvalidDataException("Particle depth normal pass is unavailable.");
        Graphics.DrawMeshNow(mesh, matrix); GL.PopMatrix();
        var image = new Texture2D(64, 64, TextureFormat.RGBAFloat, false, true);
        image.ReadPixels(new Rect(0, 0, 64, 64), 0, 0); image.Apply();
        Color pixel = image.GetPixel(32, 32); var actual = new Vector3(pixel.r, pixel.g, pixel.b);
        var preview = new Texture2D(64, 64, TextureFormat.RGBA32, false, true); preview.SetPixels(image.GetPixels()); preview.Apply();
        File.WriteAllBytes(Path.Combine(work, "particle-normal-render.png"), preview.EncodeToPNG());
        var result = new Result { actual = actual, expected = expected, error = Vector3.Distance(actual, expected) };
        using (var hash = SHA256.Create()) result.sha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant();
        result.success = result.error < .00002f && Mathf.Abs(pixel.a) < .00002f;
        File.WriteAllText(Path.Combine(work, "particle-render-result.json"), JsonUtility.ToJson(result, true));
        RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
        UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(preview);
        if (!result.success) throw new InvalidDataException("Particle normal GPU result differs: " + result.error);
        EditorApplication.Exit(0);
    }
}
