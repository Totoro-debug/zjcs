using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Zjcs.Combat.Official;
using Zjcs.Offline.ProfessionUI;
using Current = Zjcs.Official.CombatAppearance;

namespace Zjcs.Presentation.Combat
{
    // Explicit diagnostic input for the real public battle host, never a live profile mutation.
    public sealed class OfficialBattlePlayerCloneValidation
    {
        private readonly FightOfficialCombatCatalog catalog;
        private readonly FightBattleSkillDefinition attack;
        private readonly int skillEntityId;
        private readonly string expectedAnimation;
        private readonly bool combo;
        private readonly HashSet<string> expectedTracks = new HashSet<string>();
        private readonly Dictionary<MyPlayableSpineClone, int> captureStages = new Dictionary<MyPlayableSpineClone, int>();
        private int expectedInstances = 1;
        private readonly HashSet<string> observed = new HashSet<string>();
        private readonly List<ProfessionNativeSceneRenderer> renderers = new List<ProfessionNativeSceneRenderer>();
        private bool cast;
        private bool checkedHiddenSource;
        public string CaptureName { get; private set; }

        private OfficialBattlePlayerCloneValidation(FightOfficialCombatCatalog catalog,
            FightBattleSkillDefinition attack, int skillEntityId, string expectedAnimation, bool combo)
        {
            this.catalog = catalog; this.attack = attack; this.skillEntityId = skillEntityId;
            this.expectedAnimation = expectedAnimation; this.combo = combo;
            if (!combo) expectedTracks.Add(expectedAnimation);
        }

