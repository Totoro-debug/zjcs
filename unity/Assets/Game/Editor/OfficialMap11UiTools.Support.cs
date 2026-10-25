using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap11UiTools
    {
        private static void BindSupportPrefab(GameObject root, string key, OfficialMap11UiData data,
            Func<string, Transform> require, Dictionary<string, Sprite> sprites)
        {
            T Component<T>(string path) where T : Component => require(path).GetComponent<T>();
            OfficialMap11UiComponentFields Fields(string kind) =>
                data.components.Single(value => value.panel == key && value.type == kind).fields;
            foreach (OfficialMap11UiComponent source in data.components.Where(value => value.panel == key))
            {
                if (source.type == "MirrorImage")
                {
                    var target = require(source.path).gameObject;
                    var image = target.GetComponent<Image>();
                    Sprite sprite = image.sprite;
                    var color = image.color;
                    var imageType = image.type;
                    bool preserveAspect = image.preserveAspect, raycast = image.raycastTarget, maskable = image.maskable;
                    Material material = image.material;
                    UnityEngine.Object.DestroyImmediate(image);
                    var mirror = target.AddComponent<MirrorImage>();
                    mirror.sprite = sprite;
                    mirror.color = color;
                    mirror.type = imageType;
                    mirror.preserveAspect = preserveAspect;
                    mirror.raycastTarget = raycast;
                    mirror.maskable = maskable;
                    mirror.material = material;
                    mirror.MirrorStyle = (MirrorImage.MirrorType)source.fields.mirrorType;
                    mirror.Reversed = source.fields.reversed;
                }
                if (source.type == "HorizontalLayoutGroupJSZX")
                {
                    var f = source.fields;
                    var layout = require(source.path).gameObject.AddComponent<UGUIJszx.HorizontalLayoutGroupJSZX>();
                    layout.padding = new RectOffset(f.m_Padding.m_Left, f.m_Padding.m_Right, f.m_Padding.m_Top, f.m_Padding.m_Bottom);
                    layout.childAlignment = (TextAnchor)f.m_ChildAlignment;
                    layout.spacing = f.spacing;
                    layout.spacingList = (f.m_SpacingList ?? Array.Empty<float>()).ToList();
                    layout.childForceExpandWidth = f.m_ChildForceExpandWidth != 0;
                    layout.childForceExpandHeight = f.m_ChildForceExpandHeight != 0;
                    layout.childControlWidth = f.m_ChildControlWidth != 0;
                    layout.childControlHeight = f.m_ChildControlHeight != 0;
                    layout.childScaleWidth = f.m_ChildScaleWidth != 0;
                    layout.childScaleHeight = f.m_ChildScaleHeight != 0;
                    layout.reverseArrangement = f.m_ReverseArrangement != 0;
                    layout.enabled = f.m_Enabled != 0;
                }
                if (source.type == "GridLayoutGroup")
                {
                    var f = source.fields;
                    GridLayoutGroup grid = require(source.path).gameObject.AddComponent<GridLayoutGroup>();
                    grid.padding = new RectOffset(f.m_Padding.m_Left, f.m_Padding.m_Right, f.m_Padding.m_Top, f.m_Padding.m_Bottom);
                    grid.childAlignment = (TextAnchor)f.m_ChildAlignment;
                    grid.cellSize = f.m_CellSize;
                    grid.spacing = f.m_Spacing;
                    grid.startCorner = (GridLayoutGroup.Corner)f.m_StartCorner;
                    grid.startAxis = (GridLayoutGroup.Axis)f.m_StartAxis;
                    grid.constraint = (GridLayoutGroup.Constraint)f.m_Constraint;
                    grid.constraintCount = f.m_ConstraintCount;
                    grid.enabled = f.m_Enabled != 0;
                }
                Type type = source.type == "Button" ? typeof(Button)
                    : source.type == "LayoutElement" ? typeof(LayoutElement)
                    : source.type == "HorizontalLayoutGroup" ? typeof(HorizontalLayoutGroup)
                    : source.type == "VerticalLayoutGroup" ? typeof(VerticalLayoutGroup)
                    : source.type == "ContentSizeFitter" ? typeof(ContentSizeFitter)
                    : source.type == "ScrollRect" ? typeof(ScrollRect) : null;
                if (type != null && require(source.path).GetComponent(type) is Behaviour behaviour)
                    behaviour.enabled = source.fields.m_Enabled != 0;
            }
            foreach (OfficialMap11UiComponent source in data.components.Where(value => value.panel == key && value.type == "ItemIcon"))
            {
                var f = source.fields;
                var icon = require(source.path).gameObject.AddComponent<WorldItemIcon>();
                icon.Icon = Component<Image>(f.mIconProxy.mImage);
                icon.QualityImg = Component<Image>(f.QualityImg);
                icon.QualityShadowImg = Component<Image>(f.QualityShadowImg);
                icon.myText = Component<TMP_Text>(f.myText);
                icon.myTextGo = require(f.myTextGo).gameObject;
                icon.BottomGo = string.IsNullOrEmpty(f.BottomGo) ? null : require(f.BottomGo).gameObject;
                icon.myBottomText = string.IsNullOrEmpty(f.myBottomText) ? null : Component<TMP_Text>(f.myBottomText);
                icon.BgTransform = require(f.BgTransform);
                icon.ClearObjects = new[] { f.BottomGo, f.ItemIconAttachment, f.mFakeIcon, f.mProgress }
                    .Where(value => !string.IsNullOrEmpty(value)).Select(value => require(value).gameObject).ToArray();
            }
            if (key == "ItemIconDefault") return;
            if (key == "PayMoneyChangeDialog")
            {
                var f = Fields(key);
                var exchange = root.AddComponent<WorldPayMoneyChangeDialog>();
                exchange.FlareStarItemIcon = Component<WorldItemIcon>(f.FlareStarItemIcon);
                exchange.DiamondItemIcon = Component<WorldItemIcon>(f.DiamondItemIcon);
                exchange.ExchangeFixedTxt = Component<TMP_Text>(f.ExchangeFixedTxt);
                exchange.CountSliderGo = require(f.CountSliderGo).gameObject;
                exchange.PopupWindow = root.GetComponent<WorldPopupWindow>();
                return;
            }
            var confirm = Fields("ConfirmDialog");
            var cost = Fields("CostItemConfirmDialog");
            var money = data.components.Single(value => value.panel == key && value.type == "PlayerCostMoneyContent"
                && value.path == cost.CostMoneyContent).fields;
            var dialog = root.AddComponent<WorldCostItemConfirmDialog>();
            dialog.TitleText = Component<TMP_Text>(confirm.TitleText);
            dialog.ContentText = Component<TMP_Text>(confirm.ContentText);
            dialog.ConfirmText = Component<TMP_Text>(confirm.ConfirmText);
            dialog.CancelText = Component<TMP_Text>(confirm.CancelText);
            dialog.ConfirmBtn = Component<Button>(confirm.ConfirmBtn);
            dialog.CancelBtn = Component<Button>(confirm.CancelBtn);
            dialog.CostText = Component<TMP_Text>(money.CostText);
            dialog.CostItemIcon = Component<Image>(money.CostItemIcon);
            dialog.EnoughColor = money.EnoughColor;
            dialog.LackColor = money.LackColor;
            dialog.MoneyContent = require(money.MoneyContent).gameObject;
            dialog.HintGo = require(confirm.HintGo).gameObject;
            dialog.ItemContentObj = require(cost.ItemContentObj).gameObject;
            dialog.LockMask = require(confirm.LockMaskWithTime).gameObject;
            dialog.Slot = require(cost.Slot);
            dialog.TopBarSlot = require(Fields("TopBarHandler").mSlot);
            dialog.PopupWindow = root.GetComponent<WorldPopupWindow>();
        }
    }
}
