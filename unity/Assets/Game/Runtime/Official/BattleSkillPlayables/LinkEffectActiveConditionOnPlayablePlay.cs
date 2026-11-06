using System;
using Zjcs.Official.StoryPlayables;

public class LinkEffectActiveConditionOnPlayablePlay : LinkEffectActiveConditionBase
{
    public MyPlayable OwnerPlayable;
    private bool canActive;

    private void OnEnable()
    {
        canActive = false;
        if (OwnerPlayable != null) OwnerPlayable.OnPlay += OnOwnerPlayablePlay;
    }

    private void OnDisable()
    {
        if (OwnerPlayable != null) OwnerPlayable.OnPlay -= OnOwnerPlayablePlay;
    }

    public override bool CanActive() => canActive;

    private void OnOwnerPlayablePlay(MyPlayable playable) => canActive = true;
}
