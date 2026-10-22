using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleHudView
    {
        private const string RoundTextPath = "/FightMainPanel/Content/Top/FixedContent/RoundTitle/RoundText";
        private const string SkillSlotsPath = "/FightMainPanel/Content/Bottom/Anim/SkillListContent/BgSlot";
        private const string TimelineSlotPath = "/FightMainPanel/Content/Top/FixedContent/RoundUI/Linear/Slot";
        private const float TimelineMinDistance = 0.05f;
        private const float TimelineMoveDuration = 0.2f;
        private readonly OfficialBattleHudRenderer renderer;
        private readonly OfficialBattleHudRenderer skillRenderer;
        private readonly OfficialBattleHudRenderer timelineRenderer;
        private readonly OfficialBattleHudRenderer speedRenderer;
        private const string SpeedRootPath = "/FightMainPanelSpeedUI";
        private const string SpeedButtonPath = "/FightMainPanelSpeedUI/Btn";
        private const string SpeedLabelPath = "/FightMainPanelSpeedUI/Btn/Text";
        private const string SpeedLockedPath = "/FightMainPanelSpeedUI/Btn/Locked";
        private GameObject speedRoot;
        private readonly Dictionary<int, GameObject> skillCards = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> timelineCards = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Vector3> timelineFrom = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> timelineMove = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> timelineTargets = new Dictionary<int, Vector3>();
        private GameObject root;
        private string roundTextFormat;
        private RectTransform timelineSlot;
        private Vector2 timelineBounds;
        private float timelineMoveTime;
        private bool timelineMoving;

        public event Action<string> ControlClicked;
        public event Action SpeedCycled;

        public OfficialBattleHudView(OfficialBattleHudCatalog catalog)
        {
            OfficialBattleHudCatalog resolved = catalog ?? OfficialBattleHudCatalog.LoadDefault();
            renderer = new OfficialBattleHudRenderer(resolved);
            skillRenderer = new OfficialBattleHudRenderer(resolved);
            timelineRenderer = new OfficialBattleHudRenderer(resolved);
            speedRenderer = new OfficialBattleHudRenderer(resolved);
        }

        public GameObject Mount(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = renderer.Create("main", parent, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button != null) button.onClick.AddListener(() => ControlClicked?.Invoke(path));
            });
            GameObject slot = renderer.Find(TimelineSlotPath);
            timelineSlot = slot == null ? null : slot.GetComponent<RectTransform>();
            if (timelineSlot == null) throw new InvalidOperationException("Official battle timeline slot is missing.");
            timelineSlot.gameObject.SetActive(true);
            timelineBounds = new Vector2(timelineSlot.rect.xMax, timelineSlot.rect.xMin);
            speedRoot = speedRenderer.Create("speed", root.transform, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button != null && path == SpeedButtonPath)
                    button.onClick.AddListener(() => SpeedCycled?.Invoke());
            });
            speedRoot.name = "FightMainPanelSpeedUI";
            return root;
        }

        /// <summary>
        /// Binds the official speed button to the resolved speed table. The button text is the
        /// official <c>SpeedUIs</c> label, not the multiplier; the lock icon is shown when no
        /// alternative official speed is currently selectable.
        /// </summary>
        public void RefreshSpeed(FightBattleSpeedSettings settings, int index,
            IReadOnlyList<int> unlocked)
        {
            if (settings == null || speedRoot == null) return;
            bool canSwitch = unlocked != null && unlocked.Count > 1;
            speedRenderer.SetText(SpeedLabelPath, settings.Label(index));
            speedRenderer.SetActive(SpeedLockedPath, !canSwitch);
            GameObject button = speedRenderer.Find(SpeedButtonPath);
            Button component = button == null ? null : button.GetComponent<Button>();
            if (component != null) component.interactable = true;
        }

        public void Refresh(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            GameObject roundObject = renderer.Find(RoundTextPath);
            TMP_Text roundText = roundObject == null ? null : roundObject.GetComponent<TMP_Text>();
            if (roundText == null) throw new InvalidOperationException("Official battle round label is missing.");
            if (roundTextFormat == null) roundTextFormat = roundText.text;
            int currentRound = checkpoint.Outcome == null ? checkpoint.Timeline.Round : checkpoint.Outcome.Round;
            int round = Mathf.Min(checkpoint.MaxRound, Mathf.Max(1, currentRound));
            roundText.text = string.Format(CultureInfo.CurrentCulture, roundTextFormat, round, checkpoint.MaxRound);
        }

        public void RefreshSkillSlots(FightBattleCheckpoint checkpoint, Func<int, Sprite> skillIcon,
            FightBattleStepEvent stepEvent = null)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (skillIcon == null) throw new ArgumentNullException(nameof(skillIcon));
            if (root == null) throw new InvalidOperationException("Mount the official battle HUD before refreshing skill slots.");

            GameObject slotObject = renderer.Find(SkillSlotsPath);
            if (slotObject == null) throw new InvalidOperationException("Official battle skill slot container is missing.");
            var placeholders = new List<RectTransform>();
            foreach (Transform child in slotObject.transform)
                if (child.name.StartsWith("GameSkillIconItemDebug", StringComparison.Ordinal))
                    placeholders.Add(child as RectTransform);

            FightBattleUnitState player = null;
            foreach (FightBattleUnitState unit in checkpoint.Units)
                if (unit.Id == 1) { player = unit; break; }
            if (player == null) throw new InvalidOperationException("Battle HUD cannot find the main player unit.");

            var cooldowns = new FightSkillState(player.SkillState);
            var visible = new HashSet<int>();
            for (int i = 0; i < player.Skills.Count; i++)
            {
                FightBattleSkillDefinition skill = player.Skills[i];
                if (skill == null || skill.Slot < 0 || skill.Slot >= placeholders.Count) continue;
                visible.Add(skill.Slot);
                if (!skillCards.TryGetValue(skill.Slot, out GameObject card))
                {
                    RectTransform placeholder = placeholders[skill.Slot];
                    card = skillRenderer.Create("skill", slotObject.transform);
                    card.name = "GameSkillIconItem_" + skill.Slot;
                    CopyLayout((RectTransform)card.transform, placeholder);
                    placeholder.gameObject.SetActive(false);
                    foreach (Button button in card.GetComponentsInChildren<Button>(true)) button.interactable = false;
                    skillCards.Add(skill.Slot, card);
                }

                Sprite icon = skillIcon(skill.ItemId);
                if (icon == null) throw new InvalidOperationException("Official battle skill icon is not bound for item " + skill.ItemId);
                Transform iconNode = card.transform.Find("Content/Bg/Anim/Icon/SkillIconImg");
                Image iconImage = iconNode == null ? null : iconNode.GetComponent<Image>();
                if (iconImage == null) throw new InvalidOperationException("Official battle skill icon image is missing.");
                iconImage.sprite = icon;

                long leftCooldown = cooldowns.LeftCooldown(skill.Slot);
                Transform cooldown = card.transform.Find("Content/Bg/Anim/cd");
                TMP_Text cooldownText = cooldown == null ? null : cooldown.Find("Text")?.GetComponent<TMP_Text>();
                if (cooldownText == null) throw new InvalidOperationException("Official battle skill cooldown label is missing.");
                FightSkillSlotState slotState = FindSlot(player.SkillState, skill.Slot);
                bool limitedState = skill.LimitedTimes >= 0 && slotState != null
                    && slotState.ReleasedTimes >= skill.LimitedTimes;
                cooldown.gameObject.SetActive(!limitedState && leftCooldown > 0);
                cooldownText.text = leftCooldown.ToString();

                Transform limitedNode = card.transform.Find("Content/Bg/Anim/LimitedTimes");
                if (limitedNode != null)
                    limitedNode.gameObject.SetActive(limitedState);
                Transform playing = card.transform.Find("Content/Bg/Anim/Playing");
                if (playing != null)
                    playing.gameObject.SetActive(stepEvent != null && stepEvent.ActorId == player.Id
                        && HasCast(stepEvent, skill.Slot));
                card.SetActive(true);
            }

            for (int i = 0; i < placeholders.Count; i++)
            {
                bool hasSkill = visible.Contains(i);
                placeholders[i].gameObject.SetActive(!hasSkill);
                if (skillCards.TryGetValue(i, out GameObject card)) card.SetActive(hasSkill);
            }
        }

        public void RefreshTimeline(FightBattleCheckpoint checkpoint,
            Func<FightBattleUnitState, Sprite> portraitIcon,
            Func<FightBattleUnitState, Sprite> factionIcon = null, bool animate = true)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (portraitIcon == null) throw new ArgumentNullException(nameof(portraitIcon));
            if (root == null || timelineSlot == null)
                throw new InvalidOperationException("Mount the official battle HUD before refreshing the timeline.");

            var states = new Dictionary<int, FightBattleUnitState>();
            foreach (FightBattleUnitState unit in checkpoint.Units)
                if (unit != null && unit.Hp > 0 && IsWaveSpawned(checkpoint, unit.Id)) states.Add(unit.Id, unit);

            var entries = new List<FightTurnEntry>();
            foreach (FightTurnEntry entry in checkpoint.Timeline.Entries)
                if (entry != null && states.ContainsKey(entry.Id)) entries.Add(entry);

            var present = new HashSet<int>();
            double totalInterval = 0;
            foreach (FightBattleUnitState unit in states.Values)
            {
                double interval = Math.Max(0, unit.Turn.IntervalTicks);
                if (interval > totalInterval) totalInterval = interval;
            }

            var timePositions = new List<double>(entries.Count);
            timePositions.Add(0);
            for (int i = 1; i < entries.Count; i++)
            {
                double spacing = entries[i].Next - entries[i - 1].Next;
                timePositions.Add(timePositions[i - 1] + Math.Max(spacing, totalInterval * TimelineMinDistance));
            }
            double timelineDuration = timePositions.Count == 0
                ? totalInterval : Math.Max(timePositions[timePositions.Count - 1], totalInterval);

            timelineFrom.Clear();
            timelineMove.Clear();
            timelineTargets.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                FightTurnEntry entry = entries[i];
                FightBattleUnitState unit = states[entry.Id];
                GameObject card;
                bool isNew = !timelineCards.TryGetValue(unit.Id, out card);
                if (isNew)
                {
                    card = timelineRenderer.Create("round_character", timelineSlot);
                    card.name = "GameRoundCharacterItem_" + unit.Id;
                    timelineCards.Add(unit.Id, card);
                }

                bool acting = i == 0;
                RefreshTimelineCard(card, unit, portraitIcon(unit), factionIcon == null ? null : factionIcon(unit), acting);
                present.Add(unit.Id);
                RectTransform rect = card.transform as RectTransform;
                Vector3 current = rect.localPosition;
                float percent = timelineDuration < 0.00001
                    ? 0 : (float)(timePositions[i] / timelineDuration);
                Vector3 target = new Vector3(Mathf.Lerp(timelineBounds.x, timelineBounds.y, percent), 0, 0);
                timelineFrom.Add(unit.Id, isNew ? target : current);
                timelineTargets.Add(unit.Id, target);
                timelineMove.Add(unit.Id, isNew ? Vector3.zero : current.x <= target.x
                    ? target - current
                    : target - new Vector3(timelineBounds.y, 0, 0)
                        + new Vector3(timelineBounds.x, 0, 0) - current);
                if (!animate || isNew || !card.activeSelf)
                    rect.localPosition = target;
                card.SetActive(true);
                card.transform.SetSiblingIndex(entries.Count - i - 1);
            }

            var removed = new List<int>();
            foreach (KeyValuePair<int, GameObject> item in timelineCards)
                if (!present.Contains(item.Key)) removed.Add(item.Key);
            foreach (int id in removed)
            {
                UnityEngine.Object.Destroy(timelineCards[id]);
                timelineCards.Remove(id);
            }

            timelineMoving = animate && entries.Count > 0;
            timelineMoveTime = 0;
            if (!timelineMoving) SetTimelinePositions(1);
        }

        public void Tick(float deltaTime)
        {
            if (!timelineMoving) return;
            timelineMoveTime = Mathf.Min(TimelineMoveDuration, timelineMoveTime + Mathf.Max(0, deltaTime));
            float percent = timelineMoveTime / TimelineMoveDuration;
            SetTimelinePositions(percent);
            if (percent >= 1) timelineMoving = false;
        }

        public GameObject Find(string path) => renderer.Find(path);
        public bool SetText(string path, string value) => renderer.SetText(path, value);
        public bool SetSprite(string path, Sprite sprite) => renderer.SetSprite(path, sprite);
        public bool SetActive(string path, bool active) => renderer.SetActive(path, active);

        private static FightSkillSlotState FindSlot(FightSkillAgentState state, int slot)
        {
            foreach (FightSkillSlotState candidate in state.Slots)
                if (candidate != null && candidate.Slot == slot) return candidate;
            return null;
        }

        private static bool HasCast(FightBattleStepEvent stepEvent, int slot)
        {
            if (stepEvent.SkillCasts == null) return false;
            foreach (FightBattleSkillCastEvent skillCast in stepEvent.SkillCasts)
                if (skillCast != null && skillCast.Slot == slot) return true;
            return false;
        }

        private static bool IsWaveSpawned(FightBattleCheckpoint checkpoint, int unitId)
        {
            if (checkpoint.Waves == null || checkpoint.Waves.Count == 0) return true;
            foreach (FightBattleWaveState wave in checkpoint.Waves)
            {
                if (wave == null || wave.UnitIds == null || !wave.UnitIds.Contains(unitId)) continue;
                return wave.Spawned;
            }
            return true;
        }

        private static void RefreshTimelineCard(GameObject card, FightBattleUnitState unit,
            Sprite portrait, Sprite factionSprite, bool acting)
        {
            if (portrait == null)
                throw new InvalidOperationException("Official battle timeline portrait is not bound for unit " + unit.Id);
            bool monster = unit.MonsterId > 0;
            string iconPath = monster
                ? "IconGo/MonsterIconImg"
                : "IconGo/PlayerHeadETC2Go/HeadImg";
            Transform icon = card.transform.Find(iconPath);
            Image image = icon == null ? null : icon.GetComponent<Image>();
            if (image == null) throw new InvalidOperationException("Official battle timeline portrait node is missing: " + iconPath);
            image.sprite = portrait;
            Transform monsterIcon = card.transform.Find("IconGo/MonsterIconImg");
            Transform playerIcon = card.transform.Find("IconGo/PlayerHeadETC2Go");
            if (monsterIcon != null) monsterIcon.gameObject.SetActive(monster);
            if (playerIcon != null) playerIcon.gameObject.SetActive(!monster);
            Transform actingBackground = card.transform.Find("WeGo/WeBg");
            if (actingBackground != null) actingBackground.gameObject.SetActive(acting);
            Transform faction = card.transform.Find("FactionImg");
            Image factionImage = faction == null ? null : faction.GetComponent<Image>();
            if (factionImage != null)
            {
                factionImage.sprite = factionSprite;
                faction.gameObject.SetActive(factionSprite != null);
            }
        }

        private void SetTimelinePositions(float percent)
        {
            foreach (KeyValuePair<int, Vector3> entry in timelineFrom)
            {
                if (!timelineCards.TryGetValue(entry.Key, out GameObject card)) continue;
                Vector3 value = entry.Value + timelineMove[entry.Key] * percent;
                if (value.x > timelineBounds.x) value.x -= timelineBounds.x - timelineBounds.y;
                if (percent >= 1) value = timelineTargets[entry.Key];
                card.transform.localPosition = value;
            }
        }

        private static void CopyLayout(RectTransform target, RectTransform source)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.anchoredPosition = source.anchoredPosition;
            target.sizeDelta = source.sizeDelta;
            target.pivot = source.pivot;
            target.localScale = source.localScale;
            target.localRotation = source.localRotation;
        }
    }
}
