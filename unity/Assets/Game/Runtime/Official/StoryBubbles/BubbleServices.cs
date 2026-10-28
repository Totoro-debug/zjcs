using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.StoryBubbles
{
    [Serializable]
    public sealed class BubbleDialogInfo
    {
        public int Id, Order, EntityClassId;
        public string Text;
        public bool IsSelf, IsAngry;
        public Vector2 OffsetToRoot;
    }

    public interface IBubbleServices
    {
        BubbleDialogInfo Get(int dialogId, int order);
        List<BubbleDialogInfo> GetDialogInfosById(int dialogId);
        string Localize(string key);
        IEnumerable<BubbleEntityBinding> ServerEntities { get; }
        IEnumerable<BubbleEntityBinding> ClientEntities { get; }
        GameObject SelfObject { get; }
        bool TryGetHomeUI(out GameObject home);
        bool TryGetWorldPanel(out WorldPanel panel);
        bool HasBottomBar { get; }
        bool HasPlayerInfo { get; }
        void HideBottomBar(object owner);
        void TryShowBottomBar(object owner);
        void HidePlayerInfo(object owner);
        void TryShowPlayerInfo(object owner);
        void Close(GameObject window);
    }

    public static class BubbleServices
    {
        // Explicit binding of the original singleton services for the active map lifetime.
        public static IBubbleServices Current { get; set; }
    }
}
