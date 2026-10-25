using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed partial class OfficialBattleHudView
    {
        public struct BossHpBarDisplay
        {
            public float TopFill;
            public float CenterFill;
            public int RemainingPlies;
            public bool ShowCenter;
            public bool TopUsesShield;
            public bool CenterUsesShield;
            public bool BottomUsesShield;
            public bool BottomEmpty;
            public int LayerIndex;
        }

        private sealed class BossHpBarSprites
        {
            public Sprite[] Hp;
            public Sprite Shield;
            public Sprite Empty;
            public Color[] ShieldColors;
            public float MoveSpeed;
        }

        private sealed class BossHpBarRuntime
        {
            public int UnitId;
            public FightBattleUnitState Unit;
            public double TargetHp;
            public double TargetTotal;
            public double DisplayHp;
            public double DisplayTotal;
            public bool Initialized;
        }

        private const string RoundTextPath = "/FightMainPanel/Content/Top/FixedContent/RoundTitle/RoundText";
        private const string RoundTitlePath = "/FightMainPanel/Content/Top/FixedContent/RoundTitle";
        private const string RoundHintAnimation = "UIGameTop_GameProgress_Hint";
        private const int RoundCountDown = 5;
        private const string SkillSlotsPath = "/FightMainPanel/Content/Bottom/Anim/SkillListContent/BgSlot";
        private const string TimelineSlotPath = "/FightMainPanel/Content/Top/FixedContent/RoundUI/Linear/Slot";
        private const float TimelineMinDistance = 0.05f;
        private const float TimelineMoveDuration = 0.2f;
        private readonly OfficialBattleHudRenderer renderer;
        private readonly OfficialBattleHudRenderer skillRenderer;
        private readonly OfficialBattleHudRenderer timelineRenderer;
        private readonly OfficialBattleHudRenderer speedRenderer;
        private readonly OfficialBattleHudRenderer bossRenderer;
        private readonly OfficialBattleHudCatalog resolvedCatalog;
        private readonly OfficialBattleHudRenderer statusRenderer;
        private readonly OfficialBattleHudRenderer stageStatusDialogRenderer;
        private const string SpeedRootPath = "/FightMainPanelSpeedUI";
        private const string SpeedButtonPath = "/FightMainPanelSpeedUI/Btn";
        private const string SpeedLabelPath = "/FightMainPanelSpeedUI/Btn/Text";
        private const string SpeedLockedPath = "/FightMainPanelSpeedUI/Btn/Locked";
        private GameObject speedRoot;
        private GameObject bossRoot;
        private const string BossRootPath = "/FightMainPanelBossUI";
        private const string BossFrontPath = "/FightMainPanelBossUI/Anim/FrontItem";
        private const string BossBehindPath = "/FightMainPanelBossUI/Anim/BehindItem";
        private const int MaxBossCount = 2;
        private readonly List<FightBattleUnitState> bossUnits = new List<FightBattleUnitState>();
        private readonly Dictionary<string, BossHpBarRuntime> bossHpBarStates =
            new Dictionary<string, BossHpBarRuntime>(StringComparer.Ordinal);
        private BossHpBarSprites bossHpBarSprites;
        private readonly List<GameObject> stageStatusItems = new List<GameObject>();
        private readonly List<int> stageStatusIds = new List<int>();
        private readonly List<GameObject> stageStatusDialogItems = new List<GameObject>();
        private readonly Dictionary<int, GameObject> skillCards = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, GameObject> timelineCards = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, Vector3> timelineFrom = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> timelineMove = new Dictionary<int, Vector3>();
        private readonly Dictionary<int, Vector3> timelineTargets = new Dictionary<int, Vector3>();
        private GameObject root;
        private OfficialBattleHudAnimationPlayer mainAnimationPlayer;
        private GameObject stageStatusDialogRoot;
        private Transform stageStatusDialogContent;
        private string roundTextFormat;
        private int? lastShownRound;
        private RectTransform timelineSlot;
        private Vector2 timelineBounds;
        private float timelineMoveTime;
        private bool timelineMoving;

        public event Action<string> ControlClicked;
        public event Action SpeedCycled;

        public OfficialBattleHudView(OfficialBattleHudCatalog catalog)
        {
            OfficialBattleHudCatalog resolved = catalog ?? OfficialBattleHudCatalog.LoadDefault();
            resolvedCatalog = resolved;
            renderer = new OfficialBattleHudRenderer(resolved);
            skillRenderer = new OfficialBattleHudRenderer(resolved);
            timelineRenderer = new OfficialBattleHudRenderer(resolved);
            speedRenderer = new OfficialBattleHudRenderer(resolved);
            bossRenderer = new OfficialBattleHudRenderer(resolved);
            statusRenderer = new OfficialBattleHudRenderer(resolved);
            stageStatusDialogRenderer = new OfficialBattleHudRenderer(resolved);
        }

        public GameObject Mount(Transform parent)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            root = renderer.Create("main", parent, (path, node) =>
            {
                Button button = node.GetComponent<Button>();
                if (button == null) return;
                if (path == StageStatusPath) button.onClick.AddListener(OpenStageStatusDialog);
                else button.onClick.AddListener(() => ControlClicked?.Invoke(path));
            });
            mainAnimationPlayer = renderer.InitializeAnimations("main", root);
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
            bossRoot = bossRenderer.Create("boss", root.transform);
            bossRoot.SetActive(false);
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
            OfficialBattleRoundDisplay display = OfficialBattleRoundDisplayResolver.Resolve(checkpoint);
            if (!lastShownRound.HasValue || lastShownRound.Value != display.Round)
            {
                lastShownRound = display.Round;
                if (display.MaxRound - display.Round <= RoundCountDown)
                    mainAnimationPlayer?.Play(RoundTitlePath, RoundHintAnimation);
            }
            roundText.richText = true;
            roundText.text = OfficialBattleRoundTextFormatter.Format(roundTextFormat, display, RoundCountDown);
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
                bool professionMismatch = skill.ProfessionAvailability == FightSkillProfessionAvailability.Mismatch;
                bool limitedState = !professionMismatch && skill.LimitedTimes >= 0 && slotState != null
                    && slotState.ReleasedTimes >= skill.LimitedTimes;
                cooldown.gameObject.SetActive(!professionMismatch && !limitedState && leftCooldown > 0);
                cooldownText.text = leftCooldown.ToString();

                Transform limitedNode = card.transform.Find("Content/Bg/Anim/LimitedTimes");
                if (limitedNode != null)
                    limitedNode.gameObject.SetActive(limitedState);
                Transform professionMismatchNode = card.transform.Find("Content/Bg/Anim/ProfessionMismatch");
                if (professionMismatchNode != null)
                    professionMismatchNode.gameObject.SetActive(professionMismatch);
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

        /// <summary>
        /// Binds the official boss panel: at most two living bosses whose wave has spawned, matching
        /// FightMainPanelBossUI.CheckShowBossCondition (boss monster types, enemy side). HP uses the
        /// filled TopImg and the official status list shows background + stack count. Resilience uses
        /// the recovered official Fill image directly because the serialized Slider component is absent.
        /// </summary>
        private const string StageStatusPath =
            "/FightMainPanel/Content/Top/FixedContent/StageStatus";

        /// <summary>
        /// Official stage status strip, populated from the resolved EntryInfo icon and background
        /// bindings for every configured view status.
        /// </summary>
        public void RefreshStageStatus(FightBattleCheckpoint checkpoint)
        {
            if (checkpoint == null) return;
            stageStatusIds.Clear();
            GameObject strip = renderer.Find(StageStatusPath);
            if (strip == null) return;
            GameObject template = renderer.Find(StageStatusPath + "/StatusUI");
            if (template == null) return;
            var displays = new List<int>();
            foreach (int statusId in checkpoint.StageStatus == null
                ? new List<int>() : FightStageStatusRuntime.ViewStatusIds(checkpoint.StageStatus))
                if (resolvedCatalog.TryGetStatusDisplay(statusId, out Sprite icon, out _, out _,
                    out _, out _, out _, out _) && icon != null)
                    displays.Add(statusId);
            stageStatusIds.AddRange(displays);
            if (stageStatusIds.Count == 0 && stageStatusDialogRoot != null)
                stageStatusDialogRoot.SetActive(false);

            strip.SetActive(displays.Count > 0);
            RectTransform stripRect = strip.transform as RectTransform;
            RectTransform templateRect = template.transform as RectTransform;
            if (stripRect != null && templateRect != null)
            {
                float itemWidth = templateRect.sizeDelta.x * templateRect.localScale.x;
                float stride = itemWidth * 0.72f;
                stripRect.sizeDelta = new Vector2(Mathf.Max(itemWidth,
                    itemWidth + Math.Max(0, displays.Count - 1) * stride), stripRect.sizeDelta.y);
                Transform background = strip.transform.Find("StatusBack");
                if (background != null) background.gameObject.SetActive(displays.Count > 0);
                for (int index = 0; index < displays.Count; index++)
                {
                    GameObject item = StageStatusItem(template, index);
                    item.SetActive(true);
                    RectTransform itemRect = item.transform as RectTransform;
                    if (itemRect != null)
                    {
                        itemRect.anchorMin = itemRect.anchorMax = new Vector2(0f, 0.5f);
                        itemRect.pivot = new Vector2(0.5f, 0.5f);
                        itemRect.anchoredPosition = new Vector2(itemWidth * 0.5f + index * stride, 0f);
                    }
                    resolvedCatalog.TryGetStatusDisplay(displays[index], out Sprite icon,
                        out Sprite backgroundSprite, out _, out _, out _, out _, out _);
                    Image backgroundImage = item.GetComponent<Image>();
                    if (backgroundImage != null) backgroundImage.sprite = backgroundSprite;
                    Transform iconNode = item.transform.Find("StatusImg");
                    Image iconImage = iconNode == null ? null : iconNode.GetComponent<Image>();
                    if (iconImage != null) iconImage.sprite = icon;
                    if (iconNode != null) iconNode.gameObject.SetActive(iconImage != null && icon != null);
                    Transform countNode = item.transform.Find("CountTxt");
                    if (countNode != null) countNode.gameObject.SetActive(false);
                }
            }
            for (int index = displays.Count; index < stageStatusItems.Count; index++)
                if (stageStatusItems[index] != template) stageStatusItems[index].SetActive(false);
            if (displays.Count == 0) template.SetActive(false);
        }

        private GameObject StageStatusItem(GameObject template, int index)
        {
            if (index == 0)
            {
                if (stageStatusItems.Count == 0) stageStatusItems.Add(template);
                return template;
            }
            while (stageStatusItems.Count <= index)
            {
                GameObject clone = UnityEngine.Object.Instantiate(template, template.transform.parent, false);
                clone.name = "StatusUI_" + stageStatusItems.Count.ToString(CultureInfo.InvariantCulture);
                stageStatusItems.Add(clone);
            }
            return stageStatusItems[index];
        }

        private void OpenStageStatusDialog()
        {
            if (stageStatusIds.Count == 0) return;
            if (stageStatusDialogRoot == null)
            {
                stageStatusDialogRoot = stageStatusDialogRenderer.Create("stage_status", root.transform);
                GameObject content = stageStatusDialogRenderer.Find(
                    "/FightStageStatusDialog/BgDialog1/Content/LoopVerticalScrollRect/Content");
                if (content == null)
                    throw new InvalidOperationException("Official stage status dialog content is missing.");
                stageStatusDialogContent = content.transform;
                var overlay = new GameObject("OutsideClickBlocker", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image), typeof(Button));
                overlay.layer = stageStatusDialogRoot.layer;
                RectTransform overlayRect = (RectTransform)overlay.transform;
                overlayRect.SetParent(stageStatusDialogRoot.transform, false);
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = Vector2.zero;
                overlayRect.offsetMax = Vector2.zero;
                Image overlayImage = overlay.GetComponent<Image>();
                overlayImage.color = new Color(0f, 0f, 0f, 0f);
                overlayImage.raycastTarget = true;
                Button overlayButton = overlay.GetComponent<Button>();
                overlayButton.transition = Selectable.Transition.None;
                overlayButton.navigation = new Navigation { mode = Navigation.Mode.None };
                overlayButton.onClick.AddListener(CloseStageStatusDialog);
                overlay.transform.SetAsFirstSibling();
            }

            int shown = 0;
            foreach (int statusId in stageStatusIds)
            {
                if (!resolvedCatalog.TryGetStatusDetails(statusId, out _, out _)) continue;
                GameObject item = StageStatusDialogItem(shown++);
                BindStageStatusDialogItem(item, statusId);
                item.SetActive(true);
            }
            for (int index = shown; index < stageStatusDialogItems.Count; index++)
                stageStatusDialogItems[index].SetActive(false);
            if (shown == 0) return;
            stageStatusDialogRoot.transform.SetAsLastSibling();
            stageStatusDialogRoot.SetActive(true);
        }

        private GameObject StageStatusDialogItem(int index)
        {
            while (stageStatusDialogItems.Count <= index)
            {
                var itemRenderer = new OfficialBattleHudRenderer(resolvedCatalog);
                GameObject item = itemRenderer.Create("stage_status_item", stageStatusDialogContent);
                item.name = "FightStageStatusDialogItemUI_" + stageStatusDialogItems.Count
                    .ToString(CultureInfo.InvariantCulture);
                stageStatusDialogItems.Add(item);
            }
            return stageStatusDialogItems[index];
        }

        private void BindStageStatusDialogItem(GameObject item, int statusId)
        {
            if (!resolvedCatalog.TryGetStatusDetails(statusId, out string name, out string description))
                return;
            resolvedCatalog.TryGetStatusSprites(statusId, out Sprite icon, out Sprite background, out _);
            Transform iconNode = item.transform.Find("Bg/IconImage");
            Image iconImage = iconNode == null ? null : iconNode.GetComponent<Image>();
            if (iconImage != null) iconImage.sprite = icon;
            Transform backgroundNode = item.transform.Find("Bg");
            Image backgroundImage = backgroundNode == null ? null : backgroundNode.GetComponent<Image>();
            if (backgroundImage != null) backgroundImage.sprite = background;
            Transform nameNode = item.transform.Find("Labels/NameText");
            TMP_Text nameText = nameNode == null ? null : nameNode.GetComponent<TMP_Text>();
            if (nameText != null) nameText.text = name;
            Transform descriptionNode = item.transform.Find("Labels/ContentText");
            TMP_Text descriptionText = descriptionNode == null ? null : descriptionNode.GetComponent<TMP_Text>();
            if (descriptionText != null) descriptionText.text = description;
        }

        private void CloseStageStatusDialog()
        {
            if (stageStatusDialogRoot != null) stageStatusDialogRoot.SetActive(false);
        }

        public void RefreshBoss(FightBattleCheckpoint checkpoint,
            Func<FightBattleUnitState, string> nameResolver = null)
        {
            if (checkpoint == null) throw new ArgumentNullException(nameof(checkpoint));
            if (bossRoot == null) return;
            bossUnits.Clear();
            foreach (FightBattleUnitState unit in checkpoint.Units)
            {
                if (unit == null || unit.Hp <= 0 || unit.Ended || unit.Side == 1 || unit.MonsterId <= 0)
                    continue;
                if (unit.MonsterType < 3 || unit.MonsterType > 5) continue;
                if (!IsWaveSpawned(checkpoint, unit.Id)) continue;
                bossUnits.Add(unit);
                if (bossUnits.Count >= MaxBossCount) break;
            }
            if (bossUnits.Count == 0)
            {
                bossRoot.SetActive(false);
                return;
            }
            bossRoot.SetActive(true);
            BindBossItem(BossFrontPath, bossUnits[0], nameResolver);
            if (bossUnits.Count > 1)
            {
                bossRenderer.SetActive(BossBehindPath, true);
                BindBossItem(BossBehindPath, bossUnits[1], nameResolver);
            }
            else bossRenderer.SetActive(BossBehindPath, false);
        }

        /// <summary>
        /// Official boss status list using each status' resolved EntryInfo icon and background.
        /// </summary>
        private bool RefreshBossStatuses(Transform parent, FightBattleUnitState unit)
        {
            if (unit == null || unit.Statuses == null) return false;
            int created = 0;
            foreach (FightBattleStatusState status in unit.Statuses)
            {
                if (status == null || status.StackCount <= 0
                    || !resolvedCatalog.TryGetStatusDisplay(status.ClassId, out Sprite icon,
                        out Sprite backgroundSprite, out _, out bool showStackCount,
                        out _, out _, out _)) continue;
                GameObject item = BossStatusItem(parent, created);
                item.SetActive(true);
                Image background = item.GetComponent<Image>();
                if (background != null) background.sprite = backgroundSprite;
                Transform iconNode = item.transform.Find("StatusImg");
                Image iconImage = iconNode == null ? null : iconNode.GetComponent<Image>();
                if (iconImage != null) iconImage.sprite = icon;
                if (iconNode != null) iconNode.gameObject.SetActive(iconImage != null && icon != null);
                Transform count = item.transform.Find("CountTxt");
                TMP_Text countText = count == null ? null : count.GetComponent<TMP_Text>();
                if (countText != null)
                    countText.text = showStackCount && status.StackCount > 1
                        ? status.StackCount.ToString(CultureInfo.InvariantCulture) : string.Empty;
                created++;
            }
            for (int index = created; index < parent.childCount; index++)
                parent.GetChild(index).gameObject.SetActive(false);
            return created > 0;
        }

        private GameObject BossStatusItem(Transform parent, int index)
        {
            if (index < parent.childCount) return parent.GetChild(index).gameObject;
            return statusRenderer.Create("status_item", parent);
        }

        private void BindBossItem(string path, FightBattleUnitState unit,
            Func<FightBattleUnitState, string> nameResolver)
        {
            bossRenderer.SetActive(path, true);
            BossHpBarRuntime hpRuntime = BossHpBarRuntimeFor(path, unit);
            BossHpBarDisplay bar = CalculateBossHpBarDisplay(unit,
                hpRuntime.DisplayHp, hpRuntime.DisplayTotal);
            ApplyBossHpBar(path, bar);
            string pliesPath = path + "/HpBg/CurrentPliesText";
            bossRenderer.SetActive(pliesPath, bar.RemainingPlies > 1);
            if (bar.RemainingPlies > 1)
                bossRenderer.SetText(pliesPath, "X" + bar.RemainingPlies.ToString(CultureInfo.InvariantCulture));
            string name = nameResolver == null ? null : nameResolver(unit);
            GameObject nameNode = bossRenderer.Find(path + "/TextName");
            TMP_Text nameText = nameNode == null ? null : nameNode.GetComponent<TMP_Text>();
            bool hasName = !string.IsNullOrWhiteSpace(name);
            if (nameText != null && hasName) nameText.text = name;
            bossRenderer.SetActive(path + "/TextName", hasName && nameText != null);
            GameObject statusList = bossRenderer.Find(path + "/StatusList");
            if (statusList != null)
            {
                bool hasStatuses = RefreshBossStatuses(statusList.transform, unit);
                bossRenderer.SetActive(path + "/StatusList", hasStatuses);
            }
            bool hasResilience = unit.MaxResilience > 0;
            bossRenderer.SetActive(path + "/Resilience", hasResilience);
            if (hasResilience)
            {
                GameObject fillNode = bossRenderer.Find(path
                    + "/Resilience/Bg/Slider/Fill Area/Fill");
                Image resilienceFill = fillNode == null ? null : fillNode.GetComponent<Image>();
                if (resilienceFill != null)
                {
                    resilienceFill.type = Image.Type.Filled;
                    resilienceFill.fillMethod = Image.FillMethod.Horizontal;
                    resilienceFill.fillOrigin = 0;
                    resilienceFill.fillAmount = BossResilienceRatio(unit);
                }
            }
        }

        public static float BossResilienceRatio(FightBattleUnitState unit)
        {
            return unit == null || unit.MaxResilience <= 0 ? 0f
                : Mathf.Clamp01((float)((double)unit.Resilience / unit.MaxResilience));
        }

        public static BossHpBarDisplay CalculateBossHpBarDisplay(FightBattleUnitState unit)
        {
            if (unit == null) return new BossHpBarDisplay { RemainingPlies = 1 };
            long hp = Math.Max(0L, unit.Hp);
            long shield = FightBattleShieldRuntime.PersistentShieldTotal(unit);
            long total = shield > long.MaxValue - hp ? long.MaxValue : hp + shield;
            return CalculateBossHpBarDisplay(unit, hp, total);
        }

        private static BossHpBarDisplay CalculateBossHpBarDisplay(FightBattleUnitState unit,
            double displayHp, double displayTotal)
        {
            long maxHp = Math.Max(0L, unit.LastMaxHp > 0 ? unit.LastMaxHp : unit.MaxHp);
            int maxCount = Math.Max(1, unit.MaxHpPlies);
            double barValue = Math.Max(maxHp, displayTotal) / (double)maxCount;
            if (barValue <= 0.0) barValue = 1.0;

            double remainValue = Math.Ceiling(displayTotal / barValue);
            int remainCount = (int)Math.Min(maxCount, Math.Max(1.0, remainValue));
            double hpBarPer = displayHp / barValue;
            double totalBarPer = displayTotal / barValue;
            double offset = Math.Abs(totalBarPer - hpBarPer);
            double current = totalBarPer % 1.0;
            if (current == 0.0 && totalBarPer > 0.0) current = 1.0;

            int barType;
            double transition = offset - current;
            if (offset < 0.0000001 || transition <= -1.0) barType = 0;
            else if (transition < 0.0) barType = 1;
            else if (Math.Abs(transition) < 0.0000001) barType = 2;
            else if (transition < 1.0) barType = 3;
            else barType = 4;

            var result = new BossHpBarDisplay
            {
                RemainingPlies = remainCount,
                LayerIndex = Math.Abs(maxCount - remainCount)
            };
            switch (barType)
            {
                case 1:
                    result.TopFill = (float)(current - offset);
                    result.CenterFill = (float)current;
                    result.ShowCenter = true;
                    result.CenterUsesShield = true;
                    result.BottomEmpty = remainCount <= 1;
                    break;
                case 2:
                    result.TopFill = (float)current;
                    result.CenterFill = (float)current;
                    result.TopUsesShield = true;
                    result.BottomEmpty = remainCount <= 1;
                    break;
                case 3:
                    result.TopFill = (float)current;
                    result.CenterFill = (float)(current - offset + 1.0);
                    result.ShowCenter = true;
                    result.TopUsesShield = true;
                    result.BottomUsesShield = true;
                    break;
                case 4:
                    result.TopFill = (float)current;
                    result.CenterFill = (float)current;
                    result.TopUsesShield = true;
                    result.BottomUsesShield = true;
                    break;
                default:
                    result.TopFill = (float)current;
                    result.CenterFill = (float)current;
                    result.BottomEmpty = remainCount <= 1;
                    break;
            }
            result.TopFill = Mathf.Clamp01(result.TopFill);
            result.CenterFill = Mathf.Clamp01(result.CenterFill);
            return result;
        }

        private BossHpBarRuntime BossHpBarRuntimeFor(string path, FightBattleUnitState unit)
        {
            if (!bossHpBarStates.TryGetValue(path, out BossHpBarRuntime runtime))
            {
                runtime = new BossHpBarRuntime();
                bossHpBarStates.Add(path, runtime);
            }
            long hp = Math.Max(0L, unit.Hp);
            long shield = FightBattleShieldRuntime.PersistentShieldTotal(unit);
            double total = shield > long.MaxValue - hp ? long.MaxValue : hp + shield;
            if (!runtime.Initialized || runtime.UnitId != unit.Id)
            {
                runtime.DisplayHp = hp;
                runtime.DisplayTotal = total;
                runtime.Initialized = true;
            }
            runtime.UnitId = unit.Id;
            runtime.Unit = unit;
            runtime.TargetHp = hp;
            runtime.TargetTotal = total;
            return runtime;
        }

        private BossHpBarSprites BossHpSprites()
        {
            if (bossHpBarSprites != null) return bossHpBarSprites;
            BattleBossHpBarBinding binding = resolvedCatalog.FindPrefab("boss_item")?.bossHpBar;
            if (binding == null || binding.hpSpriteFiles == null || binding.hpSpriteFiles.Length == 0
                || binding.shieldColors == null || binding.shieldColors.Length == 0)
                throw new InvalidOperationException("Official boss HP bar variants are missing.");

            var hpSprites = new Sprite[binding.hpSpriteFiles.Length];
            for (int i = 0; i < hpSprites.Length; i++)
            {
                hpSprites[i] = resolvedCatalog.FindSprite(binding.hpSpriteFiles[i]);
                if (hpSprites[i] == null)
                    throw new InvalidOperationException("Official boss HP bar sprite is missing: "
                        + binding.hpSpriteFiles[i]);
            }
            var shieldColors = new Color[binding.shieldColors.Length];
            for (int i = 0; i < shieldColors.Length; i++)
            {
                float[] rgba = binding.shieldColors[i]?.rgba;
                if (rgba == null || rgba.Length < 4)
                    throw new InvalidOperationException("Official boss HP bar shield color is malformed.");
                shieldColors[i] = new Color(rgba[0], rgba[1], rgba[2], rgba[3]);
            }
            Sprite shield = resolvedCatalog.FindSprite(binding.shieldSpriteFile);
            Sprite empty = resolvedCatalog.FindSprite(binding.emptyBarSpriteFile);
            if (shield == null || empty == null)
                throw new InvalidOperationException("Official boss HP shield or empty-bar sprite is missing.");
            bossHpBarSprites = new BossHpBarSprites
            {
                Hp = hpSprites,
                Shield = shield,
                Empty = empty,
                ShieldColors = shieldColors,
                MoveSpeed = binding.moveSpeed
            };
            return bossHpBarSprites;
        }

        private void ApplyBossHpBar(string path, BossHpBarDisplay bar)
        {
            BossHpBarSprites sprites = BossHpSprites();
            Image top = ImageAt(path + "/HpBg/TopImg");
            Image center = ImageAt(path + "/HpBg/CenterImg");
            Image bottom = ImageAt(path + "/HpBg/BottomImg");
            ApplyBossLayer(top, bar.TopUsesShield, bar.LayerIndex, false,
                bar.TopFill, sprites);
            ApplyBossLayer(center, bar.CenterUsesShield, bar.LayerIndex, false,
                bar.CenterFill, sprites);
            ApplyBossLayer(bottom, bar.BottomUsesShield, bar.LayerIndex + 1,
                bar.BottomEmpty, 1f, sprites);
            bossRenderer.SetActive(path + "/HpBg/CenterImg", bar.ShowCenter);
        }

        private static void ApplyBossLayer(Image image, bool useShield, int layerIndex,
            bool empty, float fill, BossHpBarSprites sprites)
        {
            if (image == null) return;
            if (empty)
            {
                image.sprite = sprites.Empty;
                image.color = Color.white;
            }
            else if (useShield)
            {
                image.sprite = sprites.Shield;
                image.color = sprites.ShieldColors[layerIndex % sprites.ShieldColors.Length];
            }
            else
            {
                image.sprite = sprites.Hp[layerIndex % sprites.Hp.Length];
                image.color = Color.white;
            }
            if (image.type == Image.Type.Filled)
                image.fillAmount = Mathf.Clamp01(fill);
        }

        private Image ImageAt(string path)
        {
            GameObject node = bossRenderer.Find(path);
            return node == null ? null : node.GetComponent<Image>();
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
            if (timelineMoving)
            {
                timelineMoveTime = Mathf.Min(TimelineMoveDuration, timelineMoveTime + Mathf.Max(0, deltaTime));
                float percent = timelineMoveTime / TimelineMoveDuration;
                SetTimelinePositions(percent);
                if (percent >= 1) timelineMoving = false;
            }
            TickBossHpBars();
        }

        private void TickBossHpBars()
        {
            if (bossHpBarStates.Count == 0) return;
            BossHpBarSprites sprites = BossHpSprites();
            foreach (KeyValuePair<string, BossHpBarRuntime> entry in bossHpBarStates)
            {
                BossHpBarRuntime runtime = entry.Value;
                if (runtime.Unit == null) continue;
                runtime.DisplayHp += (runtime.TargetHp - runtime.DisplayHp) * sprites.MoveSpeed;
                runtime.DisplayTotal += (runtime.TargetTotal - runtime.DisplayTotal) * sprites.MoveSpeed;
                if (Math.Abs(runtime.DisplayTotal - runtime.TargetTotal) < 1.0
                    && Math.Abs(runtime.DisplayHp - runtime.TargetHp) < 1.0)
                {
                    runtime.DisplayHp = runtime.TargetHp;
                    runtime.DisplayTotal = runtime.TargetTotal;
                }
                BossHpBarDisplay bar = CalculateBossHpBarDisplay(runtime.Unit,
                    runtime.DisplayHp, runtime.DisplayTotal);
                ApplyBossHpBar(entry.Key, bar);
                string countPath = entry.Key + "/HpBg/CurrentPliesText";
                bossRenderer.SetActive(countPath, bar.RemainingPlies > 1);
                if (bar.RemainingPlies > 1)
                    bossRenderer.SetText(countPath, "X" + bar.RemainingPlies.ToString(CultureInfo.InvariantCulture));
            }
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

        private const string FactionPlayerIcon = "res://assets/official/battle-hud/sprites/ImgWe-8447ae550c.png";
        private const string FactionEnemyIcon = "res://assets/official/battle-hud/sprites/ImgEnemy-db0a1794e6.png";

        private void RefreshTimelineCard(GameObject card, FightBattleUnitState unit,
            Sprite portrait, Sprite factionSprite, bool acting)
        {
            bool monster = unit.MonsterId > 0;
            string iconPath = monster
                ? "IconGo/MonsterIconImg"
                : "IconGo/PlayerHeadETC2Go/HeadImg";
            Transform icon = card.transform.Find(iconPath);
            Image image = icon == null ? null : icon.GetComponent<Image>();
            RawImage rawImage = icon == null ? null : icon.GetComponent<RawImage>();
            if (icon == null || (image == null && rawImage == null))
                throw new InvalidOperationException("Official battle timeline portrait graphic is missing: " + iconPath);
            if (portrait != null)
            {
                if (image != null) image.sprite = portrait;
                else BindRawImage(rawImage, portrait);
            }
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
                Sprite sprite = factionSprite ?? resolvedCatalog.FindSprite(
                    unit.Side == 1 ? FactionPlayerIcon : FactionEnemyIcon);
                factionImage.sprite = sprite;
                faction.gameObject.SetActive(sprite != null);
            }
        }

        private static void BindRawImage(RawImage target, Sprite sprite)
        {
            Texture2D texture = sprite.texture;
            Rect rect;
            try { rect = sprite.textureRect; }
            catch (UnityException) { rect = sprite.rect; }
            target.texture = texture;
            target.uvRect = new Rect(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height);
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
