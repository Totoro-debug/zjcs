using System;
using UnityEngine;

namespace Zjcs.Official.BattleStageStatusUI
{
    public sealed class FightStageStatusPresentation
    {
        public struct Item
        {
            public Sprite Icon, Background;
            public string Name, Description;
            public bool DescriptionUpdatable;
        }

        private readonly Func<int, Item> resolve;
        private readonly Action<GameObject> bind;
        public event Action DescriptionChanged;

        public FightStageStatusPresentation(Func<int, Item> resolve, Action<GameObject> bind = null)
        {
            this.resolve = resolve ?? throw new ArgumentNullException(nameof(resolve));
            this.bind = bind;
        }

        internal void Bind(GameObject row) => bind?.Invoke(row);
        public Item Resolve(int statusId) => resolve(statusId);
        public void NotifyDescriptionChanged() => DescriptionChanged?.Invoke();
    }
}
