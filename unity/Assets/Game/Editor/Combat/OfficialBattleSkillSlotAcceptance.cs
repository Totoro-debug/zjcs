using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Editor.Combat
{
    public static class OfficialBattleSkillSlotAcceptance
    {
        [MenuItem("Zjcs/Combat/Validate Original Skill Slots")]
        public static void Run()
        {
            OfficialBattleSkillQualityAcceptance.Run();
            var canvas = new GameObject("BattleSkillSlotFixture", typeof(RectTransform), typeof(Canvas));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            try
            {
                var view = new OfficialBattleHudView(OfficialBattleHudCatalog.LoadDefault());
                GameObject root = view.Mount(canvas.transform);
                BattleSkillSlotData data = OfficialBattleSkillSlotBindings.Load();
                Require(!OfficialBattleSkillSlotBindings.IsPetFunctionUnlocked(0)
                    && OfficialBattleSkillSlotBindings.IsPetFunctionUnlocked(1), "The official HasAnyPetCondition changed.");
                Transform pet = root.transform.Find(data.skillUI.pet.Substring("/FightMainPanel/".Length));
                var listRect = (RectTransform)root.transform.Find(data.skillUI.rect.Substring("/FightMainPanel/".Length));
                float offsetY = listRect.offsetMax.y;
                foreach (bool unlocked in new[] { false, true, false })
                {
                    view.RefreshPetFunction(unlocked);
                    Require(pet.gameObject.activeSelf == unlocked
                        && Mathf.Abs(listRect.offsetMax.x - (unlocked ? 0 : data.skillUI.noUnlockPetOffset)) < 0.001f
                        && listRect.offsetMax.y == offsetY, "Pet visibility or the original right offset differs.");
                }
                var renderer = new OfficialBattleHudRenderer(OfficialBattleHudCatalog.LoadDefault());
                // Exercise the official state switches against the same serialized main prefab.
                GameObject stateRoot = renderer.Create("main", canvas.transform);
                var cards = OfficialBattleSkillSlotBindings.Mount(renderer);
                for (int slot = 0; slot < cards.Length; slot++)
                {
                    BattleSkillSlotBinding binding = data.slots[slot];
                    foreach (int state in Enumerable.Range(0, 8))
                    {
                        cards[slot].SetUIState((WorldGameSkillIcon.State)state);
                        AssertState(stateRoot, binding, state);
                    }
                    cards[slot].SetSkillCd(3);
                    AssertState(stateRoot, binding, 3);
                    Require(Find(stateRoot, binding.cooldown).GetComponent<TMP_Text>().text == "3", "Cooldown text lost its value.");
                    cards[slot].SetSkillCd(0);
                    AssertState(stateRoot, binding, 1);
                }
                stateRoot.SetActive(false);

                var checkpoint = new FightBattleCheckpoint();
                var player = new FightBattleUnitState { Id = 1, Hp = 100, MaxHp = 100 };
                checkpoint.Units.Add(player);
                var first = new FightBattleSkillDefinition { Slot = 0, ItemId = 10012 };
                var third = new FightBattleSkillDefinition { Slot = 2, ItemId = 10013 };
                player.Skills.Add(first); player.Skills.Add(third);
                player.SkillState.IsDriving = true;
                player.SkillState.Round = 1;
                var cooldown = new FightSkillSlotState { Slot = 0, Cooldown = 3, LastRound = 1 };
                player.SkillState.Slots.Add(cooldown);
                Sprite sprite = root.GetComponentsInChildren<Image>(true).First(image => image.sprite != null).sprite;
                int count = root.GetComponentsInChildren<Transform>(true).Length;
                int[] siblings = data.slots.Select(binding => Find(root, binding.owner).GetSiblingIndex()).ToArray();
                view.RefreshSkillSlots(checkpoint, _ => sprite);
                AssertState(root, data.slots[0], 3);
                AssertState(root, data.slots[1], 0);
                AssertState(root, data.slots[2], 1);
                AssertState(root, data.slots[3], 0);
                first.LimitedTimes = 1; cooldown.ReleasedTimes = 1;
                view.RefreshSkillSlots(checkpoint, _ => sprite);
                AssertState(root, data.slots[0], 5);
                first.ProfessionAvailability = FightSkillProfessionAvailability.Mismatch;
                view.RefreshSkillSlots(checkpoint, _ => sprite);
                AssertState(root, data.slots[0], 6);
                first.ProfessionAvailability = FightSkillProfessionAvailability.Available;
                var step = new FightBattleStepEvent { ActorId = 1 };
                step.SkillCasts.Add(new FightBattleSkillCastEvent { Slot = 2 });
                view.RefreshSkillSlots(checkpoint, _ => sprite, step);
                AssertState(root, data.slots[2], 2);
                player.Skills.Clear();
                view.RefreshSkillSlots(checkpoint, _ => sprite);
                foreach (BattleSkillSlotBinding slot in data.slots) AssertState(root, slot, 0);
                Require(root.GetComponentsInChildren<Transform>(true).Length == count
                    && siblings.SequenceEqual(data.slots.Select(binding => Find(root, binding.owner).GetSiblingIndex())),
                    "Refreshing skills appended or reordered the original slots.");
                Debug.Log("Original skill slots passed: four serialized references, all original states including absent Disabled, CD text, profession/count precedence, applied/empty transitions, no appended cards or sibling changes; HasAnyPetCondition and pet layout offsets 70/0/70.");
            }
            finally { UnityEngine.Object.DestroyImmediate(canvas); }
        }

        public static void VerifyActual(GameObject root, FightBattleCheckpoint checkpoint)
        {
            OfficialBattleSkillQualityAcceptance.VerifyActual(root, checkpoint);
            BattleSkillSlotData data = OfficialBattleSkillSlotBindings.Load();
            var player = checkpoint.Units.Single(unit => unit.Id == 1);
            // The isolated combat profile starts without any owned pet.
            Transform pet = root.transform.Find(data.skillUI.pet.Substring("/FightMainPanel/".Length));
            var listRect = (RectTransform)root.transform.Find(data.skillUI.rect.Substring("/FightMainPanel/".Length));
            Require(!pet.gameObject.activeSelf && Mathf.Abs(listRect.offsetMax.x - data.skillUI.noUnlockPetOffset) < 0.001f,
                "The saved profile without pets did not hide the pet slot and restore the official offset.");
            Canvas.ForceUpdateCanvases();
            foreach (BattleSkillSlotBinding binding in data.slots)
            {
                var rect = Find(root, binding.owner) as RectTransform;
                Require(rect != null, "An original skill slot is missing.");
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                Canvas canvas = rect.GetComponentInParent<Canvas>();
                Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                foreach (Vector3 corner in corners)
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    Require(point.x >= 0 && point.x <= Screen.width && point.y >= 0 && point.y <= Screen.height,
                        "An original skill slot is outside the Game view: " + binding.owner + " at " + point
                        + " in " + Screen.width + "x" + Screen.height);
                }
            }
            Require(!root.GetComponentsInChildren<Transform>(true).Any(node => node.name.StartsWith("GameSkillIconItem_", StringComparison.Ordinal)),
                "The battle appended a replacement skill card.");
            for (int index = 0; index < data.slots.Length; index++)
                if (!player.Skills.Any(skill => skill.Slot == index)) AssertState(root, data.slots[index], 0);
            Debug.Log("Original skill slots Play Mode passed: original four objects retained inside " + Screen.width + "x" + Screen.height + ", empty slots use None, no appended cards.");
        }

        private static void AssertState(GameObject root, BattleSkillSlotBinding binding, int state)
        {
            var selected = binding.states.FirstOrDefault(value => value.state == state)?.objects ?? Array.Empty<string>();
            foreach (string path in binding.states.SelectMany(value => value.objects).Distinct())
                Require(Find(root, path).gameObject.activeSelf == selected.Contains(path), "Original skill state " + state + " differs at " + path);
        }

        private static Transform Find(GameObject root, string path)
        {
            // The official prefab has duplicate sibling names. Export paths disambiguate them
            // with (1), but the runtime keeps their original names and serialized references.
            BattleSkillSlotData data = OfficialBattleSkillSlotBindings.Load();
            for (int slot = 0; slot < data.slots.Length; slot++)
            {
                string owner = data.slots[slot].owner;
                if (path != owner && !path.StartsWith(owner + "/", StringComparison.Ordinal)) continue;
                Transform card = root.GetComponentsInChildren<WorldGameSkillIcon>(true)
                    .Single(value => value.SkillIndex == slot).transform;
                if (path == owner) return card;
                return card.Find(path.Substring(owner.Length + 1))
                    ?? throw new InvalidOperationException("Missing original skill child: " + path);
            }
            throw new InvalidOperationException("Path is outside the original skill slots: " + path);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
