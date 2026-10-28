using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.ProfessionUI
{
    public static class ProfessionImageLoaderValidation
    {
        [Serializable] private sealed class Fixture { public Expected[] items; }
        [Serializable] private sealed class Expected { public string path; }
        private sealed class Operation : IProfessionAssetOperation<Sprite>
        {
            private Action<IProfessionAssetOperation<Sprite>> completed;
            public bool Done, Disposed, Success = true;
            public int DisposeCount;
            public Sprite Value;
            public bool IsValid { get { Require(!Disposed, "released request result must not be consumed"); return Success; } }
            public int Status => Success ? 2 : 3;
            public Sprite Result => Value;
            public event Action<IProfessionAssetOperation<Sprite>> Completed
            {
                add { if (Done) value(this); else completed += value; }
                remove { completed -= value; }
            }
            public void Complete(Sprite value)
            {
                Require(!Done, "test operation completes once");
                Value = value; Done = true; completed?.Invoke(this);
            }
            public void Dispose() { DisposeCount++; Disposed = true; Value = null; }
        }
        private sealed class Resources : IProfessionImageResourceLoader
        {
            public readonly List<Operation> Operations = new List<Operation>();
            public readonly List<string> Requests = new List<string>();
            public Sprite Next;
            public bool ImmediateAsync;
            private IProfessionAssetOperation<T> Create<T>(string kind, string key, bool done) where T : Object
            {
                Require(typeof(T) == typeof(Sprite), "original Image loader requests Sprite type");
                Requests.Add(kind + ":" + key);
                var operation = new Operation(); Operations.Add(operation);
                if (done) operation.Complete(Next);
                return (IProfessionAssetOperation<T>)(object)operation;
            }
            public IProfessionAssetOperation<T> LoadAsset<T>(string key) where T : Object => Create<T>("path", key, true);
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string key) where T : Object => Create<T>("guid", key, true);
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string key) where T : Object => Create<T>("async-path", key, ImmediateAsync);
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string key) where T : Object => Create<T>("async-guid", key, ImmediateAsync);
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("Original profession image loader: " + message);
        }
        private static void Field(object target, string name, object value)
        {
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) { field.SetValue(target, value); return; }
            }
            throw new MissingFieldException(name);
        }
        public static void Run()
        {
            GameObject owner = null;
            var previous = ProfessionImageResourceBridge.Provider;
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                string fixturePath = ProfessionValidationWorkspace.Fixture("reward-sprite-native-fixture.json");
                var fixture = JsonUtility.FromJson<Fixture>(File.ReadAllText(fixturePath));
                Sprite a = AssetDatabase.LoadAssetAtPath<Sprite>(fixture.items[0].path);
                Sprite b = AssetDatabase.LoadAssetAtPath<Sprite>(fixture.items[1].path);
                Require(a != null && b != null && a != b, "two imported original reward Sprite assets");
                owner = new GameObject("Profession image loader contract probe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                Image image = owner.GetComponent<Image>();
                var loader = new ImageLoaderProxy(); Field(loader, "mLoader", image);
                Field(loader, "mDataProxy", new AssetGuidProxy<Sprite>());
                var proxy = new ImageProxy(); Field(proxy, "mImage", image); Field(proxy, "mImageLoaderProxy", loader);
                var resources = new Resources { Next = a };
                ProfessionImageResourceBridge.Provider = requested => { Require(requested == image, "original image-scoped resource provider"); return resources; };
                proxy.Load("ItemIcon/Other/item_301");
                Require(image.sprite == a && resources.Requests.Last() == "path:Assets/Image/ItemIcon/Other/item_301", "original image path prefix and synchronous assignment");
                resources.Next = b; proxy.LoadByGuid("original-source-guid");
                Require(image.sprite == b && resources.Requests.Last() == "guid:original-source-guid", "original direct GUID load");
                Require(resources.Operations.All(value => !value.Disposed), "retain all request lifetimes until Clear");
                proxy.LoadAsync("first"); var first = resources.Operations.Last();
                proxy.LoadByGuidAsync("second"); var second = resources.Operations.Last();
                Require(resources.Requests[2] == "async-path:Assets/Image/first" && resources.Requests[3] == "async-guid:second", "original async path/GUID routing");
                first.Complete(a); Require(image.sprite == b, "stale asynchronous completion cannot overwrite current image");
                second.Complete(a); Require(image.sprite == a, "current asynchronous completion updates original image");
                proxy.LoadAsync("cleared"); var cleared = resources.Operations.Last();
                proxy.Clear(); Require(image.sprite == null && resources.Operations.All(value => value.DisposeCount == 1), "Clear releases all accumulated operations exactly once");
                cleared.Complete(b); Require(image.sprite == null, "late completion after Clear stays cancelled without consuming released handle");
                resources.ImmediateAsync = true; resources.Next = b; proxy.LoadByGuidAsync("already-complete");
                Require(image.sprite == b, "already-complete operation dispatches during subscription after current handle assignment");
                resources.ImmediateAsync = false; proxy.LoadByGuidAsync("failure");
                Operation failure = resources.Operations.Last(); failure.Success = false; failure.Complete(a);
                Require(image.sprite == null, "failed original operation clears the image");
                proxy.LoadAsync("disposed"); Operation disposed = resources.Operations.Last();
                proxy.Dispose(); Require(image.sprite == null && resources.Operations.All(value => value.DisposeCount == 1), "Dispose releases current and retained operations");
                disposed.Complete(a); Require(image.sprite == null, "late completion after Dispose ignored");
                int count = resources.Requests.Count; proxy.Load("ignored"); proxy.LoadByGuidAsync("ignored");
                Require(resources.Requests.Count == count && image.sprite == null, "disposed loader rejects future loads before resolving resources");
                Debug.Log("Original profession image loader validation passed: original Sprite-backed synchronous/GUID paths, out-of-order async completion, clear/dispose cancellation, already-completed subscription and failed-load behavior. Full ItemIcon prefab and GUI are not covered.");
            }
            finally
            {
                ProfessionImageResourceBridge.Provider = previous;
                if (owner != null) Object.DestroyImmediate(owner);
                AssetDatabase.DeleteAsset("Assets/ProfessionDynamicResourcesProbe");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
