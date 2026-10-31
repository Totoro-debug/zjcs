using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class CurrentResourceTextureValidation
{
    [Serializable] public sealed class Mip { public int face, level; public string expected, sha256; }
    [Serializable] public sealed class Item { public string path, sha256, name; public int width, height, mip_count, native_format; public bool cubemap; public Mip[] mips; }
    [Serializable] public sealed class Catalog { public Item[] textures; }
    [Serializable] public sealed class Result { public string path, error; public bool success; public int checked_mips; }
    [Serializable] public sealed class Results { public Result[] textures; public string graphics_device; }
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(work, "texture-catalog.json")));
        var results = new List<Result>();
        bool rendered = false;
        foreach (var item in catalog.textures)
        {
            var result = new Result { path = item.path };
            try
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture>(item.path);
                if (!texture) throw new InvalidDataException("Native texture did not import.");
                if (texture.width != item.width || texture.height != item.height)
                    throw new InvalidDataException("Native dimensions differ: " + texture.width + "x" + texture.height);
                if ((texture is Cubemap) != item.cubemap) throw new InvalidDataException("Texture type differs.");
                int mipCount = texture is Texture2D t2 ? t2.mipmapCount : ((Cubemap)texture).mipmapCount;
                if (item.mips.Length != 0 && mipCount != item.mip_count) throw new InvalidDataException("Mip count differs: " + mipCount);
                foreach (var group in item.mips.GroupBy(m => m.level))
                {
                    var request = AsyncGPUReadback.Request(texture, group.Key, TextureFormat.RGBA32);
                    request.WaitForCompletion();
                    if (request.hasError) throw new InvalidDataException("GPU readback failed: " + group.Key);
                    foreach (var mip in group)
                    {
                        byte[] actual = request.GetData<byte>(mip.face).ToArray();
                        byte[] expected = File.ReadAllBytes(Path.Combine(work, mip.expected));
                        if (!actual.SequenceEqual(expected))
                        {
                            int first = Enumerable.Range(0, Math.Min(actual.Length, expected.Length)).FirstOrDefault(i => actual[i] != expected[i]);
                            throw new InvalidDataException("GPU bytes differ: face=" + mip.face + " level=" + mip.level + " sizes=" + actual.Length + "/" + expected.Length + " first=" + first + " values=" + actual[first] + "/" + expected[first]);
                        }
                        result.checked_mips++;
                    }
                }
                if (!rendered && item.native_format == 4 && item.width >= 64 && !item.cubemap)
                {
                    var target = RenderTexture.GetTemporary(512, 512, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(texture, target);
                    var previous = RenderTexture.active;
                    RenderTexture.active = target;
                    var image = new Texture2D(512, 512, TextureFormat.RGBA32, false);
                    image.ReadPixels(new Rect(0, 0, 512, 512), 0, 0); image.Apply();
                    File.WriteAllBytes(Path.Combine(work, "native-texture-render.png"), image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    RenderTexture.active = previous; RenderTexture.ReleaseTemporary(target);
                    rendered = true;
                }
                result.success = true;
            }
            catch (Exception error) { result.error = error.ToString(); }
            results.Add(result);
            File.WriteAllText(Path.Combine(work, "texture-results.json"), JsonUtility.ToJson(new Results { textures = results.ToArray(), graphics_device = SystemInfo.graphicsDeviceVersion }, true));
            Debug.Log("CURRENT_TEXTURE " + results.Count + "/" + catalog.textures.Length + " " + item.name + " " + result.success);
        }
        EditorApplication.Exit(results.All(r => r.success) && rendered ? 0 : 1);
    }
}
