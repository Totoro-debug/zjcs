using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.World.NativeMap13;

namespace Zjcs.Editor.Map13
{
    internal static class OfficialMap13FollowerTextureAcceptance
    {
        [Serializable] private sealed class SkeletonCatalog { public Model[] models; public Renderer[] renderers; }
        [Serializable] private sealed class Model { public string resource; }
        [Serializable] private sealed class Renderer
        { public int npcClassId, skeletonIndex; public string startingAnimation; public string[] combineSkins; }

        private const string ActiveKey = "Zjcs.Map13.FollowerTextures.Active";
        private static readonly int[] ClassIds = { 8402, 8440, 8441, 8442 };

        [InitializeOnLoadMethod]
        private static void RegisterCompletion()
        {
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update += FinishWhenStopped;
        }

        public static void Run()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(ActiveKey, true);
            SessionState.SetBool(ActiveKey + ".Done", false);
            SessionState.SetInt(ActiveKey + ".Exit", 1);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(ActiveKey, false) || state != PlayModeStateChange.EnteredPlayMode) return;
            try
            {
                VerifyResources();
                SessionState.SetInt(ActiveKey + ".Exit", 0);
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                SessionState.SetBool(ActiveKey + ".Done", true);
                EditorApplication.isPlaying = false;
            }
        }

        private static void FinishWhenStopped()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !SessionState.GetBool(ActiveKey + ".Done", false) ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            int exitCode = SessionState.GetInt(ActiveKey + ".Exit", 1);
            SessionState.EraseBool(ActiveKey);
            EditorApplication.Exit(exitCode);
        }

        private static void VerifyResources()
        {
            TextAsset source = Resources.Load<TextAsset>("Map12/TeamNpcSkeletons/catalog");
            Require(source != null, "Official NPC skeleton catalog is missing.");
            SkeletonCatalog skeletonCatalog = JsonUtility.FromJson<SkeletonCatalog>(source.text);
            Require(skeletonCatalog.models != null && skeletonCatalog.renderers != null,
                "Official NPC skeleton catalog could not be parsed.");
            NativeMap13AssetCatalog textures = Resources.Load<NativeMap13AssetCatalog>(
                "Map13/Native/npc-follower-texture-catalog");
            NativeMap13AssetCatalog materials = Resources.Load<NativeMap13AssetCatalog>(
                "Map13/Native/native-entity-materials-catalog");
            Require(textures != null && materials != null, "Original follower texture or material catalog is missing.");

            foreach (int classId in ClassIds)
            {
                Renderer binding = skeletonCatalog.renderers.Single(row => row.npcClassId == classId);
                Require(binding.skeletonIndex >= 0 && binding.skeletonIndex < skeletonCatalog.models.Length,
                    "Original follower skeleton index is invalid: " + classId);
                Model model = skeletonCatalog.models[binding.skeletonIndex];
                TextAsset package = Resources.Load<TextAsset>(model.resource);
                Require(package != null, "Original follower skeleton package is missing: " + classId);
                ProfessionNativeSkeletonData data = ProfessionNativeSkeletonData.LoadNpc(package.bytes);
                Require(data.AnimationNames.Contains(binding.startingAnimation),
                    "Original follower idle animation is missing: " + classId);
                ProfessionNativeSkin skin = data.CreateCombinedNpcSkin(binding.combineSkins);
                Require(skin.AttachmentCount > 0, "Original follower skin has no attachments: " + classId);

                var pages = skin.Entries.Select(row => row.Attachment as ProfessionNativeRenderAttachment)
                    .Where(attachment => attachment != null).Select(attachment => Path.GetFileName(attachment.Page))
                    .Distinct(StringComparer.Ordinal).ToArray();
                string[] expectedPages = classId == 8440
                    ? new[] { "npc_8440", "pWeapon_100_8440" }
                    : new[] { "npc_" + classId };
                string[] pageNames = pages.Select(Path.GetFileNameWithoutExtension).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                Require(pageNames.SequenceEqual(expectedPages.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal),
                    "Original follower skin Atlas page differs from its local texture binding: " + classId +
                    " (expected " + string.Join(",", expectedPages) + ", found " + string.Join(",", pages) + ").");
                foreach (string pageName in pageNames)
                {
                    string page = pageName + ".png";
                    string modelName = classId == 8440 ? "npc_big_model" : "npc_middle_model";
                    string officialPath = "Assets/AppearanceAssets/Npc/" + modelName + "/Atlas/High/" + page;
                    Texture2D texture = textures.Load<Texture2D>(officialPath);
                    Require(texture != null && texture.width > 0 && texture.height > 0 && texture.mipmapCount == 1 &&
                        texture.name.StartsWith(pageName, StringComparison.Ordinal),
                        "Original follower Atlas page is not a valid packaged texture: " + officialPath);
                }

                string materialKey = classId == 8440
                    ? "CAB-35db32a9156215d66e80a363a190f94d:Material:-8287306839946497736"
                    : classId == 8402
                        ? "CAB-b9b1514d5b015c219a6e35eb0745556e:Material:1601366034732659983"
                        : classId == 8441
                            ? "CAB-b9b1514d5b015c219a6e35eb0745556e:Material:-6569910388499614487"
                            : "CAB-b9b1514d5b015c219a6e35eb0745556e:Material:-8466807034191633858";
                Material material = materials.Load<Material>(materialKey);
                Require(material != null && material.shader != null && material.mainTexture != null,
                    "Original follower page material binding is incomplete: " + classId);
            }
            Debug.Log("Map13 follower Resources check passed for four original skeletons, idle animations, combined skins, Atlas pages, materials and local textures. Live follower rendering and constraint evaluation remain separate.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidDataException(message); }
    }
}
