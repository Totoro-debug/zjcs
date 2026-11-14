#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Zjcs.Presentation.Home;

public sealed partial class HomeIncomeFormulaHostRunner
{
    IEnumerator CheckNativeBackdrop()
    {
        var assets = Resources.Load<HomeNativeBackdropAssets>("Home/HomeNativeBackdropAssets");
        var environment = Resources.Load<HomeNativeEnvironmentAssets>("Home/HomeNativeEnvironmentAssets");
        Require(assets != null && assets.Root != null && assets.Clip != null,
            "original Background ClipDatas entry resolves to its complete native root and clip");
        Require(assets.Additional != null && assets.Additional.Length == 2
            && assets.Additional[0].Name == "Back_Cloud" && assets.Additional[1].Name == "Back_Mountain",
            "original cloud and mountain ClipDatas entries retain their original binding order");
        var preview = new GameObject("Original home backdrop validation");
        var graph = PlayableGraph.Create("Original HomeTimeline backdrop validation");
        graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playables = new List<AnimationClipPlayable>();
        var roots = new List<GameObject>();
        var cameraObject = new GameObject("Backdrop validation camera",typeof(Camera));
        cameraObject.transform.SetParent(preview.transform,false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.CopyFrom(home.Scene.Camera);
        camera.transform.SetPositionAndRotation(home.Scene.Camera.transform.position,home.Scene.Camera.transform.rotation);
        camera.targetTexture = target;
        cameraObject.tag = "MainCamera";
        home.Scene.Root.SetActive(false);
        try
        {
            GameObject background = null;
            var pairs = new List<Tuple<GameObject,AnimationClip>> {
                Tuple.Create(environment.Day,environment.DayClip),
                Tuple.Create(environment.Night,environment.NightClip),
                Tuple.Create(assets.Root,assets.Clip) };
            foreach (var binding in assets.Additional) pairs.Add(Tuple.Create(binding.Root,binding.Clip));
            foreach (var pair in pairs)
            {
                var root = UnityEngine.Object.Instantiate(pair.Item1,preview.transform,false);
                roots.Add(root);
                if (pair.Item1 == assets.Root) background = root;
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    Require(component != null,"original native backdrop has no missing script");
                    if (component.GetType().Name == "HomeCameraFollower")
                        component.GetType().GetField("Target").SetValue(component,camera.transform);
                }
                var output = AnimationPlayableOutput.Create(graph,pair.Item2.name,root.GetComponent<Animator>());
                var mixer = AnimationMixerPlayable.Create(graph,2);
                output.SetSourcePlayable(mixer);
                var playable = AnimationClipPlayable.Create(graph,pair.Item2);
                graph.Connect(playable,0,mixer,0);mixer.SetInputWeight(0,1);
                playables.Add(playable);
            }
            graph.Play();
            yield return null; yield return null;
            var stars = background.transform.Find("FX_Star");
            Require(stars != null && stars.childCount == 10
                && stars.GetComponentsInChildren<ParticleSystem>(true).Length == 20,
                "all ten original star groups and twenty particle systems remain in their native hierarchy");
            var sky = background.transform.Find("Bg/Back_Bg").GetComponent<SpriteRenderer>();
            Require(sky != null && sky.sortingLayerID == 1037039787 && sky.sharedMaterial != null
                && sky.sharedMaterial.shader.name == "Universal Render Pipeline/2D/Sprite-Lit-ColorBlend",
                "original native sky retains its sorting layer and verified Windows ColorBlend shader");
            Color noon = Color.clear, night = Color.clear;
            string noonClouds = null, nightClouds = null;
            foreach (var hour in new[] { 6,12,18,21 })
            {
                foreach (var playable in playables) playable.SetTime(environment.Duration * (hour / 24.0));
                graph.Evaluate();
                yield return null;
                string cloudState = CheckNativeCloudBindings(roots);
                camera.Render();
                Save("home-native-backdrop-"+hour+".png");
                if (hour == 12) { noon = SkyPixel(); noonClouds = cloudState; }
                if (hour == 21) { night = SkyPixel(); nightClouds = cloudState; }
            }
            Require(Mathf.Abs(noon.r-night.r)+Mathf.Abs(noon.g-night.g)+Mathf.Abs(noon.b-night.b) > .1f,
                "original native background clip changes rendered sky pixels between noon and night: "+noon+" -> "+night);
            Require(noonClouds != nightClouds,"original cloud clips animate custom mesh HSL and sprite-group opacity");
            Debug.Log("HOME_NATIVE_CLOUD_MOUNTAIN_PASSED: original cloud/mountain hierarchies and clips, native custom sprite meshes, UV1 HSL, alpha property blocks and actual dawn/noon/sunset/night rendering. Controlled scene only.");
            Debug.Log("HOME_NATIVE_BACKDROP_PASSED: original complete Back_Ground, ten star groups and twenty particle systems, native ColorBlend shader and rendered dawn/noon/sunset/night. Controlled scene only; production sorting integration remains pending.");
        }
        finally
        {
            if (graph.IsValid()) graph.Destroy();
            UnityEngine.Object.Destroy(preview);
            home.Scene.Root.SetActive(true);
        }
        yield return null;
    }

    string CheckNativeCloudBindings(List<GameObject> roots)
    {
        int meshes = 0, groups = 0;
        var state = new List<string>();
        foreach (var root in roots)
            foreach (var component in root.GetComponentsInChildren<MonoBehaviour>())
            {
                var type = component.GetType();
                if (type.Name == "CustomRenderer")
                {
                    var mesh = component.GetComponent<MeshFilter>().sharedMesh;
                    var sprite = (Sprite)type.GetField("Sprite").GetValue(component);
                    var expected = new Vector3((float)type.GetField("Hue").GetValue(component),
                        (float)type.GetField("Saturation").GetValue(component),
                        (float)type.GetField("Lightness").GetValue(component));
                    var uv = new List<Vector3>(); mesh.GetUVs(1,uv);
                    Require(sprite != null && mesh.vertexCount == sprite.vertices.Length && uv.Count == mesh.vertexCount
                        && uv.Count > 0 && (uv[0]-expected).sqrMagnitude < .00000001f,
                        "original animated custom sprite reaches its native mesh and UV1 HSL: "+component.name);
                    meshes++; state.Add(component.GetInstanceID()+":"+expected.ToString("R"));
                }
                if (type.Name == "SpriteRendererGroup")
                {
                    float alpha = (float)type.GetField("Alpha").GetValue(component);
                    var material = (Material)type.GetField("Mat").GetValue(component);
                    foreach (var sprite in component.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block);
                        Require(sprite.sharedMaterial == material && Mathf.Abs(block.GetFloat("_ExtraAlpha")-alpha) < .00001f,
                            "original group animation preserves its material and alpha property block: "+component.name);
                    }
                    groups++; state.Add(component.GetInstanceID()+":"+alpha.ToString("R"));
                }
            }
        Require(meshes > 0 && groups > 0,"selected cloud hierarchy uses the actual original custom renderers and alpha groups");
        return string.Join(";",state);
    }
}
#endif
