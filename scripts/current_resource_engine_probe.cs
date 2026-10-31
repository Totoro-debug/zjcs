using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public sealed class CurrentResourceEngineProbe : ScriptableObject
{
    public Shader[] shaders;
    public ComputeShader[] computes;
    [Serializable] public sealed class KernelCase { public string name; public uint x, y, z; }
    [Serializable] public sealed class Item { public string name; public bool windows_d3d; public KernelCase[] kernels; }
    [Serializable] public sealed class Catalog { public Item[] shaders, computes; }
    [Serializable] public sealed class Results { public int shaders, computes, kernel_groups; public bool[] supported; public string graphics_device; }
    public static void Run()
    {
        string work = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
        var catalog = JsonUtility.FromJson<Catalog>(File.ReadAllText(Path.Combine(work, "engine-catalog.json")));
        var probe = AssetDatabase.LoadAssetAtPath<CurrentResourceEngineProbe>("Assets/EngineProbe.asset");
        if (!probe || probe.shaders.Length != catalog.shaders.Length || probe.computes.Length != catalog.computes.Length)
            throw new InvalidDataException("Native engine references did not import.");
        var result = new Results { graphics_device = SystemInfo.graphicsDeviceVersion, supported = new bool[probe.shaders.Length] };
        for (int i = 0; i < probe.shaders.Length; i++)
        {
            var shader = probe.shaders[i];
            var found = Shader.Find(catalog.shaders[i].name);
            if (found && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(found, out string guid, out long fileId))
                Debug.Log("ENGINE_BINDING " + found.name + " " + guid + " " + fileId + " " + found.isSupported);
            if (!shader || shader.name != catalog.shaders[i].name)
                throw new InvalidDataException("Native engine shader differs: " + catalog.shaders[i].name + " actual=" + (shader ? shader.name + " supported=" + shader.isSupported : "null"));
            result.supported[i] = shader.isSupported;
            result.shaders++;
        }
        for (int i = 0; i < probe.computes.Length; i++)
        {
            var shader = probe.computes[i]; var item = catalog.computes[i];
            if (!shader || shader.name != item.name) throw new InvalidDataException("Native engine compute differs: " + item.name);
            if (item.windows_d3d)
                foreach (var kernel in item.kernels)
                {
                    int index = shader.FindKernel(kernel.name);
                    shader.GetKernelThreadGroupSizes(index, out uint x, out uint y, out uint z);
                    if (x != kernel.x || y != kernel.y || z != kernel.z)
                        throw new InvalidDataException("Native engine thread group differs: " + item.name + "/" + kernel.name);
                    result.kernel_groups++;
                }
            result.computes++;
        }
        File.WriteAllText(Path.Combine(work, "engine-results.json"), JsonUtility.ToJson(result, true));
        Debug.Log("CURRENT_ENGINE " + result.shaders + " shaders, " + result.computes + " computes, " + result.kernel_groups + " kernel groups.");
        EditorApplication.Exit(0);
    }
}
