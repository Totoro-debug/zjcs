using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Zjcs.Official.World.NativeMap14Labels;

namespace Zjcs.Editor.Map14
{
    public static partial class OfficialMap14LabelTools
    {
        [Serializable] private sealed class ScriptBinding
        {
            public string original_type, script_asset, script_guid;
            public long script_file_id;
        }
        [Serializable] private sealed class Bindings { public ScriptBinding[] scripts; }
        public static void ExportBindings()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var types = new[] { typeof(MapRegionDescSupportUI), typeof(MapRegionDescItemUI), typeof(MapRegionStrengthDisplayUI),
                typeof(UIColorByLevel), typeof(ShowRemainTimeInfo), typeof(TextMeshPro), typeof(ContentSizeFitter),
                typeof(Zjcs.Official.World.NativeLabelUI.MultiLanguageText), typeof(Zjcs.Official.World.NativeLabelUI.TextMeshProColorProxy),
                typeof(TMPEffect) };
            var scripts = MonoImporter.GetAllRuntimeMonoScripts();
            var bindings = types.Select(type =>
            {
                var script = scripts.Single(s => s.GetClass() == type);
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string guid, out long fileId))
                    throw new InvalidDataException("Original label MonoScript binding is unavailable: " + type);
                return new ScriptBinding { original_type = type.Name, script_asset = AssetDatabase.GetAssetPath(script),
                    script_guid = guid, script_file_id = fileId };
            }).ToArray();
            File.WriteAllText(Path.Combine(OfficialMap14EnvironmentSceneTools.Repository, ".tools/map14-native-cache-871_156598/label-script-bindings.json"),
                JsonUtility.ToJson(new Bindings { scripts = bindings }, true) + "\n", new UTF8Encoding(false));
            Debug.Log("Map14 original label MonoScript bindings exported: " + bindings.Length);
        }
    }
}
