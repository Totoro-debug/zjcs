using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using Zjcs.Official.HomeUI;
using Zjcs.Official.StoryMasks;
using Zjcs.Offline.HomeStoryDialog;
using Zjcs.Presentation.World;
using Object = UnityEngine.Object;

public sealed partial class HomeStoryMaskRunner
{
    private NativeStoryMaskHost maskHost;

    private IEnumerator CheckFurnitureMask()
    {
        var assets = HomeStoryFurnitureColliderAssets.LoadDefault();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath("__COLLIDER_GUID__"));
        Require(prefab && assets.DiningTableUnrepairedTouch == prefab
            && assets.DiningTableWorldPosition == new Vector3(__COLLIDER_X__f, __COLLIDER_Y__f, 0f),
            "Original unrepaired dining-table collision catalog");
        var target = Object.Instantiate(prefab, home.Scene.Root.transform, false);
        target.transform.position = assets.DiningTableWorldPosition;
        var collider = target.GetComponent<BoxCollider>();
        Require(collider && collider.size == new Vector3(__COLLIDER_SX__f, __COLLIDER_SY__f, __COLLIDER_SZ__f),
            "Original collision component");
        home.Scene.Camera.tag = "MainCamera";
        var position = home.Scene.Position; position.x = target.transform.position.x;
        home.Scene.Camera.transform.position = home.Scene.Clamp(position);
        maskHost = NativeStoryMaskHost.Create(home.Hud.CanvasTransform);
        maskHost.Manager.OpenRectMask(target, new MaskSetting());
        yield return new WaitForSeconds(.2f);
        var view = maskHost.GetComponent<StoryMask>();
        var graphic = maskHost.GetComponentInChildren<StoryMaskGraphic>(true);
        Require(maskHost.Manager.EnableRectMask && view.IsPlayingSlotMask && graphic && graphic.isActiveAndEnabled,
            "Original story mask follows the original collision subtree");
        Vector2 center = home.Scene.Camera.WorldToScreenPoint(collider.bounds.center);
        var centerHits = Hits(center);
        Require(centerHits.Count == 0, "The furniture center passes the original mask and all other UI");
        Vector2 outside = new Vector2(8, Screen.height * .5f);
        var outsideHits = Hits(outside);
        Require(outsideHits.Count > 0 && outsideHits[0].gameObject == graphic.gameObject,
            "Original mask blocks outside the furniture: " + string.Join(",", outsideHits.Select(hit => hit.gameObject.name)));
        yield return CaptureActual("home-story-furniture-mask.png");
        int clicked = -1;
        Action<int> observeClick = type => clicked = type;
        home.FurnitureClickedForTutorial += observeClick;
        try
        {
            var input = typeof(Zjcs.Presentation.Home.HomeFeatureRuntime).GetField("input", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(home);
            foreach (string method in new[] { "PointerDown", "PointerUp" })
                input.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(input, new object[] { center, false });
            Require(clicked == 2, "Simulated pointer reaches the original dining-table scene target through the mask");
        }
        finally { home.FurnitureClickedForTutorial -= observeClick; }
        maskHost.Manager.CloseRectMask();
        if (home.Hud.AnyModalOpen) { home.Hud.CloseDialog(); yield return new WaitForSeconds(.6f); }
        Object.Destroy(target);
        var bed = home.Scene.Position;
        Require(home.Scene.TryGetFurnitureClickBounds(0, out Rect bedBounds), "Bed collision bounds");
        bed.x = bedBounds.center.x; home.Scene.Camera.transform.position = home.Scene.Clamp(bed);
    }

    private IEnumerator CheckRepairButtonMask(FurnitureRepairDialog repair)
    {
        maskHost.Manager.OpenRectMask(repair.RepairBtn, new MaskSetting());
        yield return new WaitForSeconds(.2f);
        var graphic = maskHost.GetComponentInChildren<StoryMaskGraphic>(true);
        Require(maskHost.Manager.EnableRectMask && graphic && graphic.isActiveAndEnabled,
            "Original repair-button mask is active");
        Vector2 outside = new Vector2(8, Screen.height * .5f);
        var hits = Hits(outside);
        Require(hits.Count > 0 && hits[0].gameObject == graphic.gameObject,
            "Original mask blocks outside the repair button: " + string.Join(",", hits.Select(hit => hit.gameObject.name)));
        yield return CaptureActual("home-story-repair-button-mask.png");
        Click(repair.RepairBtn);
        maskHost.Manager.Reset();
        Object.Destroy(maskHost.gameObject);
        maskHost = null;
    }

    private static List<RaycastResult> Hits(Vector2 position)
    {
        Canvas.ForceUpdateCanvases();
        var hits = new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
        return hits;
    }

    private IEnumerator CaptureActual(string name)
    {
        var previous = home.Scene.Camera.targetTexture;
        // The fixture converts production overlay canvases to camera space for
        // an isolated render target. Keep their relative window/mask orders
        // while placing them above scene renderers for the actual Game View.
        var canvases = home.Hud.CanvasTransform.GetComponentsInChildren<Canvas>(true)
            .Concat(Object.FindObjectsOfType<Canvas>(true).Where(value => value.worldCamera == home.Scene.Camera))
            .Distinct().ToArray();
        var layers = canvases.Select(value => value.sortingLayerID).ToArray();
        int uiLayer = SortingLayer.layers.Last().id;
        foreach (var canvas in canvases) canvas.sortingLayerID = uiLayer;
        home.Scene.Camera.targetTexture = null;
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        var image = ScreenCapture.CaptureScreenshotAsTexture();
        Require(image && image.width == 608 && image.height == 1080, "Actual portrait game view");
        File.WriteAllBytes(Path.Combine(Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR"), name), image.EncodeToPNG());
        Object.Destroy(image);
        home.Scene.Camera.targetTexture = previous;
        for (int i = 0; i < canvases.Length; i++) if (canvases[i]) canvases[i].sortingLayerID = layers[i];
    }
}
