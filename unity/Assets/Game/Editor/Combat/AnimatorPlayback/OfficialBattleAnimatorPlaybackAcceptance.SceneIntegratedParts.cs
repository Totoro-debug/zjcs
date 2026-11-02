using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Official.CombatAppearance;
using Zjcs.Official.ProfessionUI;
using Zjcs.Offline.ProfessionUI;
using Object = UnityEngine.Object;

namespace Zjcs.Editor.Combat
{
    public static partial class OfficialBattleAnimatorPlaybackAcceptance
    {
        public static void RunSceneIntegratedParts()
        {
            Run(); SessionState.SetBool(Key + ".sceneIntegratedParts", true);
            SessionState.SetFloat(Key + ".deadline", (float)EditorApplication.timeSinceStartup + 600);
        }

        private static IEnumerator SceneIntegratedPartsSteps()
        {
            // Re-run the existing whole/split native renderer comparison after
            // moving mesh assignment into the shared body/weapon endpoint.
            var regression = SceneRendererSteps();
            while (regression.MoveNext()) yield return regression.Current;
            var catalog = Resources.Load<ProfessionNativeSceneAppearanceResources>("Combat/SceneCharacters/appearance-resources");
            var weapons = Resources.Load<ProfessionNativeSceneWeaponCatalog>("ProfessionUI/scene-weapon-sources");
            var config = AssetDatabase.LoadAssetAtPath<AppearanceConfig>("Assets/Official/ProfessionUI/Portrait/AppearanceConfig.asset");
            var previous = ProfessionAppearanceTextureBindings.Current;
            using var textures = new OfflineProfessionSceneTextureResources(catalog);
            using var manager = new AppearanceAssetManager(weapons.Textures.ManagerConfig, _ => new SceneSubscription());
            var materials = new OfflineProfessionWeaponTextureResources(config, manager, weapons.Textures);
            ProfessionAppearanceTextureBindings.Current = new OfflineProfessionBattleSceneTextures(catalog, textures, weapons.Textures, materials);
            catalog.Loader.TryInit();
            var clones = new Dictionary<Material, Material>();
            var loads = new List<int>();
            Action<Material[]> replace = array =>
            {
                for (int i = 0; i < array.Length; i++)
                {
                    var source = array[i];
                    if (!clones.TryGetValue(source, out var material))
                    {
                        material = new Material(source);
                        int index = catalog.Loader.PlaceholderTextureIndexDict[source.mainTexture];
                        Material unused = null;
                        catalog.Loader.RequestLoadMaterialTextures(material, ref unused);
                        catalog.Loader.AddLoadedTextureRef(index); loads.Add(index); clones.Add(source, material);
                    }
                    array[i] = material;
                }
            };
            try
            {
                foreach (var model in catalog.Models)
                foreach (string group in new[] { "100", "200" })
                {
                    var host = new GameObject("Actual original body and independent scene weapon");
                    host.AddComponent<SortingGroup>().sortingOrder = 2000;
                    var root = new GameObject("PlayerModel_Game"); root.transform.SetParent(host.transform, false);
                    root.transform.localScale = new Vector3(-.3f, .27f, 1);
                    root.transform.localRotation = Quaternion.Euler(0, 0, 17);
                    var native = root.AddComponent<ProfessionNativeSceneRenderer>(); native.Configure(catalog);
                    native.skeletonDataAsset = model.Asset; native.Initialize(false, false);
                    native.OnGetUpdatedSharedMaterials += replace;
                    var slot = new GameObject("PartSlot").transform; slot.SetParent(root.transform, false);
                    var binding = new ScenePartBindings(config);
                    var poses = new SceneDiagnosticWeaponPoses();
                    bool usePool = true;
                    var resources = new OfflineProfessionScenePartPoolBindings(weapons, materials, binding, poses, () => usePool);
                    using var pool = new CharacterPartEntryProvider(resources);
                    var handler = root.AddComponent<AppearanceRendererPartHandler>();
                    handler.ConfigureParts(binding); handler.ConfigureRenderer(native, slot, pool, SortingLayer.IDToName(1182434123), 0);
                    handler.OnGetSharedMaterialDelegate = array => replace(array);
                    string skin = "pWeapon_" + group + "_G_007";
                    Action equip = () => handler.SetSkins(new Dictionary<SkinType, string> { { SkinType.pWeapon, skin } });
                    try
                    {
                        SetIntegratedBodyPose(native, model.Sex, group, group + "/idle_01", .4f);
                        native.LateUpdateMesh();
                        var main = root.GetComponent<ProfessionNativeSceneMeshPart>();
                        Bounds bounds = main.MeshRenderer.bounds;
                        CaptureSceneRendererCamera(bounds, model.Sex + "-" + group + "-before-independent");
                        equip();
                        var part = (AppearanceSkeletonPart)handler.LoadedSkeletonDict[SkinType.pWeapon].Part;
                        var weapon = part.Skeleton.GetComponent<ProfessionNativeSceneWeaponRenderer>();
                        var follower = (SkeletonRendererFollower)part.mSkeletonFollower;
                        var go = part.gameObject;
                        var source = JsonUtility.FromJson<SceneHierarchy>(weapons.Find(skin).Hierarchy.text);
                        var spineFields = source.nodes.Single(n => n.name == "Spine").components.Single(c => c.type == "Transform").runtime_fields;
                        Require(weapon.transform.localPosition == spineFields.m_LocalPosition
                            && weapon.transform.localScale == spineFields.m_LocalScale
                            && Quaternion.Angle(weapon.transform.localRotation, spineFields.m_LocalRotation) < .001f,
                            "independent weapon retains original Spine offset/rotation/scale");
                        Require(part.Skeleton.transform.parent.name == skin && part.Skeleton.transform.parent.parent == go.transform
                            && weapon.transform.Find("Effect") && go.transform.parent == slot, "actual four-node weapon hierarchy and PartSlot binding");
                        var page = weapons.Weapon.FindPage("pWeapon_" + group + "_007");
                        var original = weapons.Find(skin).OriginalMaterial;
                        Require(weapon.CustomMaterialOverride[original].mainTexture == page.Main && original.mainTexture == page.Placeholder,
                            "actual source lite binds High weapon textures while current body Low remains live");
                        var effect = weapon.transform.Find("Effect").GetComponent<SkeletonRendererFollower>();
                        Require(!effect.gameObject.activeSelf, "original effect node starts inactive");
                        // Exercise the real original follower's enabled branch
                        // as well as preserving its source prefab default.
                        effect.gameObject.SetActive(true);
                        foreach (string action in new[] { group + "/idle_01", group + "/skill_41" })
                        {
                            SetIntegratedBodyPose(native, model.Sex, group, action, .4f);
                            poses.Apply(weapon.State);
                            native.LateUpdateMesh(); follower.ForceUpdate(); weapon.UpdateAnimationAndMesh();
                            var point = ((IProfessionFollowerSkeleton)native.State).FindSlot(follower.SlotName);
                            Require(point.Attachment is ProfessionNativePointAttachment && follower.Gos.Single().activeSelf,
                                "original face-shape point anchor enables independent weapon without a second base-weapon mesh");
                            Require(Vector2.Distance(follower.transform.position, root.transform.TransformPoint(
                                new Vector3(point.Bone.WorldX, point.Bone.WorldY, 0))) < .0001f, "actual source follower aligns to the current body bone");
                            var renderers = ((DynamicSkeletonRenderSeparator)native.Separator).PartsRenderers.Cast<ProfessionNativeSceneMeshPart>().ToArray();
                            int cut = Array.FindIndex(native.State.DrawOrder, s => ReferenceEquals(s, point));
                            int key = go.GetComponent<SortingGroup>().sortingOrder;
                            Require(renderers.Length == 2 && renderers[0].GetComponent<SortingGroup>().sortingOrder < key
                                && key < renderers[1].GetComponent<SortingGroup>().sortingOrder && key == 2 * cut + 1,
                                "actual separate weapon is drawn between body segments at its source point slot");
                            bounds = renderers[0].MeshRenderer.bounds;
                            foreach (var renderer in renderers) bounds.Encapsulate(renderer.MeshRenderer.bounds);
                            bounds.Encapsulate(weapon.GetComponent<MeshRenderer>().bounds);
                            var before = CaptureSceneRendererCamera(bounds, model.Sex + "-" + action.Replace('/', '-') + "-integrated");
                            Require(weapon.GetComponent<MeshRenderer>().isVisible, "actual portrait camera renders the independent weapon");
                            // Same-frame return and re-rent through the actual official
                            // handler must preserve transforms, materials and GPU pixels.
                            handler.SetSkins(new Dictionary<SkinType, string>());
                            Require(!go.activeInHierarchy && go.activeSelf && resources.LiveInstanceCount == 1
                                && follower.SkeletonRenderer == null, "official pool retains activeSelf, clears followers and hides pooled objects");
                            equip();
                            Require(handler.LoadedSkeletonDict[SkinType.pWeapon].Part.GameObject == go
                                && go.GetComponents<SortingGroup>().Length == 1, "pool reuses actual native part without duplicate deferred sorting groups");
                            follower.ForceUpdate(); native.LateUpdateMesh(); weapon.UpdateAnimationAndMesh();
                            Require(before.SequenceEqual(CaptureSceneRendererCamera(bounds, model.Sex + "-" + action.Replace('/', '-') + "-rerented")),
                                "same-frame original pool reuse retains exact actual portrait camera pixels");
                            weapon.Initialize(true, false);
                            Require(ReferenceEquals(effect.Bone, weapon.State.FindBone(effect.BoneName)),
                                "pooled enabled effect follower remains subscribed to actual weapon skeleton rebuilds");
                            Require(weapon.CustomMaterialOverride.TryGetValue(original, out var rebuiltMaterial)
                                && rebuiltMaterial.mainTexture == page.Main,
                                "weapon geometry rebuild retains original lite-owned live High material rather than drawing its placeholder");
                            // Restore the same explicit diagnostic pose after rebuilding.
                            poses.Apply(weapon.State);
                            weapon.UpdateAnimationAndMesh();
                            Require(before.SequenceEqual(CaptureSceneRendererCamera(bounds, model.Sex + "-" + action.Replace('/', '-') + "-rebuilt")),
                                "actual portrait pixels retain the weapon High texture and geometry after rebuild");
                        }
                        handler.SetSkins(new Dictionary<SkinType, string>());
                        pool.Update(.5f); pool.Update(18.5f); pool.Update(.5f);
                        Require(resources.LiveInstanceCount == 1, "source one-second checks retain part before twenty seconds");
                        pool.Update(.5f);
                        Require(resources.LiveInstanceCount == 0, "source twenty-second idle expiry releases native part");
                        yield return null;
                        Require(!go, "expired actual weapon objects are destroyed");
                        // Two actual native characters exercise the source LIFO
                        // pool and rebind one prefab across different skeletons.
                        var otherHost = new GameObject("Other source body for native part reuse");
                        otherHost.AddComponent<SortingGroup>();
                        var otherRoot = new GameObject("PlayerModel_Game"); otherRoot.transform.SetParent(otherHost.transform, false);
                        var other = otherRoot.AddComponent<ProfessionNativeSceneRenderer>(); other.Configure(catalog);
                        other.skeletonDataAsset = catalog.Models.Single(m => m.Sex != model.Sex).Asset; other.Initialize(false, false);
                        SetIntegratedBodyPose(other, other.State.Source.Sex, group, group + "/idle_01", .4f);
                        var otherSlot = new GameObject("PartSlot").transform; otherSlot.SetParent(otherRoot.transform, false);
                        try
                        {
                            var first = pool.RentIntegratedPart(SkinType.pWeapon, skin, slot, true, null);
                            var second = pool.RentIntegratedPart(SkinType.pWeapon, skin, otherSlot, true, null);
                            pool.ReturnPart(first, false); pool.ReturnPart(second, false);
                            bool beforeParent = false;
                            var last = pool.RentIntegratedPart(SkinType.pWeapon, skin, slot, true, value =>
                                beforeParent = value.transform.parent.name == "[CharacterPartPool]" && value.activeSelf);
                            var next = pool.RentIntegratedPart(SkinType.pWeapon, skin, otherSlot, true, null);
                            Require(ReferenceEquals(last, second) && ReferenceEquals(next, first) && beforeParent,
                                "original two-object LIFO and callback-before-reparent behavior survive native binding");
                            Require(last.GoPart.GetComponent<SkeletonRendererFollower>().SkeletonRenderer == native
                                && next.GoPart.GetComponent<SkeletonRendererFollower>().SkeletonRenderer == other,
                                "pooled actual weapons rebind to each destination skeleton");
                            pool.ReturnPart(last, false); pool.ReturnPart(next, false); pool.ClearPool();
                            Require(resources.LiveInstanceCount == 0, "source per-path reference counts release both native instances");
                        }
                        finally { other.Release(); Object.Destroy(otherHost); }
                        yield return null;
                        equip(); handler.SetSkins(new Dictionary<SkinType, string>()); pool.OnLowMemory();
                        Require(resources.LiveInstanceCount == 0, "actual low-memory branch clears pooled native objects");
                        yield return null;
                        pool.BeforeSceneLoad(); equip(); handler.SetSkins(new Dictionary<SkinType, string>());
                        Require(resources.LiveInstanceCount == 0, "scene transition rejects returns to the native pool");
                        pool.AfterSceneLoad(); yield return null;
                        usePool = false; equip();
                        Require(!handler.LoadedSkeletonDict[SkinType.pWeapon].PoolEntry.InPool, "source non-map caller disables pooling");
                        handler.SetSkins(new Dictionary<SkinType, string>());
                        Require(resources.LiveInstanceCount == 0, "non-pooled source returns release actual native objects");
                    }
                    finally { handler.Release(true); pool.ClearPool(); native.Release(); Object.Destroy(host); }
                    yield return null;
                    Require(resources.LiveInstanceCount == 0, "source handler/pool teardown releases actual independent weapons");
                }
            }
            finally
            {
                foreach (var material in clones.Values) Object.Destroy(material);
                foreach (int index in loads) catalog.Loader.RemoveLoadedTextureRef(index);
                catalog.Loader.TryUnloadUnusedTextures(true);
                ProfessionAppearanceTextureBindings.Current = previous;
            }
            yield return null;
            Require(textures.LoadedTextureCount == 0, "integrated scene teardown releases all actual body Low texture requests");
            Debug.Log("Actual source body/independent weapon camera composition, original handler and twenty-second pool lifecycle passed for both sexes and groups 100/200. Poses remain explicit unmixed pre-physics diagnostics.");
        }

        private static void SetIntegratedBodyPose(ProfessionNativeSceneRenderer native, string sex, string group, string action, float time)
        {
            var state = native.State;
            var skin = state.CreateSkin("Original independent-weapon point marker diagnostic");
            foreach (string name in new[] { "default", "body_" + group + "_" + (sex == "male" ? "M" : "F") + "_01",
                sex == "male" ? "hair_G_01" : "hair_F_01", sex == "male" ? "faceShape_M_01" : "faceShape_F_01" })
                skin.AddSkin(state.Data.FindSkin(name));
            state.SetSkin(skin); state.SetToSetupPose();
            ProfessionNativeConstraintPose.EvaluateBeforePhysics(state, new ProfessionNativeAnimationChannels(state.Source, action), time, false);
        }
    }
}
