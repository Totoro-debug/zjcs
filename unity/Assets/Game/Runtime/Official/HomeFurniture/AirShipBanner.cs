// Original airship banner logic with activity and resource owners supplied by the host.
using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeSceneInput;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.HomeFurniture
{
    public sealed class AirshipActivityEntry
    {
        public int EntryType, ActivityId, Order;
    }

    public interface IAirShipBannerServices
    {
        bool HasAirship { get; }
        IDisposable SubscribeRepair(Action callback);
        IDisposable SubscribeActivities(Action callback);
        IEnumerable<AirshipActivityEntry> ActivityEntries();
        void ClickActivity(AirshipActivityEntry entry);
        string SubtitleKey(AirshipActivityEntry entry);
        GameObject LoadActivityBanner(AirshipActivityEntry entry, Transform slot);
        void ReleaseBanners();
    }

    public class AirShipBanner : ClickableHomeObj
    {
        public enum AirShipBannerType { Msg, Activity }
        [Serializable] public struct AirShipBannerData { public AirShipBannerType AirShipBannerType; public int Key; }
        [SerializeField] private HomeScrollTextBord mHomeScrollTextBord;
        [SerializeField] private Transform mBannerSlot;
        [SerializeField] private WorldStateSwitcher<AirShipBannerType> mBannerSwitcher;
        [SerializeField] private int mSwitchBannerInterval;
        [SerializeField] private AirShipBannerData mDefaultAirshipBannerData;
        private readonly List<AirshipActivityEntry> mActivityEntryKeyDatas = new List<AirshipActivityEntry>();
        private readonly List<AirShipBannerData> mBanners = new List<AirShipBannerData>();
        private IDisposable repair, activities;
        private float mTimer, mCloseTime, mAdditionalDeltaTime;
        private int mCurIndex;
        private GameObject mBanner;
        public IAirShipBannerServices BannerServices { get; set; }

        private void OnEnable()
        {
            if (BannerServices == null) throw new InvalidOperationException("Bind airship banner services before activation.");
            if (!BannerServices.HasAirship) repair = BannerServices.SubscribeRepair(Refresh);
            activities = BannerServices.SubscribeActivities(Refresh);
            Refresh();
            if (mCloseTime > 0f) mAdditionalDeltaTime = Time.time - mCloseTime;
        }

        private void OnDisable()
        {
            repair?.Dispose(); repair = null;
            mCloseTime = Time.time;
            activities?.Dispose(); activities = null;
            BannerServices?.ReleaseBanners();
        }

        protected override void OnValidClick()
        {
            if (mCurIndex >= mBanners.Count) return;
            AirShipBannerData banner = mBanners[mCurIndex];
            if (banner.AirShipBannerType == AirShipBannerType.Activity)
                BannerServices.ClickActivity(mActivityEntryKeyDatas[banner.Key]);
        }

        private void Refresh()
        {
            bool repaired = BannerServices.HasAirship;
            ClearBanners();
            if (repaired)
            {
                mBanners.Add(mDefaultAirshipBannerData);
                mActivityEntryKeyDatas.Clear();
                mActivityEntryKeyDatas.AddRange(BannerServices.ActivityEntries());
                mActivityEntryKeyDatas.Sort((a, b) => a.Order - b.Order);
                for (int i = 0; i < mActivityEntryKeyDatas.Count; i++)
                    mBanners.Add(new AirShipBannerData { AirShipBannerType = AirShipBannerType.Activity, Key = i });
            }
            if (mBanners.Count == 0) return;
            if (mCurIndex >= mBanners.Count) mCurIndex = mBanners.Count - 1;
            FillBanner(mBanners[mCurIndex]);
        }

        private void Update()
        {
            if (mBanners.Count == 0) return;
            mTimer += mAdditionalDeltaTime + Time.deltaTime;
            if (mTimer > mSwitchBannerInterval)
            {
                int count = (int)(mTimer / mSwitchBannerInterval);
                mCurIndex = (mCurIndex + count) % mBanners.Count;
                FillBanner(mBanners[mCurIndex]);
                mTimer -= count * mSwitchBannerInterval;
            }
            mAdditionalDeltaTime = 0f;
        }

        public void ClearBanners() => mBanners.Clear();

        private void FillBanner(AirShipBannerData banner)
        {
            mBannerSwitcher.SetState(banner.AirShipBannerType);
            if (mBanner) Destroy(mBanner);
            switch (banner.AirShipBannerType)
            {
                case AirShipBannerType.Msg:
                    mHomeScrollTextBord.SetContent("ui_airship_billboard_repaired");
                    break;
                case AirShipBannerType.Activity:
                    AirshipActivityEntry entry = mActivityEntryKeyDatas[banner.Key];
                    mBanner = BannerServices.LoadActivityBanner(entry, mBannerSlot);
                    mHomeScrollTextBord.SetContent(BannerServices.SubtitleKey(entry));
                    break;
                default: throw new ArgumentOutOfRangeException();
            }
        }
    }
}
