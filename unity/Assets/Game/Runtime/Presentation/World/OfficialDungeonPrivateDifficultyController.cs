using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    /// <summary>Private-map host for DailyDungeonDifficultyContent's tab selection and lock tips.</summary>
    public sealed class OfficialDungeonPrivateDifficultyController : MonoBehaviour
    {
        [Serializable] private sealed class Binding
        { public string manifest, group; public int dungeonId; public TabBinding[] tabs; }
        [Serializable] private sealed class TabBinding
        { public int difficulty; public string path, lockButton; public StateBinding[] unlockStates; }
        [Serializable] private sealed class StateBinding { public int state; public string[] objects; }

        private Binding source;
        private WorldTabGroup group;
        private WorldTabItem[] tabs;
        private Button[] lockButtons;
        private UnityAction[] lockHandlers;
        private OfflineDailyDungeonSession session;
        private DailyDungeonRuntimeBattle info;
        private int difficulty;
        private Action<int> changed;
        private Action<string> notice;
        private Func<string, string> localize;

        public int Difficulty => difficulty;

        private void OnEnable()
        {
            if (source != null && tabs != null) InitContent();
        }

        internal void Initialize(OfflineDailyDungeonSession localSession, int initialDifficulty,
            Action<int> onChanged, Action<string> showNotice, Func<string, string> language)
        {
            if (source != null) throw new InvalidOperationException("Private difficulty content was already initialized.");
            string path = Path.Combine(Application.streamingAssetsPath, "Dungeons", "UI", "map12-private-difficulty.json");
            source = JsonUtility.FromJson<Binding>(File.ReadAllText(path));
            if (source?.manifest != "871_156598" || source.dungeonId != 21 || source.tabs?.Length != 5)
                throw new InvalidDataException("Original private difficulty references are missing.");
            session = localSession ?? throw new ArgumentNullException(nameof(localSession));
            info = session.FindDungeon(source.dungeonId);
            if (info?.difficulties == null || !info.difficulties.Any(value => value.difficulty == initialDifficulty))
                throw new InvalidDataException("Original private display difficulty is missing.");
            // Both selected-source display difficulties have no tab condition. Fail explicitly
            // if a future catalog adds one without supplying its original condition evaluator.
            if (info.difficulties.Any(value => value.tabConditionId > 0))
                throw new InvalidDataException("Private difficulty tab conditions require their original evaluator.");
            difficulty = initialDifficulty;
            changed = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
            notice = showNotice ?? throw new ArgumentNullException(nameof(showNotice));
            localize = language ?? throw new ArgumentNullException(nameof(language));
            group = Node(source.group).GetComponent<WorldTabGroup>();
            tabs = source.tabs.Select(value => Node(value.path).GetComponent<WorldTabItem>()).ToArray();
            lockButtons = source.tabs.Select(value => Node(value.lockButton).GetComponent<Button>()).ToArray();
            if (group == null || tabs.Any(value => value == null) || lockButtons.Any(value => value == null)
                || group.GetAllTabs().Length != tabs.Length || tabs.Any(value => !group.GetAllTabs().Contains(value)))
                throw new InvalidDataException("Original private difficulty tab components were not restored.");
            group.OnTabItemChanged.AddListener(OnDifficultyTabGroupValueChanged);
            lockHandlers = new UnityAction[source.tabs.Length];
            for (int index = 0; index < source.tabs.Length; index++)
            {
                int tabIndex = index;
                EnsureLockButtonRaycastTarget(lockButtons[index]);
                lockHandlers[index] = () => OnClickLockTipBtn(source.tabs[tabIndex]);
                lockButtons[index].onClick.AddListener(lockHandlers[index]);
            }
            InitContent();
        }

        private static void EnsureLockButtonRaycastTarget(Button button)
        {
            if (button.targetGraphic != null) return;
            Image surface = button.GetComponent<Image>() ?? button.gameObject.AddComponent<Image>();
            surface.sprite = null;
            surface.color = Color.clear;
            surface.raycastTarget = true;
            surface.maskable = true;
        }

        // DailyDungeonDifficultyContent.InitContent: preserve refresh/selection ordering.
        private void InitContent()
        {
            RefreshTabItem();
            tabs[Array.FindIndex(source.tabs, value => value.difficulty == difficulty)]
                .SetIsOn(true, false, true);
            changed(difficulty);
        }

        private void RefreshTabItem()
        {
            int visibleCount = 0;
            for (int index = 0; index < source.tabs.Length; index++)
            {
                TabBinding data = source.tabs[index];
                int position = Array.FindIndex(info.difficulties, value => value.difficulty == data.difficulty);
                tabs[index].gameObject.SetActive(position >= 0);
                if (position < 0) continue;
                visibleCount++;
                bool unlocked = position <= 0 || session.IsUnlocked(info.id, info.difficulties[position - 1].difficulty);
                var states = new WorldStateGameObjectData {
                    GoConfig = data.unlockStates.Select(value => new WorldStateGameObjectData.StateGameObjectKV {
                        State = value.state, GameObjects = value.objects.Select(item => Node(item).gameObject).ToList()
                    }).ToList()
                };
                WorldStateGameObjectConfig.FromBindings(states).SetState(unlocked ? 1 : 0);
            }
            if (group.transform.parent != null) group.transform.parent.gameObject.SetActive(visibleCount > 1);
        }

        private void OnDifficultyTabGroupValueChanged(WorldTabItem tab, bool value)
        {
            if (!value) return;
            int index = Array.IndexOf(tabs, tab);
            if (index < 0 || source.tabs[index].difficulty == difficulty) return;
            difficulty = source.tabs[index].difficulty;
            changed(difficulty);
        }

        private void OnClickLockTipBtn(TabBinding data)
        {
            int index = Array.FindIndex(info.difficulties, value => value.difficulty == data.difficulty);
            if (index <= 0) throw new InvalidDataException("Original private lock tip has no preceding difficulty.");
            int previous = info.difficulties[index - 1].difficulty;
            string name = OfficialDungeonUiBindingRuntime.PrivateDifficultyName(previous);
            notice(string.Format(localize("ui_daily_dungeon_difficulty_lock_tip_text"),
                localize("ui_daily_dungeon_item_title_" + info.id + "_" + name)));
        }

        private Transform Node(string path)
        {
            Transform node = transform;
            if (string.IsNullOrEmpty(path)) return node;
            foreach (string part in path.Split('/'))
            {
                if (!int.TryParse(part, out int index) || index < 0 || index >= node.childCount)
                    throw new InvalidDataException("Original private difficulty hierarchy reference changed: " + path);
                node = node.GetChild(index);
            }
            return node;
        }

        private void OnDestroy()
        {
            if (group != null) group.OnTabItemChanged.RemoveListener(OnDifficultyTabGroupValueChanged);
            if (lockButtons == null || lockHandlers == null) return;
            for (int index = 0; index < lockButtons.Length; index++)
                if (lockButtons[index] != null && lockHandlers[index] != null)
                    lockButtons[index].onClick.RemoveListener(lockHandlers[index]);
        }
    }

    internal static partial class OfficialDungeonUiBindingRuntime
    {
        internal static string PrivateDifficultyName(int difficulty) => LoadDocument().routeStyles.privateDisplay
            .difficulties.Single(value => value.value == difficulty).name;

        internal static void InitializePrivateDifficulty(GameObject root, OfflineDailyDungeonSession session,
            int initialDifficulty, Action<int> changed, Action<string> notice)
        {
            root.AddComponent<OfficialDungeonPrivateDifficultyController>()
                .Initialize(session, initialDifficulty, changed, notice, RouteText);
        }
    }
}
