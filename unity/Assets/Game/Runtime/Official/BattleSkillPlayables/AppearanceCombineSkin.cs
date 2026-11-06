using System;
using System.Collections.Generic;
using Spine;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

[ExecuteAlways]
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1)]
public sealed class AppearanceCombineSkin : MonoBehaviour
{
    public bool WorkOnPlaying = true;
    public bool IsUI;
    public bool UseRandomSkin;
    public List<string> CombineSkins = new List<string>();
    public Action OnApplySkin;
    public Action OnPostApplySkin;

    private SkeletonGraphic skeletonGraphic;
    private ISkeletonComponent skeletonComponent;
    private IAppearanceComponent appearanceComponent;
    private bool initialized;

    public bool Init => initialized;

    private void OnEnable()
    {
        if (initialized) return;
        skeletonGraphic = GetComponent<SkeletonGraphic>();
        skeletonComponent = GetComponent<ISkeletonComponent>();
        appearanceComponent = GetComponent<IAppearanceComponent>();
        IsUI = skeletonGraphic != null;
        if (WorkOnPlaying)
        {
            if (UseRandomSkin) UseAllSkin(true, false);
            if (CombineSkins.Count > 0) ApplyCombineSkinInRuntime();
        }
        initialized = true;
    }

    public void SetSkin(string skinName)
    {
        CombineSkins.Clear();
        CombineSkins.Add(skinName);
        ApplyCombineSkinInRuntime();
    }

    public void SetSkins(IEnumerable<string> skinNames)
    {
        CombineSkins.Clear();
        CombineSkins.AddRange(skinNames);
        ApplyCombineSkinInRuntime();
    }

    public void AddSkin(string skinName)
    {
        if (CombineSkins.Contains(skinName)) return;
        CombineSkins.Add(skinName);
        ApplyCombineSkinInRuntime();
    }

    public void ReplaceSkin(string skinName)
    {
        string key = skinName.Split('_')[0];
        for (int i = 0; i < CombineSkins.Count; i++)
        {
            if (!CombineSkins[i].StartsWith(key, StringComparison.Ordinal)) continue;
            CombineSkins[i] = skinName;
            ApplyCombineSkinInRuntime();
            break;
        }
    }

    public void RemoveSkin(string skinName)
    {
        if (CombineSkins.Remove(skinName)) ApplyCombineSkinInRuntime();
    }

    private void ApplyCombineSkinInRuntime()
    {
        if (appearanceComponent != null)
        {
            appearanceComponent.Release();
            appearanceComponent.SetSkins(CombineSkins);
            return;
        }
        if (skeletonComponent?.SkeletonDataAsset == null || skeletonComponent.Skeleton == null) return;
        SkeletonData data = skeletonComponent.SkeletonDataAsset.GetSkeletonData(true);
        var combined = new Skin("combineSkin");
        foreach (string name in CombineSkins)
        {
            Skin skin = data.FindSkin(name);
            if (skin != null) combined.AddSkin(skin);
        }
        skeletonComponent.Skeleton.SetSkin(combined);
        skeletonComponent.Skeleton.SetToSetupPose();
        if (skeletonGraphic != null)
        {
            skeletonGraphic.TrimRenderers();
            skeletonGraphic.MatchRectTransformWithBounds();
        }
        OnApplySkin?.Invoke();
    }

    public void UseAllSkin(bool isRandom, bool includeDefault)
    {
        if (skeletonComponent?.SkeletonDataAsset == null) return;
        UseAllSkin(isRandom, includeDefault, skeletonComponent.SkeletonDataAsset, ref CombineSkins);
    }

    public static void UseAllSkin(bool isRandom, bool includeDefault,
        SkeletonDataAsset skeletonDataAsset, ref List<string> result)
    {
        var groups = new Dictionary<string, List<string>>();
        foreach (Skin skin in skeletonDataAsset.GetSkeletonData(true).Skins)
        {
            string name = skin.Name;
            if ((!includeDefault && name == "default") || name == "combineSkin") continue;
            string key = name.Split('_')[0];
            if (!groups.TryGetValue(key, out List<string> names)) groups.Add(key, names = new List<string>());
            names.Add(name);
        }
        result.Clear();
        foreach (List<string> names in groups.Values)
            result.Add(names[isRandom ? UnityEngine.Random.Range(0, names.Count) : 0]);
    }

    public void Clear()
    {
        CombineSkins.Clear();
        if (skeletonComponent?.SkeletonDataAsset == null || skeletonComponent.Skeleton == null) return;
        skeletonComponent.Skeleton.SetSkin(skeletonComponent.SkeletonDataAsset.GetSkeletonData(true).DefaultSkin);
        skeletonComponent.Skeleton.SetToSetupPose();
        OnApplySkin?.Invoke();
    }
}