        public static OfficialBattlePlayerCloneValidation Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            string selected = Environment.GetEnvironmentVariable("ZJCS_PLAYER_CLONE_ENTITY");
            int skillEntityId = 13100;
            if (!string.IsNullOrEmpty(selected))
                Require(int.TryParse(selected, out skillEntityId), "Invalid isolated clone segment id.");
            var segments = new Dictionary<int, string> {
                {13100, "100/skill_62_1"}, {13104, "100/skill_62_2"},
                {13105, "100/skill_62_3"}, {13106, "100/skill_62_4"}, {13107, "100/skill_62_5"} };
            Require(segments.TryGetValue(skillEntityId, out string expectedAnimation),
                "Select one original clone-bearing skill segment.");
            var player = checkpoint.Units.Single(value => value.Id == 1);
            bool combo = Environment.GetEnvironmentVariable("ZJCS_PLAYER_COMBO_ACCEPTANCE") == "1";
            Require(!combo || skillEntityId == 13100, "The combo diagnostic must begin with original 13100.");
            var enemy = checkpoint.Units.First(value => value.Id != 1);
            enemy.MonsterId = combo ? 3665 : 3331; enemy.MonsterGroupNo = 0;
            enemy.CharacterType = catalog.MonsterCharacterType(enemy.MonsterId);
            enemy.MonsterType = catalog.MonsterType(enemy.MonsterId);
            enemy.AiType = catalog.MonsterAiType(enemy.MonsterId);
            enemy.MaxHpPlies = catalog.MonsterHpPliesFor(enemy.MonsterId, 0, enemy.Level);
            OfficialBattleUnitAssembler.ApplyMonsterIdentity(catalog, enemy);
            Require(enemy.BodyOffsets.Count == (combo ? 9 : 1), "The diagnostic target lost its original body.");
            player.X = enemy.X; player.Y = enemy.Y - 2;
            Require(checkpoint.Grid.Contains(new FightGridPoint(player.X, player.Y)), "Diagnostic source is outside the grid.");
            enemy.Skills.Clear(); enemy.SkillState.Slots.Clear();
            foreach (var unit in checkpoint.Units) unit.Statuses.Clear();
            var attack = player.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                foreach (var property in unit.Properties.Concat(unit.BaseProperties ?? unit.Properties))
                {
                    if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 5000000;
                    if (unit != player && property.Id == catalog.PropertyType("Attack")) property.Value = 100;
                }
                unit.Hp = unit.Turn.Hp = unit.MaxHp = unit.LastMaxHp = 5000000;
            }
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            var skill = catalog.CreateSkill(skillEntityId, 0, 10154, 5, 20, 1, null);
            skill.LimitedTimes = 1;
            player.Skills.Add(skill); player.SkillState.Slots.Add(catalog.CreateSkillSlot(skill, 0));
            return new OfficialBattlePlayerCloneValidation(catalog, attack, skillEntityId, expectedAnimation, combo);
        }

        public void ObserveStep(OfficialBattleRuntimeController controller)
        {
            if (cast || !controller.Checkpoint.Receipts.Last().Event.SkillCasts.Any(value => value.SkillEntityId == skillEntityId)) return;
            cast = true;
            if (combo)
            {
                var casts = controller.Checkpoint.Receipts.Last().Event.SkillCasts
                    .Where(value => value.ActorId == 1).OrderBy(value => value.Delay).ToArray();
                Require(casts.Length == 6 && casts[0].SkillEntityId == 13100,
                    "The public combo did not execute its five continuation stages.");
                expectedInstances = 0;
                foreach (var segment in casts)
                    if (segment.SkillEntityId >= 13100 && segment.SkillEntityId <= 13107)
                    {
                        int index = segment.SkillEntityId == 13100 ? 1 : segment.SkillEntityId - 13102;
                        expectedTracks.Add("100/skill_62_" + index); expectedInstances++;
                    }
                var saved = FightBattleCheckpointSerializer.Load(FightBattleCheckpointSerializer.Save(controller.Checkpoint));
                Require(saved.Units.Single(value => value.Id == 1).SkillState.Slots[0].ReleasedTimes == 1,
                    "The actual combo spent more than one equipped release.");
                Debug.Log("[Player combo] actual six-stage order: " + string.Join(",", casts.Select(value =>
                    value.SkillEntityId + "@" + value.Delay)) + "; one equipped release; checkpoint passed.");
            }
            var player = controller.Checkpoint.Units.Single(value => value.Id == 1);
            player.Skills.Clear(); player.SkillState.Slots.Clear();
            player.Skills.Add(attack); player.SkillState.Slots.Add(catalog.CreateSkillSlot(attack, 0));
            foreach (var property in player.Properties.Concat(player.BaseProperties ?? player.Properties))
                if (property.Id == catalog.PropertyType("Attack")) property.Value = 100000000;
        }

        public void ObserveFrame()
        {
            CaptureName = null;
            if (cast && expectedTracks.Count > 0 && expectedTracks.All(observed.Contains)
                && (!combo || !UnityEngine.Object.FindObjectsOfType<MyPlayableSpineClone>(true)
                    .Any(value => value.gameObject.activeInHierarchy))) Time.timeScale = 5f;
            foreach (var playable in UnityEngine.Object.FindObjectsOfType<MyPlayableSpineClone>(true))
            {
                var native = playable.SkeletonAnimation as ProfessionNativeSceneRenderer;
                Require(native, "A public skill clone still targets the SDK renderer.");
                var animation = native.GetComponent<OfficialBattleSpineCloneAnimation>();
                Require(animation && !native.GetComponent<Animator>(), "The clone has no independent native clock.");
                if (!native.gameObject.activeInHierarchy || animation.AnimState?.GetCurrent(0) == null
                    || animation.AnimationTime <= 0) continue;
                Require(animation.AnimState.GetCurrent(0).Animation.Name == playable.AnimName,
                    "The original clone did not select its authored track.");
                Require(animation.PhysicsTime > 0 && native.State != null, "The clone pose/physics did not advance.");
                Require(!native.GetComponentInParent<SortingGroup>(true)
                    && native.GetComponentsInChildren<SortingGroup>(true).All(value => !value.enabled),
                    "The original ungrouped clone was assigned a new character or fragment sorting priority.");
                var source = playable.myApplyComponent;
                Require(source && source.TryGetComponent<Current.PlayerAppearance>(out _),
                    "The clone did not resolve the actual source player's appearance.");
                var sourceAppearance = source.GetComponent<Current.PlayerAppearance>();
                var sourceRenderer = source.GetComponent<ProfessionNativeSceneRenderer>();
                Require(sourceRenderer && !ReferenceEquals(sourceRenderer.State, native.State),
                    "A clone shares its source skeleton state.");
                var sourceAnimator = source.GetComponent<Animator>();
                if (sourceAnimator && !sourceAnimator.gameObject.activeInHierarchy)
                {
                    Require(OfficialBattleAnimatorPlayback.Supports(sourceAnimator,
                        new OfficialBattleSkillPlayableCue { trigger_param = "run" })
                        && !OfficialBattleAnimatorPlayback.Supports(sourceAnimator,
                            new OfficialBattleSkillPlayableCue { trigger_param = "__missing_clone_validation_parameter" }),
                        "A hidden source lost its actual controller contract or accepted an unknown parameter: "
                        + sourceAnimator.runtimeAnimatorController?.name + ", initialized=" + sourceAnimator.isInitialized);
                    checkedHiddenSource = true;
                }
                foreach (var pair in sourceAppearance.AppearanceRuntimeData.SkinMap)
                    if (sourceAppearance.AppearanceCharacter.TryGetSkinName(pair.Key, out var expected))
                        Require(native.GetComponent<Current.AppearanceCharacter>().TryGetSkinName(pair.Key, out var skin)
                            && skin == expected, "A clone lost or copied a different source skin.");
                var meshes = native.GetComponentsInChildren<MeshFilter>(true)
                    .Where(value => value.gameObject.activeInHierarchy && value.sharedMesh && value.sharedMesh.vertexCount > 0).ToArray();
                Require(meshes.Length > 0, "The active clone produced no native body/part geometry.");
                var materials = meshes.Select(value => value.GetComponent<MeshRenderer>()).Where(value => value)
                    .SelectMany(value => value.sharedMaterials).Where(value => value).ToArray();
                Require(meshes.Select(value => value.GetComponent<MeshRenderer>()).Where(value => value)
                    .All(value => value.sortingLayerID == 1182434123 && value.sortingOrder == 0)
                    && Mathf.Approximately(native.ZSpacing, -.0002f),
                    "The clone body/weapon renderer orders or original slot depth changed.");
                Require(materials.Length > 0 && materials.All(value => value.mainTexture
                    && !value.mainTexture.name.EndsWith("_placeholder", StringComparison.Ordinal)),
                    "The clone's actual body/part textures were not loaded.");
                if (!renderers.Contains(native)) renderers.Add(native);
                if (observed.Add(playable.AnimName))
                {
                    DumpVisibleMaterials();
                    CaptureName = "clone-" + playable.AnimName.Replace('/', '-');
                    Debug.Log("[Player clone] original " + playable.AnimName
                        + ": source appearance, independent skeleton/clock, native body/parts and textures verified.");
                }
                int stage = captureStages.TryGetValue(playable, out int value) ? value : 0;
                if (stage < 3 && animation.AnimationTime >= new[] { .03f, .12f, .25f }[stage])
                {
                    captureStages[playable] = stage + 1;
                    CaptureName = "clone-" + renderers.IndexOf(native) + "-" + playable.AnimName.Replace('/', '-') + "-" + stage;
                    DumpVisibleMaterials(CaptureName);
                }
            }
        }

        public void Verify(FightBattleCheckpoint checkpoint)
        {
            Time.timeScale = 1f;
            Require(cast && checkpoint.Finished && observed.SetEquals(expectedTracks) && checkedHiddenSource,
                "The public " + skillEntityId + " isolated segment or its hidden source binding was not verified: " + string.Join(",", observed));
            Require(renderers.Count == expectedInstances && renderers.All(value => !value), "The battle retained or omitted original clone renderers after release.");
            Debug.Log("[Player clone] public " + skillEntityId + (combo ? " combo" : " isolated segment")
                + " completed: appearance/body/parts, independent clock, hidden source movement binding and battle release passed.");
        }

        private void DumpVisibleMaterials(string capture = "initial")
        {
            string output = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
            Require(!string.IsNullOrEmpty(output) && Path.IsPathRooted(output),
                "The isolated material trace needs its validation directory.");
            var lines = new List<string>();
            foreach (var renderer in UnityEngine.Object.FindObjectsOfType<Renderer>())
            {
                if (!renderer.enabled || !renderer.isVisible) continue;
                string path = string.Join("/", renderer.GetComponentsInParent<Transform>(true)
                    .Reverse().Select(value => value.name));
                if (!path.Contains("MyPlayableManager")) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (!material || !material.shader) continue;
                    lines.Add(path + " | " + renderer.GetType().Name + " | " + material.name
                        + " | " + material.shader.name + " | keywords=" + string.Join(",", material.shaderKeywords));
                    foreach (string property in material.GetTexturePropertyNames())
                    {
                        var texture = material.GetTexture(property);
                        lines.Add("  " + property + "=" + (texture ? texture.name : "<null>"));
                    }
                    for (int property = 0; property < material.shader.GetPropertyCount(); property++)
                        if (material.shader.GetPropertyType(property) == ShaderPropertyType.Color)
                        {
                            string name = material.shader.GetPropertyName(property);
                            lines.Add("  " + name + "=" + material.GetColor(name));
                        }
                        else if (material.shader.GetPropertyType(property) == ShaderPropertyType.Float
                            || material.shader.GetPropertyType(property) == ShaderPropertyType.Range)
                        {
                            string name = material.shader.GetPropertyName(property);
                            lines.Add("  " + name + "=" + material.GetFloat(name));
                        }
                }
            }
            File.WriteAllLines(Path.Combine(output, "clone-materials-" + skillEntityId + "-" + capture + ".log"), lines);
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Original player clone: " + message); }
    }
}
