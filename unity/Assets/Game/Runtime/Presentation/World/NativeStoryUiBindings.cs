using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.StoryNodes;

namespace Zjcs.Presentation.World
{
    // Restores the original serialized lists to the migrated native registration
    // script. Recovered APK DLLs stay disabled on Windows.
    public sealed class NativeStoryUiBindings
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private readonly Transform root;
        public StoryNodeRegisterMgr Nodes { get; } = new StoryNodeRegisterMgr();
        public NativeStoryUiBindings(Transform root) => this.root = root ? root : throw new ArgumentNullException(nameof(root));

        public GameObject FindWindow(string path)
        {
            string name = path.Substring(path.LastIndexOf('/') + 1);
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(false))
                // WaitWindowOpenedNode.OnWindowsOpen also accepts WindowName.
                if (candidate.name == name || candidate.name == name + "(Clone)") return candidate.gameObject;
            return null;
        }

        public GameObject FindNode(string key)
        {
            return Nodes.GetNode(key);
        }

        [Serializable] private sealed class Document { public string manifest; public Prefab[] prefabs; }
        [Serializable] private sealed class Prefab { public string name; public Registration[] registrations; public Raycaster[] raycasters; public Window[] windows; }
        [Serializable] private sealed class Window : Address { public int windowType; }
        [Serializable] private class Address { public string path; public string[] objects; }
        [Serializable] private sealed class Registration : Address
        { public bool enabled; public string[] keys; public Address[] nodes; }
        [Serializable] private sealed class Raycaster : Address
        { public bool enabled, ignoreReversedGraphics; public int blockingObjects; public uint blockingMask; }
        private static Document document;

        public void BindNativeWindow(GameObject window, string prefabName)
        {
            if (window.activeInHierarchy) throw new InvalidOperationException("Bind original story UI before enabling the window.");
            if (document == null)
            {
                TextAsset source = Resources.Load<TextAsset>("StoryUI/native-registrations");
                if (!source) throw new InvalidOperationException("Original story UI registration payload is missing.");
                document = JsonUtility.FromJson<Document>(source.text);
            }
            if (document.manifest != "884_156692") throw new InvalidOperationException("Story UI registration version differs.");
            Prefab prefab = Array.Find(document.prefabs, value => value.name == prefabName)
                ?? throw new InvalidOperationException("Original story UI registrations are not bound: " + prefabName);
            foreach (Window source in prefab.windows ?? Array.Empty<Window>())
            {
                GameObject owner = Resolve(window.transform, source);
                var metadata = owner.GetComponent<NativeStoryWindowMetadata>() ?? owner.AddComponent<NativeStoryWindowMetadata>();
                metadata.WindowType = source.windowType;
            }
            foreach (Raycaster source in prefab.raycasters)
            {
                GameObject owner = Resolve(window.transform, source);
                var raycaster = owner.GetComponent<GraphicRaycaster>() ?? owner.AddComponent<GraphicRaycaster>();
                raycaster.ignoreReversedGraphics = source.ignoreReversedGraphics;
                raycaster.blockingObjects = (GraphicRaycaster.BlockingObjects)source.blockingObjects;
                raycaster.blockingMask = unchecked((int)source.blockingMask);
                raycaster.enabled = source.enabled;
            }
            foreach (Registration source in prefab.registrations)
            {
                GameObject owner = Resolve(window.transform, source);
                var registration = owner.GetComponent<Zjcs.Official.ProfessionUI.StoryNodeRegister>()
                    ?? owner.AddComponent<Zjcs.Official.ProfessionUI.StoryNodeRegister>();
                registration.StoryNodeKeys = new List<string>(source.keys);
                registration.StoryNodes = new List<GameObject>();
                foreach (Address node in source.nodes)
                    registration.StoryNodes.Add(node == null ? null : Resolve(window.transform, node));
                registration.enabled = source.enabled;
                registration.Init(Nodes);
            }
        }

        private static GameObject Resolve(Transform root, Address address)
        {
            Transform current = root;
            string[] steps = string.IsNullOrEmpty(address.path) ? Array.Empty<string>() : address.path.Split('/');
            if (address.objects.Length != steps.Length + 1 ||
                (root.name != address.objects[0] && root.name != address.objects[0] + "(Clone)"))
                throw new InvalidOperationException("Original story UI root/hierarchy differs.");
            for (int i = 0; i < steps.Length; i++)
            {
                current = current.GetChild(int.Parse(steps[i]));
                if (current.name != address.objects[i + 1]) throw new InvalidOperationException("Original story UI target hierarchy differs.");
            }
            return current.gameObject;
        }

        public static Button ListItemButton(GameObject list, int index)
        {
            foreach (MonoBehaviour component in list.GetComponents<MonoBehaviour>())
            {
                if (!component) continue;
                Type type = component.GetType();
                if (!IsLoopScroll(type)) continue;
                var content = type.GetProperty("content", Fields)?.GetValue(component) as RectTransform
                    ?? type.GetField("m_Content", Fields)?.GetValue(component) as RectTransform;
                if (!content) throw new InvalidOperationException("Original story LoopScrollRect content is missing.");
                GameObject item = content.GetChild(index).gameObject;
                foreach (MonoBehaviour target in item.GetComponents<MonoBehaviour>())
                    if (target && target.GetType().Name == "StoryTarget")
                        return (target.GetType().GetField("mTarget", Fields)?.GetValue(target) as GameObject)?.GetComponent<Button>();
                throw new InvalidOperationException("Original story list item StoryTarget is missing.");
            }
            throw new InvalidOperationException("Original registered story LoopScrollRect is missing.");
        }

        private static bool IsLoopScroll(Type type)
        {
            for (; type != null; type = type.BaseType)
                if (type.Name == "LoopScrollRect" || type.Name == "LoopScrollRectBase") return true;
            return false;
        }

    }
}
