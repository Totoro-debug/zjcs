using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionOverviewData
    {
        int CurrentProfession { get; }
        int TotalProfessionLevel { get; }
        Sprite SecondIcon(int profession);
        Sprite FullBodyImage(int profession);
        string ProfessionNameKey(int profession);
        Color ProfessionPanelFadeColor(int profession);
        Color ProfessionPropRadarColor(int profession);
        IReadOnlyDictionary<int, long> FeatureProps(int profession);
        IReadOnlyDictionary<int, long> AccumulatedLevelProps();
        void SetPropValue(ProfessionPlayerPropItem item, int propType, long value);
        void ShowLevelRule();
        event Action ProfessionChanged;
        event Action ProfessionUpgraded;
    }

    // Port of ProfessionOverviewPanel. Data/resources and local event delivery replace
    // MeManager, the original parsers/resource loader, and WindowManager.
    public sealed class ProfessionOverviewPanel : MonoBehaviour
    {
        public Image Icon1, Icon2;
        public WorldMultiLanguageText ProfessionName, TotalLevel;
        public int PropsNumber;
        public WorldTabGroup TabGroup;
        public WorldTabItem TabProfession, TabAchievement;
        public GameObject PanelProfession, PanelAchievement;
        [SerializeField] private GameObject mPropPrefab;
        [SerializeField] private Transform mPropSlot;
        [SerializeField] private ProfessionPropRadar mPropRadar;
        [SerializeField] private Button mHelpBtn;
        [SerializeField] private ImageLocator mRoleFigure;
        [SerializeField] private Image mRoleBg;
        [SerializeField] private WorldStateSwitcher<int> mContentStateSwitcher;
        private readonly List<ProfessionPlayerPropItem> mPropItems = new List<ProfessionPlayerPropItem>();
        private readonly Stack<ProfessionPlayerPropItem> mPool = new Stack<ProfessionPlayerPropItem>();
        private IProfessionOverviewData data;
        private bool subscribed;
        public event Action<int> TabChanged;
        private void Awake()
        {
            TabGroup.OnTabItemChanged.AddListener(OnSwitchTab);
            mHelpBtn.onClick.AddListener(OnViewHelp);
        }
        public void Init(IProfessionOverviewData context)
        {
            Unsubscribe();
            data = context ?? throw new ArgumentNullException(nameof(context));
            if (isActiveAndEnabled) Activate();
        }
        private void OnEnable() { if (data != null) Activate(); }
        private void Activate()
        {
            if (!subscribed)
            {
                data.ProfessionUpgraded += UpdateLevelAndProp;
                data.ProfessionChanged += UpdateProfessionView;
                subscribed = true;
            }
            TabGroup.ResetTabItem(true);
            Populate();
        }
        private void Unsubscribe()
        {
            if (!subscribed || data == null) return;
            data.ProfessionUpgraded -= UpdateLevelAndProp;
            data.ProfessionChanged -= UpdateProfessionView;
            subscribed = false;
        }
        private void OnDisable()
        {
            Unsubscribe();
            foreach (ProfessionPlayerPropItem item in mPropItems)
            {
                item.gameObject.SetActive(false);
                item.transform.SetParent(transform, false);
                mPool.Push(item);
            }
            mPropItems.Clear();
        }
        private void OnSwitchTab(WorldTabItem tab, bool value)
        {
            if (!value) return;
            int state = TabGroup.GetTabItemIndex(tab);
            mContentStateSwitcher.SetState(state);
            TabChanged?.Invoke(state);
        }
        private void Populate() { UpdateProfessionView(); UpdateLevelAndProp(); }
        private void UpdateProfessionView()
        {
            int profession = data.CurrentProfession;
            Icon1.sprite = data.SecondIcon(profession);
            Icon2.sprite = Icon1.sprite;
            ProfessionName.SetKey(data.ProfessionNameKey(profession));
            if (mRoleFigure != null) mRoleFigure.SetSprite(data.FullBodyImage(profession));
            mRoleBg.color = data.ProfessionPanelFadeColor(profession);
        }
        private void UpdateLevelAndProp()
        {
            TotalLevel.Format(data.TotalProfessionLevel);
            mPropRadar.ShowProp(data.FeatureProps(data.CurrentProfession),
                data.ProfessionPropRadarColor(data.CurrentProfession), data.SetPropValue);
            int index = 0;
            foreach (var entry in data.AccumulatedLevelProps())
            {
                ProfessionPlayerPropItem item = GetPropItem(index++);
                item.gameObject.SetActive(true);
                data.SetPropValue(item, entry.Key, entry.Value);
            }
        }
        private ProfessionPlayerPropItem GetPropItem(int index)
        {
            while (mPropItems.Count <= index)
            {
                ProfessionPlayerPropItem item;
                if (mPool.Count > 0)
                {
                    item = mPool.Pop();
                    item.transform.SetParent(mPropSlot, false);
                }
                else item = Instantiate(mPropPrefab, mPropSlot, false).GetComponent<ProfessionPlayerPropItem>();
                item.SetBgVisible(mPropItems.Count % 4 < 2);
                mPropItems.Add(item);
            }
            return mPropItems[index];
        }
        public void OnViewHelp() => data?.ShowLevelRule();
    }
}
