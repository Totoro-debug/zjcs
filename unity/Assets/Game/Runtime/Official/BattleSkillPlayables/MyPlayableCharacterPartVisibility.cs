using System;
using UnityEngine;
using Zjcs.Official.StoryPlayableManager;
using Zjcs.Official.StoryPlayables;

public class MyPlayableCharacterPartVisibility : MyPlayable, IBattleCharacterPartVisibilityPlayable
{
    public bool mStartVisible;
    public bool mEndVisible = true;
    public float mDuration;

    private IBattleCharacterPartVisibilityHost visibilityHost;

    public void BindCharacterPartVisibilityHost(IBattleCharacterPartVisibilityHost host)
        => visibilityHost = host;

    public override float GetDuration() => mDuration;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        SetVisibility(mStartVisible);
    }

    protected override void StopImpl()
    {
        SetVisibility(mEndVisible);
        base.StopImpl();
    }

    private void SetVisibility(bool visible)
    {
        if (mySource == null) return;
        if (visibilityHost == null)
            throw new InvalidOperationException("Official character-part visibility host is not bound.");
        visibilityHost.SetCharacterPartVisibility(mySource, visible);
    }
}
