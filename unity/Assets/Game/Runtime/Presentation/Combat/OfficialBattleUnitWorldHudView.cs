using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleUnitWorldHudView : IDisposable
    {
        private const float HudPixelsPerWorldUnit = 100f;

        private sealed class BarGraphic
        {
            public BattleUnitBarPart Data;
            public Image Image;
            public RectTransform FillMask;
            public RectTransform FillImage;
        }

        private readonly OfficialBattleHudCatalog catalog;
        private readonly OfficialBattleHudRenderer statusRenderer;
        private readonly GameObject root;
        private readonly Canvas canvas;
        private readonly Transform statusRoot;
        private readonly BattleUnitBarBinding binding;
        private readonly BattleUnitBarFaction faction;
        private readonly Dictionary<string, BarGraphic> bars =
            new Dictionary<string, BarGraphic>(StringComparer.Ordinal);
        private readonly List<GameObject> statusItems = new List<GameObject>();
        private string statusSignature;
        private string effectRole;
        private float effectStart;
        private float effectEnd;
        private float effectElapsed;
        private float currentHpRatio;
        private float visibleHpRatio;
        private bool hasHpRatio;
        private bool disposed;

        public Transform Root => root.transform;

        public static OfficialBattleUnitWorldHudView Create(OfficialBattleHudCatalog catalog,
            BattleUnitBarBinding binding, BattleUnitBarFaction faction, FightBattleUnitState unit,
            Transform body, Camera camera, int sortingOrder)
        {
            var view = new OfficialBattleUnitWorldHudView(catalog, binding, unit, body, faction);
            view.canvas.worldCamera = camera;
            view.canvas.sortingOrder = sortingOrder;
            return view;
        }

        public static OfficialBattleUnitWorldHudView Create(OfficialBattleHudCatalog catalog,
            FightBattleUnitState unit, Transform body)
        {
            if (catalog == null || unit == null || body == null
                || !TryGetUnitBar(catalog, unit, out BattleUnitBarBinding binding)) return null;
            return new OfficialBattleUnitWorldHudView(catalog, binding, unit, body);
        }

        public static int ResolveUnitBarEntityId(FightBattleUnitState unit)
        {
            if (unit == null) return 0;
            if (unit.MonsterId > 0) return unit.MonsterId;
            if (unit.MonsterTemplateId > 0) return unit.MonsterTemplateId;
            return unit.CharacterType == 1 ? 3100 : 0;
        }

        private static bool TryGetUnitBar(OfficialBattleHudCatalog catalog,
            FightBattleUnitState unit, out BattleUnitBarBinding binding)
        {
            if (unit.MonsterId > 0 && catalog.TryGetUnitBar(unit.MonsterId, out binding)) return true;
            if (unit.MonsterTemplateId > 0
                && catalog.TryGetUnitBar(unit.MonsterTemplateId, out binding)) return true;
            if (unit.MonsterId <= 0 && unit.MonsterTemplateId <= 0 && unit.CharacterType == 1
                && catalog.TryGetUnitBar(3100, out binding)) return true;
            binding = null;
            return false;
        }

        private OfficialBattleUnitWorldHudView(OfficialBattleHudCatalog catalog,
            BattleUnitBarBinding binding, FightBattleUnitState unit, Transform body, BattleUnitBarFaction factionOverride = null)
        {
            this.catalog = catalog;
            this.binding = binding;
            faction = factionOverride ?? catalog.UnitBarFaction(unit.Id == 1 || unit.Side == 1);
            statusRenderer = new OfficialBattleHudRenderer(catalog);

            root = new GameObject("OfficialBattleUnitHud_" + unit.Id,
                typeof(RectTransform), typeof(Canvas));
            root.transform.SetParent(body, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            canvas.worldCamera = Camera.main;
            RectTransform canvasRect = (RectTransform)root.transform;
            canvasRect.sizeDelta = new Vector2(100f, 100f);

            foreach (BattleUnitBarPart part in OrderedParts(binding.parts))
                CreateBar(part);
            statusRoot = CreateStatusRoot(binding.statusLayout);
            Refresh(unit);
        }

        public void Refresh(FightBattleUnitState unit)
        {
            if (disposed || unit == null) return;
            CalculateBarRatios(unit, out float nextHp, out float shieldRatio);
            if (hasHpRatio && !Mathf.Approximately(nextHp, currentHpRatio))
            {
                effectRole = nextHp < currentHpRatio ? "HpSubSlider" : "HpSlider";
                effectStart = visibleHpRatio;
                effectEnd = nextHp;
                effectElapsed = 0f;
                if (effectRole == "HpSubSlider")
                {
                    SetBar("HpSlider", nextHp, true);
                    SetBar("HpSubSlider", effectStart, true);
                    SetBar("HpAddSlider", 0f, false);
                    visibleHpRatio = nextHp;
                }
                else
                {
                    SetBar("HpSlider", effectStart, true);
                    SetBar("HpAddSlider", nextHp, true);
                    SetBar("HpSubSlider", 0f, false);
                }
            }
            else if (!hasHpRatio)
            {
                hasHpRatio = true;
                visibleHpRatio = nextHp;
            }
            currentHpRatio = nextHp;
            if (string.IsNullOrEmpty(effectRole) || effectRole != "HpSlider")
                SetBar("HpSlider", nextHp, true);
            SetBar("Resilience", unit.MaxResilience <= 0 ? 0f
                : Mathf.Clamp01((float)unit.Resilience / unit.MaxResilience), unit.MaxResilience > 0);
            SetBar("ShieldSlider", shieldRatio, shieldRatio > 0f);
            RefreshStatuses(unit);
        }

        public static void CalculateBarRatios(FightBattleUnitState unit,
            out float hpRatio, out float shieldRatio)
        {
            hpRatio = 0f;
            shieldRatio = 0f;
            if (unit == null) return;

            long hp = Math.Max(0L, unit.Hp);
            long shield = FightBattleShieldRuntime.PersistentShieldTotal(unit);
            long hpWithShield = shield > long.MaxValue - hp ? long.MaxValue : hp + shield;
            long maxHp = unit.LastMaxHp > 0 ? unit.LastMaxHp : unit.MaxHp;
            long displayMaxHp = Math.Max(Math.Max(0L, maxHp), hpWithShield);
            if (displayMaxHp <= 0) return;

            hpRatio = Mathf.Clamp01((float)((double)hp / displayMaxHp));
            if (shield > 0)
                shieldRatio = Mathf.Clamp01((float)((double)hpWithShield / displayMaxHp));
        }

        public void Tick(float deltaTime)
        {
            if (disposed || string.IsNullOrEmpty(effectRole) || deltaTime <= 0f) return;
            effectElapsed += deltaTime;
            float duration = Mathf.Max(0.01f, binding.duration);
            float progress = Mathf.Clamp01(effectElapsed / duration);
            float animated = Mathf.Lerp(effectStart, effectEnd, progress);
            if (effectRole == "HpSlider")
            {
                SetBar("HpSlider", animated, true);
                visibleHpRatio = animated;
            }
            else
                SetBar(effectRole, animated, true);
            if (progress < 1f) return;
            if (effectRole == "HpSlider")
            {
                visibleHpRatio = effectEnd;
                SetBar("HpSlider", effectEnd, true);
                SetBar("HpAddSlider", 0f, false);
            }
            else
                SetBar(effectRole, 0f, false);
            effectRole = null;
        }

        public void SetVisible(bool visible)
        {
            if (root != null) root.SetActive(visible);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (root != null) UnityEngine.Object.Destroy(root);
            bars.Clear();
            statusItems.Clear();
        }

        private void CreateBar(BattleUnitBarPart part)
        {
            Sprite sprite = catalog.FindSprite(part.spriteFile);
            if (sprite == null) throw new InvalidOperationException(
                "Official unit bar sprite is missing: " + part.spriteFile);
            var go = new GameObject(part.name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition3D = new Vector3(part.position[0], part.position[1], part.position[2]);
            rect.localScale = new Vector3(part.scale[0], part.scale[1], part.scale[2]);
            rect.sizeDelta = new Vector2(part.size[0], part.size[1]);
            RectTransform fillMask = null;
            RectTransform fillImage = null;
            Image image;
            if (part.fillMode >= 0 && part.fillMode <= 3)
            {
                var maskObject = new GameObject("FillMask", typeof(RectTransform), typeof(RectMask2D));
                maskObject.transform.SetParent(rect, false);
                fillMask = (RectTransform)maskObject.transform;
                var imageObject = new GameObject("FillImage", typeof(RectTransform),
                    typeof(CanvasRenderer), typeof(Image));
                imageObject.transform.SetParent(fillMask, false);
                fillImage = (RectTransform)imageObject.transform;
                image = imageObject.GetComponent<Image>();
                ConfigureSlicedFill(rect, fillMask, fillImage, part, 1f);
            }
            else
            {
                image = go.AddComponent<Image>();
            }
            image.sprite = sprite;
            image.raycastTarget = false;
            image.color = PartColor(part);
            image.type = part.fillMode < 0 || part.fillMode <= 3
                ? Image.Type.Sliced : Image.Type.Filled;
            if (part.fillMode > 3)
            {
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = Mathf.Clamp(part.fillOrigin, 0, 3);
                image.fillAmount = 1f;
            }
            bars[part.role] = new BarGraphic
            {
                Data = part,
                Image = image,
                FillMask = fillMask,
                FillImage = fillImage
            };
            bool visible = part.role == "background" || part.role == "HpSlider";
            if (part.role == "Resilience") visible = false;
            image.enabled = visible;
        }

        private Transform CreateStatusRoot(BattleUnitStatusLayout layout)
        {
            var go = new GameObject("StatusLayout", typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = Vector2.zero;
            if (layout == null)
            {
                go.SetActive(false);
                return rect;
            }
            rect.anchoredPosition3D = new Vector3(layout.position[0], layout.position[1], layout.position[2]);
            rect.localScale = new Vector3(layout.scale[0], layout.scale[1], layout.scale[2]);
            rect.sizeDelta = new Vector2(layout.columns * layout.cellSize[0],
                layout.rows * layout.cellSize[1]);
            return rect;
        }

        private void RefreshStatuses(FightBattleUnitState unit)
        {
            var signature = new StringBuilder();
            if (unit.Statuses != null)
                foreach (FightBattleStatusState status in unit.Statuses)
                    if (status != null
                        && catalog.TryGetStatusDisplay(status.ClassId, out _, out _,
                            out bool showSpriteAboveUnit, out bool showStackCount,
                            out bool showRoundReplaceStack, out bool showCustomCount,
                            out bool hasRoundStartComponent)
                        && showSpriteAboveUnit)
                        signature.Append(status.InstanceId).Append(':').Append(status.ClassId).Append(':')
                            .Append(status.StackCount).Append(':')
                            .Append(StatusCountText(status, unit, showStackCount,
                                showRoundReplaceStack, showCustomCount, hasRoundStartComponent)).Append(';');
            string nextSignature = signature.ToString();
            if (statusSignature == nextSignature) return;
            statusSignature = nextSignature;
            if (unit.Statuses == null || binding.statusLayout == null)
            {
                SetUnusedStatusItemsActive(0);
                return;
            }

            int shown = 0;
            foreach (FightBattleStatusState status in unit.Statuses)
            {
                if (status == null
                    || !catalog.TryGetStatusDisplay(status.ClassId, out Sprite icon, out Sprite background,
                        out bool showSpriteAboveUnit, out bool showStackCount,
                        out bool showRoundReplaceStack, out bool showCustomCount,
                        out bool hasRoundStartComponent)
                    || !showSpriteAboveUnit) continue;
                if (shown >= binding.statusLayout.columns * binding.statusLayout.rows) break;
                GameObject item;
                if (shown < statusItems.Count)
                {
                    item = statusItems[shown];
                    item.SetActive(true);
                }
                else
                {
                    item = statusRenderer.Create("status_item", statusRoot);
                    statusItems.Add(item);
                }
                RectTransform itemRect = item.GetComponent<RectTransform>();
                float cellWidth = binding.statusLayout.cellSize[0];
                float cellHeight = binding.statusLayout.cellSize[1];
                itemRect.anchorMin = itemRect.anchorMax = Vector2.zero;
                itemRect.pivot = Vector2.zero;
                itemRect.localScale = new Vector3(1f / HudPixelsPerWorldUnit,
                    1f / HudPixelsPerWorldUnit, 1f);
                itemRect.anchoredPosition = new Vector2((shown % binding.statusLayout.columns) * cellWidth,
                    (shown / binding.statusLayout.columns) * cellHeight);
                Image backgroundImage = item.GetComponent<Image>();
                if (backgroundImage != null)
                {
                    backgroundImage.sprite = background;
                    backgroundImage.enabled = background != null;
                    backgroundImage.raycastTarget = false;
                }
                Transform iconNode = item.transform.Find("StatusImg");
                Image iconImage = iconNode == null ? null : iconNode.GetComponent<Image>();
                if (iconImage != null)
                {
                    iconImage.sprite = icon;
                    iconImage.enabled = icon != null;
                    iconImage.raycastTarget = false;
                }
                if (iconNode != null) iconNode.gameObject.SetActive(icon != null && iconImage != null);
                Transform countNode = item.transform.Find("CountTxt");
                TMP_Text countText = countNode == null ? null : countNode.GetComponent<TMP_Text>();
                if (countText != null)
                    countText.text = StatusCountText(status, unit, showStackCount,
                        showRoundReplaceStack, showCustomCount, hasRoundStartComponent);
                shown++;
            }
            SetUnusedStatusItemsActive(shown);
        }

        private static string StatusCountText(FightBattleStatusState status, FightBattleUnitState unit,
            bool showStackCount, bool showRoundReplaceStack, bool showCustomCount,
            bool hasRoundStartComponent)
        {
            if (status == null) return string.Empty;
            if (showRoundReplaceStack)
            {
                if (hasRoundStartComponent) return string.Empty;
                int remaining;
                if (status.RoundTarget == FightStatusLifecycle.TargetNone)
                    remaining = status.DurationRound;
                else if (unit != null && unit.SkillState != null
                    && (status.RoundTarget == FightStatusLifecycle.TargetApplicator
                        && status.OwnerId == unit.Id
                        || status.RoundTarget == FightStatusLifecycle.TargetCreator
                        && status.CreatorId == unit.Id))
                    remaining = FightStatusLifecycle.LeftRound(status, unit.SkillState.Round);
                else
                    return string.Empty;
                return remaining > 0 ? remaining.ToString(CultureInfo.InvariantCulture) : string.Empty;
            }
            if (showCustomCount || !showStackCount || status.StackCount <= 0)
                return string.Empty;
            return status.StackCount.ToString(CultureInfo.InvariantCulture);
        }

        private void SetUnusedStatusItemsActive(int activeCount)
        {
            for (int i = activeCount; i < statusItems.Count; i++)
                if (statusItems[i] != null && statusItems[i].activeSelf)
                    statusItems[i].SetActive(false);
        }

        private void SetBar(string role, float amount, bool visible)
        {
            if (!bars.TryGetValue(role, out BarGraphic graphic)) return;
            graphic.Image.enabled = visible;
            amount = Mathf.Clamp01(amount);
            if (graphic.FillMask != null)
                ConfigureSlicedFill((RectTransform)graphic.FillMask.parent, graphic.FillMask,
                    graphic.FillImage, graphic.Data, amount);
            else if (graphic.Data.fillMode > 3)
                graphic.Image.fillAmount = amount;
        }

        private static void ConfigureSlicedFill(RectTransform fullRect, RectTransform maskRect,
            RectTransform fillRect, BattleUnitBarPart part, float amount)
        {
            Vector2 fullSize = new Vector2(part.size[0], part.size[1]);
            maskRect.anchorMin = maskRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillRect.localScale = Vector3.one;
            fillRect.localRotation = Quaternion.identity;
            switch (part.fillMode)
            {
                case 0:
                    maskRect.pivot = new Vector2(0f, 0.5f);
                    maskRect.anchoredPosition = new Vector2(-fullSize.x * 0.5f, 0f);
                    maskRect.sizeDelta = new Vector2(fullSize.x * amount, fullSize.y);
                    fillRect.anchorMin = fillRect.anchorMax = new Vector2(0f, 0.5f);
                    fillRect.pivot = new Vector2(0f, 0.5f);
                    fillRect.anchoredPosition = Vector2.zero;
                    fillRect.sizeDelta = fullSize;
                    break;
                case 1:
                    maskRect.pivot = new Vector2(1f, 0.5f);
                    maskRect.anchoredPosition = new Vector2(fullSize.x * 0.5f, 0f);
                    maskRect.sizeDelta = new Vector2(fullSize.x * amount, fullSize.y);
                    fillRect.anchorMin = fillRect.anchorMax = new Vector2(1f, 0.5f);
                    fillRect.pivot = new Vector2(1f, 0.5f);
                    fillRect.anchoredPosition = Vector2.zero;
                    fillRect.sizeDelta = fullSize;
                    break;
                case 2:
                    maskRect.pivot = new Vector2(0.5f, 0f);
                    maskRect.anchoredPosition = new Vector2(0f, -fullSize.y * 0.5f);
                    maskRect.sizeDelta = new Vector2(fullSize.x, fullSize.y * amount);
                    fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 0f);
                    fillRect.pivot = new Vector2(0.5f, 0f);
                    fillRect.anchoredPosition = Vector2.zero;
                    fillRect.sizeDelta = fullSize;
                    break;
                case 3:
                    maskRect.pivot = new Vector2(0.5f, 1f);
                    maskRect.anchoredPosition = new Vector2(0f, fullSize.y * 0.5f);
                    maskRect.sizeDelta = new Vector2(fullSize.x, fullSize.y * amount);
                    fillRect.anchorMin = fillRect.anchorMax = new Vector2(0.5f, 1f);
                    fillRect.pivot = new Vector2(0.5f, 1f);
                    fillRect.anchoredPosition = Vector2.zero;
                    fillRect.sizeDelta = fullSize;
                    break;
            }
            fullRect.ForceUpdateRectTransforms();
        }

        private Color PartColor(BattleUnitBarPart part)
        {
            float[] values = part.role switch
            {
                "HpSlider" => faction.hpColor,
                "HpAddSlider" => faction.hpAddColor,
                "HpSubSlider" => faction.hpSubColor,
                _ => part.color
            };
            return new Color(values[0], values[1], values[2], values[3]);
        }

        private static List<BattleUnitBarPart> OrderedParts(BattleUnitBarPart[] parts)
        {
            var ordered = new List<BattleUnitBarPart>(parts);
            ordered.Sort((left, right) => left.order.CompareTo(right.order));
            return ordered;
        }
    }
}
