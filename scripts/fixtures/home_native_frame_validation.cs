using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.HomeScreenUI;

// Extra checks run inside the actual furniture/home-entry fixture, before any
// simulated repair input. The desktop display configuration is never changed.
public static class HomeNativeFrameChecks
{
    public static void Check(Canvas canvas)
    {
        Require(canvas && canvas.GetComponent<CanvasWidthAdjuster>(), "actual entry uses original frame controller");
        Require(canvas.GetComponentsInChildren<MonoBehaviour>(true).All(value=>value), "actual entry has no missing frame scripts");
        var main=canvas.transform.Find("Content/Main/Anim");
        var shared=canvas.transform.Find("Content/Main/Public/Anim");
        Require(main && shared && main.childCount==1 && shared.childCount==2 && shared.Find("PlayerInfoMask"), "actual home/bottom bar occupy original Main/Public slots and retain original player stencil");
        var home=main.GetChild(0);
        foreach(var path in new[]{"Content","NewPublicTopBarParent"})
        {
            var area=home.Find(path).GetComponent<SafeArea>();
            Require(area && area.IsInit, "original HomeUI safe-area path initialized: "+path);
            var bounds=area.GetComponent<RectTransform>().rect;
            Require(bounds.width>1000 && bounds.height>1900, "original HomeUI safe-area covers portrait viewport: "+path);
            Require(!area.GetComponent<Zjcs.Official.WorldUI.WorldSafeArea>(), "legacy safe-area controller removed: "+path);
        }
        Require(shared.GetChild(1).GetComponentInParent<SafeArea>(), "original public safe area encloses bottom bar");
        var scaler=canvas.GetComponent<CanvasScaler>();
        Require(scaler && scaler.referenceResolution==new Vector2(1080,1920) && scaler.screenMatchMode==CanvasScaler.ScreenMatchMode.Expand, "original portrait scaling survives actual host assembly");
        Debug.Log("HOME_NATIVE_FRAME_HOST_PASSED: actual HomeFeatureRuntime uses packaged original MainCanvas, Main/Public ancestors, native screen/safe-area controllers and original portrait scaler; repair interaction and reopened state are checked by enclosing fixture.");
    }
    static void Require(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
}
