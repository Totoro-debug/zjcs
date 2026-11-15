using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterFaceValidation : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void PrepareValidation()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_CHARACTER_FACE_PROBE") != "1") return;
            Environment.SetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION", "1");
            AudioListener.volume = 0;
            Application.runInBackground = true;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartValidation()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_CHARACTER_FACE_PROBE") != "1") return;
            var host = new GameObject("Character face validation");
            DontDestroyOnLoad(host);
            host.AddComponent<CharacterFaceValidation>().StartCoroutine(Check());
        }
        private static System.Collections.IEnumerator Check()
        {
            yield return null;
            try { Capture(); File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), "face-result.txt"), "passed"); }
            catch (Exception e) { Debug.LogException(e); Finish(1); yield break; }
            Finish(0);
        }
        private static void Finish(int code)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            UnityEditor.EditorApplication.delayCall += () => UnityEditor.EditorApplication.Exit(code);
#else
            Application.Quit(code);
#endif
        }
#if UNITY_EDITOR
        public static void RunEditor()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
            UnityEditor.EditorApplication.isPlaying = true;
        }
#endif
        public static void Capture()
        {
            AudioListener.volume = 0;
            string root = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            VerifyTransparentTexel();
            for (int sex = 0; sex < 2; sex++)
            foreach (string animation in new[] { "home/idle_01", "map/run_01" })
            foreach (float sample in new[] { .2f, .7f })
            foreach (int facing in new[] { 1, -1 })
            {
                var model = CharacterCreationNativeModelFactory.CreateArenaGuardModel(sex, 0, null);
                model.transform.position = Vector3.right * 1000;
                model.transform.localScale = new Vector3(facing, 1, 1);
                foreach (Transform child in model.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var animator = model.GetComponent<Animator>();
                var clip = animator.runtimeAnimatorController.animationClips.First(x => x.name == animation);
                animator.enabled = false;
                clip.SampleAnimation(model, sample);
                model.GetComponent<CharacterNativeAttachmentGeometry>().RefreshForCapture();
                var renderers = model.GetComponentsInChildren<MeshRenderer>().Where(x => x.enabled && !x.forceRenderingOff).ToArray();
                if (!renderers.Any(x => x.GetComponent<MeshFilter>().sharedMesh.name.Contains("face_eye_yankuang")))
                    throw new InvalidOperationException("The original eye outline is not visible.");
                var bounds = renderers[0].bounds;
                foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                var go = new GameObject("Face validation camera", typeof(Camera));
                var camera = go.GetComponent<Camera>();
                camera.cullingMask = 1 << 31;
                camera.orthographic = true;
                camera.orthographicSize = bounds.size.y * .55f;
                camera.transform.position = bounds.center + Vector3.back * 10;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.5f,.65f,.6f,1);
                var target = new RenderTexture(600,800,24);
                camera.targetTexture = target;
                camera.Render();
                var old = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(600,800,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,600,800),0,0); image.Apply();
                string key = sex+"-"+animation.Replace('/','-')+"-"+sample.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+"-"+facing;
                File.WriteAllBytes(Path.Combine(root,"face-"+key+".png"),image.EncodeToPNG());
                RenderTexture.active = old;
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(model);
            }
            Debug.Log("Character face capture complete.");
        }

        private static void VerifyTransparentTexel()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var mesh = go.GetComponent<MeshFilter>().mesh;
            mesh.colors = Enumerable.Repeat(Color.white, mesh.vertexCount).ToArray();
            go.layer = 31;
            go.transform.position = Vector3.right * 2000;
            var material = new Material(Shader.Find("Zjcs/CharacterCreation/OfficialSpineDye"));
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            material.mainTexture = texture;
            go.GetComponent<Renderer>().sharedMaterial = material;
            var cameraObject = new GameObject("Alpha regression camera", typeof(Camera));
            var camera = cameraObject.GetComponent<Camera>();
            camera.cullingMask = 1 << 31;
            camera.orthographic = true; camera.orthographicSize = 1;
            camera.transform.position = go.transform.position + Vector3.back * 10;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            var target = new RenderTexture(16,16,24);
            var pixels = new Texture2D(16,16,TextureFormat.RGBA32,false);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            try
            {
                foreach (float alpha in new[] { 0f, 1f })
                {
                    texture.SetPixel(0,0,new Color(1,1,1,alpha)); texture.Apply();
                    camera.Render(); RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0,0,16,16),0,0); pixels.Apply();
                    float value = pixels.GetPixel(8,8).r;
                    if (alpha == 0 && value > .01f || alpha == 1 && value < .99f)
                        throw new InvalidOperationException("Decoded straight-alpha atlas blend failed: alpha="+alpha+" output="+value);
                }
            }
            finally
            {
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(go); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(mesh);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
