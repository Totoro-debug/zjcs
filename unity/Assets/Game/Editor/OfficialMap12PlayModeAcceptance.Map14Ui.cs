using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void ImportUiMap14AndRun()
        {
            try { OfficialMap11UiTools.ImportForMap(14); RunUiMap14Imported(); }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void RunUiMap14Imported()
        {
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_SIMULATED", "1");
            ProjectTools.Validate();
            RunCore(true, false, 14);
        }

        // The shared UI fixture uses Map14's own restored prefabs. Terrain,
        // progression and persistence have separate runtime checks.
        private static void ValidateMap14UiPrefabs()
        {
            var text = Resources.Load<TextAsset>("Map14/UI/catalog");
            if (!text) throw new InvalidDataException("The original Map14 UI catalog was not packaged.");
            var data = JsonUtility.FromJson<OfficialMap11UiData>(text.text);
            if (data.manifest != "871_156598" || data.mapClassId != 14 || data.panels.Length != 68 || data.sprites.Length != 956)
                throw new InvalidDataException("Map14 UI differs from the selected official source.");
            int nodes = 0;
            foreach (var panel in data.panels)
            {
                var prefab = Resources.Load<GameObject>("Map14/UI/" + panel.key);
                if (!prefab) throw new InvalidDataException("Original Map14 prefab was not restored: " + panel.key);
                foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidDataException("Original Map14 prefab has an unresolved script: " + panel.key);
                var originalTransforms = new System.Collections.Generic.Dictionary<string, Transform>();
                var byIndex = new Transform[panel.nodes.Length];
                var siblingIndices = new int[panel.nodes.Length];
                for (int index = 0; index < panel.nodes.Length; index++)
                {
                    var node = panel.nodes[index];
                    var transform = index == 0 ? prefab.transform : byIndex[node.parentIndex].GetChild(siblingIndices[node.parentIndex]++);
                    byIndex[index] = transform;
                    originalTransforms.Add(node.path, transform);
                    if (transform.name != node.name) throw new InvalidDataException("Original Map14 UI sibling order/name differs: " + node.path);
                    if (!transform) throw new InvalidDataException("Original Map14 UI hierarchy is incomplete: " + node.path);
                    var rect = transform as RectTransform;
                    if (node.native_transform)
                    {
                        if (rect || node.local_position == null || node.local_rotation == null)
                            throw new InvalidDataException("Original Map14 non-UI Transform was changed: " + node.path);
                        var position = node.local_position;
                        var rotation = node.local_rotation;
                        if (Vector3.Distance(transform.localPosition, new Vector3(position[0], position[1], position[2])) > .00001f
                            || Quaternion.Angle(transform.localRotation, new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3])) > .001f)
                            throw new InvalidDataException("Original Map14 non-UI Transform fields differ: " + node.path);
                    }
                    else
                    {
                        if (!rect) throw new InvalidDataException("Original Map14 UI node lost its RectTransform: " + node.path);
                        var sourceSlider = data.components.SingleOrDefault(c => c.panel == panel.key && c.type == "Slider"
                            && (c.fields.m_FillRect == node.path || c.fields.m_HandleRect == node.path));
                        if (sourceSlider != null)
                        {
                            var slider = originalTransforms[sourceSlider.path].GetComponent<Slider>();
                            var original = sourceSlider.fields;
                            if (!slider || slider.fillRect != rect && slider.handleRect != rect || slider.direction != (Slider.Direction)original.m_Direction
                                || Mathf.Abs(slider.value - original.m_Value) > .00001f || slider.minValue != original.m_MinValue || slider.maxValue != original.m_MaxValue)
                                throw new InvalidDataException("Original Map14 Slider binding/value differs: " + sourceSlider.path);
                            // Unity Slider drives its fill/handle anchors from these serialized parameters.
                        }
                        else
                        {
                            CheckMap14Vector(rect.anchorMin, node.anchor_min, node.path, "anchorMin");
                            CheckMap14Vector(rect.anchorMax, node.anchor_max, node.path, "anchorMax");
                        }
                        CheckMap14Vector(rect.pivot, node.pivot, node.path, "pivot");
                    }
                    nodes++;
                }
            }
            Debug.Log("Map14 original UI prefabs validated: 68 source panels, " + nodes + " original nodes, source anchors/pivots and resolved scripts.");
        }
        private static void CheckMap14Vector(Vector2 actual, float[] expected, string path, string field)
        {
            if (expected == null || expected.Length != 2 || Vector2.Distance(actual, new Vector2(expected[0], expected[1])) > .00001f)
                throw new InvalidDataException("Original Map14 UI " + field + " differs: " + path);
        }
    }
}
