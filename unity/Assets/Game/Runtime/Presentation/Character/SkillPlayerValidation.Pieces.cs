using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Character
{
    public sealed partial class SkillPlayerValidation
    {
        // The diagnostic supplies only original materials; all skill mutations use native pointer input.
        private static void SupplyPassivePieceFixture(OfflineMainlineSave save, OfficialCharacterAssetCatalog assets,
            OfflineSkillDefinition definition, int rank)
        {
            void Give(int id, long count)
            {
                var stack = save.Player.Inventory.SingleOrDefault(value => value.ItemId == id);
                if (stack == null) save.Player.Inventory.Add(new OfflineItemStack { ItemId = id, Count = count });
                else stack.Count += count;
            }
            var active = assets.LoadSkillRules().Skills.Where(value => value.ItemType == 100
                && value.AvailableProfessions.Contains(save.Player.Wardrobe.Profession))
                .OrderBy(value => value.ClassId).First();
            Require(assets.Load().SkillMergePiece(active.ClassId) > 1,
                "The original active fragment fixture must remain below its merge requirement.");
            Give(active.PieceItemId, 1);
            Give(definition.PieceItemId, assets.Load().SkillMergePiece(definition.ClassId));
            foreach (var cost in definition.RankCosts.Single(value => value.CurrentRank == rank).Items)
                Give(cost.ItemId, cost.Count);
        }

        private static Button PieceRowWithCount(long count) => FindObjectsOfType<Button>().Single(value =>
            value.isActiveAndEnabled && value.interactable
            && value.name.StartsWith("CharacterPanelSkillDetailPanelSkillPieceItem", StringComparison.Ordinal)
            && value.GetComponentsInChildren<TMP_Text>().Any(text => text.name == "LeftTxt"
                && text.text == OfficialPropFormat.Compact(count)));

        private static Transform PiecePopup() => FindObjectsOfType<Transform>().Single(value => value.name == "skill_piece_detail");

        private static void ClosePiecePopupThroughPointer()
        {
            var close = PiecePopup().GetComponentsInChildren<Button>().SingleOrDefault(value => value.name == "CloseBtn");
            if (close == null)
                close = FindObjectsOfType<Button>().Where(value => value.isActiveAndEnabled
                    && (value.name == "Mask" || value.name == "BlurMask" || value.name == "invisableMask")
                    && value.GetComponent<Canvas>() != null)
                    .OrderByDescending(value => value.GetComponent<Canvas>().sortingOrder).First();
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4]; ((RectTransform)close.transform).GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            var hits = new List<RaycastResult>();
            for (int y = 0; y < 5; y++) for (int x = 0; x < 5; x++)
            {
                var point = new Vector2(Mathf.Lerp(min.x, max.x, (x + .5f) / 5),
                    Mathf.Lerp(min.y, max.y, (y + .5f) / 5));
                if (point.x < 0 || point.x >= Screen.width || point.y < 0 || point.y >= Screen.height) continue;
                hits.Clear(); EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, hits);
                if (hits.Count == 0 || (hits[0].gameObject != close.gameObject
                    && !hits[0].gameObject.transform.IsChildOf(close.transform))) continue;
                Click(close, point); return;
            }
            throw new InvalidOperationException("The original fragment close target has no unobstructed pointer position.");
        }

        private IEnumerator AcquirePassiveFromPieces(OfflineCharacterProgressionService progression, int itemId, int rank)
        {
            int activePiece = progression.Player.Inventory.Single(value => value.Count == 1
                && progression.Catalog.ItemType(value.ItemId) == 111).ItemId;
            int activeSkill = progression.SkillRules.Skills.Single(value => value.PieceItemId == activePiece).ClassId;
            Require(progression.Catalog.ItemType(progression.SkillDefinition(itemId).PieceItemId) == 112,
                "The selected official secret fragment type must survive historical catalog adaptation.");
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId) == 0,
                "The secret fragment flow must start without the selected secret.");
            Click(ButtonContaining("SkillPieceTab"));
            yield return new WaitForSecondsRealtime(.4f);
            Require(FindObjectsOfType<Button>().Count(value => value.isActiveAndEnabled && value.interactable
                && value.name.StartsWith("CharacterPanelSkillDetailPanelSkillPieceItem", StringComparison.Ordinal)) == 2,
                "The shared fragment list must contain both original active and passive fragment types.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-list");
            Click(PieceRowWithCount(1));
            yield return new WaitForSecondsRealtime(.6f);
            var insufficient = PiecePopup();
            var slider = insufficient.GetComponentsInChildren<Slider>().Single();
            Require(slider.value == 1 && slider.maxValue == progression.SkillMergePiece(activeSkill)
                && !insufficient.GetComponentsInChildren<Button>().Any(value => value.name == "CommonBtn_12_Merge"),
                "Insufficient fragments must preserve the actual partial progress and hide the merge operation.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-insufficient");
            ClosePiecePopupThroughPointer();
            yield return new WaitForSecondsRealtime(.3f);
            var definition = progression.SkillDefinition(itemId);
            long owned = progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count);
            Click(PieceRowWithCount(owned));
            yield return new WaitForSecondsRealtime(.6f);
            var detail = PiecePopup();
            yield return new WaitForEndOfFrame(); Capture("passive-piece-detail");
            System.IO.File.WriteAllLines(System.IO.Path.Combine(output, "passive-piece-detail-texts.txt"),
                detail.GetComponentsInChildren<TMP_Text>().Select(value => value.name + "=" + value.text));
            Require(detail.GetComponentsInChildren<TMP_Text>().Any(value => value.name == "TypeText" && value.text == "秘法碎片")
                && !detail.GetComponentsInChildren<TMP_Text>().Any(value => value.text.Contains("{0}")),
                "The passive fragment detail lost its original type or still contains template placeholders.");
            Click(ButtonNamed("CommonBtn_12_Merge"));
            yield return new WaitForSecondsRealtime(.4f);
            var confirm = FindObjectsOfType<Transform>().Single(value => value.name == "skill_merge_confirm");
            Require(confirm.GetComponentsInChildren<Image>().Count(value => value.name == "icon") == 2,
                "The passive merge confirmation must initialize exactly its original two icon nodes.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-confirm");
            Click(ButtonNamed("MergeBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId) == rank
                && progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count)
                    == owned - progression.SkillMergePiece(itemId),
                "The original merge did not grant the secret at its configured initial rank and exact material cost.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-merged");
            Debug.Log("[Passive piece flow] Original active/passive list, insufficient progress and native secret acquisition passed.");
        }

        private IEnumerator RankPassiveFromPieceMaterial(OfflineCharacterProgressionService progression, int itemId, int rank)
        {
            Click(ButtonNamed("SkillPromotePanelRankUpTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.4f);
            var material = FindPointerTarget(value => value.name == "ItemIconSkillPiece"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "RankUpContent"),
                "original passive fragment cultivation material");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-material");
            Click(material);
            yield return new WaitForSecondsRealtime(.6f);
            var popup = PiecePopup();
            Require(popup.GetComponentsInChildren<TMP_Text>().Any(value => value.name == "TypeText" && value.text == "秘法碎片")
                && popup.GetComponentsInChildren<Button>().Any(value => value.name == "CommonBtn_12_RankUp"),
                "A passive cultivation fragment must open its original rank-up detail, including its owned skill state.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-rank-detail");
            ClosePiecePopupThroughPointer();
            yield return new WaitForSecondsRealtime(.3f);
            Click(FindPointerTarget(value => value.name == "UseItemBtn"
                && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "RankUpContent"),
                "passive fragment rank-up operation"));
            yield return new WaitForSecondsRealtime(.7f);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId) == rank + 1,
                "The secret fragment cultivation did not advance exactly one original rank.");
            yield return new WaitForEndOfFrame(); Capture("passive-piece-ranked");
            Debug.Log("[Passive piece flow] Native passive material icon, fragment detail and original rank transaction passed.");
        }
    }
}
