using System;
using UnityEngine;
using Zjcs.Official.StoryBubbles;

namespace Zjcs.Official.StoryBubbleTimeline
{
    public interface IBubblePopupServices : IBubbleServices
    {
        IBubblePopupOperation Popup(Action<BubbleDialog> created);
    }
    public interface IBubblePopupOperation
    {
        event Action<GameObject> OnComplete;
    }
}
