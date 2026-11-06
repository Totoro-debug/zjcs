using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.BattleResultUI
{
    public sealed class OfficialBattleAwardEntry
    {
        public int ItemId;
        public long Count;
        public object OriginalData;
    }

    public interface IOfficialBattleAwardListHost
    {
        bool HasLocalPlayer { get; }
        IReadOnlyList<OfficialBattleAwardEntry> PrepareAwards(IReadOnlyList<OfficialBattleAwardEntry> awards);
        bool ShouldDisplay(OfficialBattleAwardEntry award);
        void BindAwardItem(Component item, OfficialBattleAwardEntry award);
        void SetAwardItemVisible(Component item, bool visible);
    }

    public interface IOfficialBattleAwardListItem
    {
        Component Component { get; }
    }

    public sealed class OfficialBattleAwardList : MonoBehaviour, LoopScrollDataSource
    {
        [SerializeField] private float mPlayIntervalSeconds;
        [SerializeField] private float mShowItemIconTime;
        [SerializeField] private LoopScrollRect mAwardLoopScroll;

        private readonly List<OfficialBattleAwardEntry> awards = new List<OfficialBattleAwardEntry>();
        private readonly Queue<Component> itemsWaitingShow = new Queue<Component>();
        private IOfficialBattleAwardListHost host;
        private Coroutine showRoutine;

        public void Bind(IOfficialBattleAwardListHost value) =>
            host = value ?? throw new ArgumentNullException(nameof(value));

        public void Init(IReadOnlyList<OfficialBattleAwardEntry> source)
        {
            if (host == null) throw new InvalidOperationException("Original battle award list host is unbound.");
            if (mAwardLoopScroll == null) throw new InvalidOperationException("Original award LoopScrollRect is unbound.");

            StopShowRoutine();
            awards.Clear();
            itemsWaitingShow.Clear();
            IReadOnlyList<OfficialBattleAwardEntry> prepared = host.PrepareAwards(source ?? Array.Empty<OfficialBattleAwardEntry>());
            if (prepared != null)
            {
                foreach (OfficialBattleAwardEntry award in prepared)
                    if (award != null && host.ShouldDisplay(award)) awards.Add(award);
            }

            mAwardLoopScroll.dataSource = this;
            mAwardLoopScroll.totalCount = awards.Count;
            showRoutine = StartCoroutine(ShowIn());
        }

        public void Clear()
        {
            StopShowRoutine();
            if (mAwardLoopScroll != null)
            {
                if (ReferenceEquals(mAwardLoopScroll.dataSource, this)) mAwardLoopScroll.dataSource = null;
                mAwardLoopScroll.ClearCells();
            }
            awards.Clear();
            itemsWaitingShow.Clear();
        }

        public void ProvideData(Transform itemTransform, int index)
        {
            if (host == null) throw new InvalidOperationException("Original battle award list host is unbound.");
            if (index < 0 || index >= awards.Count) throw new ArgumentOutOfRangeException(nameof(index));
            IOfficialBattleAwardListItem item = itemTransform.GetComponent<MonoBehaviour>() as IOfficialBattleAwardListItem;
            if (item == null || item.Component == null)
                throw new InvalidOperationException("Original award row component is missing.");
            host.BindAwardItem(item.Component, awards[index]);
            if (mPlayIntervalSeconds > 0f)
            {
                host.SetAwardItemVisible(item.Component, false);
                itemsWaitingShow.Enqueue(item.Component);
            }
        }

        private IEnumerator ShowIn()
        {
            if (mShowItemIconTime > 0f) yield return new WaitForSeconds(mShowItemIconTime);
            if (host == null || !host.HasLocalPlayer)
            {
                showRoutine = null;
                yield break;
            }

            mAwardLoopScroll.RefillCells();
            yield return null;
            while (itemsWaitingShow.Count > 0)
            {
                if (mPlayIntervalSeconds > 0f) yield return new WaitForSeconds(mPlayIntervalSeconds);
                Component item = itemsWaitingShow.Dequeue();
                if (item != null) host.SetAwardItemVisible(item, true);
            }
            showRoutine = null;
        }

        private void OnDisable() => Clear();

        private void StopShowRoutine()
        {
            if (showRoutine == null) return;
            StopCoroutine(showRoutine);
            showRoutine = null;
        }
    }
}
