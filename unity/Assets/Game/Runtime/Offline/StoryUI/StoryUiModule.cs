using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.WorldUI;

namespace Zjcs.Offline.Story
{
    // Offline transport for WaitWindowOpened/ClosedNode and ClickBtnNode. The
    // chapter supplies native windows/registered objects, never synthetic clicks.
    public sealed class StoryUiModule
    {
        private readonly Func<string, GameObject> findWindow;
        private readonly IStoryNodeRegisterMgr nodes;

        public StoryUiModule(Func<string, GameObject> findWindow, IStoryNodeRegisterMgr nodes)
        {
            this.findWindow = findWindow ?? throw new ArgumentNullException(nameof(findWindow));
            this.nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        }

        public IEnumerator WaitWindow(string path, bool opened, float timeoutSeconds = -1)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Original story window path is required.");
            double deadline = Time.timeAsDouble + timeoutSeconds;
            while (IsOpen(findWindow(path)) != opened)
            {
                if (timeoutSeconds >= 0 && Time.timeAsDouble >= deadline)
                    throw new TimeoutException("Original story window wait timed out: " + path);
                yield return null;
            }
        }

        public IEnumerator WaitActive(string key, bool active)
        {
            GameObject target = null;
            while (!target)
            {
                target = nodes.GetNode(key);
                if (!target) yield return null;
            }
            // The original node retains the resolved GameObject, including when
            // changing activeSelf also unregisters its own owner.
            while (target.activeSelf != active) yield return null;
        }

        public IEnumerator WaitTab(string key, bool isOn)
        {
            GameObject registered = nodes.GetNode(key);
            WorldTabItem tab = registered ? registered.GetComponent<WorldTabItem>() : null;
            // WaitTabItemStateNode skips absent tabs rather than waiting for registration.
            if (!tab || tab.isOn == isOn) yield break;
            bool met = false;
            UnityAction<bool> changed = value => met = value == isOn;
            tab.OnValueChanged.AddListener(changed);
            try { while (!met) yield return null; }
            finally { if (tab) tab.OnValueChanged.RemoveListener(changed); }
        }

        public IEnumerator WaitBottomBar(Func<bool?> state, bool waitUntilPresent, bool shown)
        {
            bool? current = state();
            if (!current.HasValue && !waitUntilPresent) yield break;
            while (!current.HasValue)
            {
                yield return null;
                current = state();
            }
            while (current.Value != shown)
            {
                yield return null;
                current = state();
                if (!current.HasValue) throw new InvalidOperationException("Original public bottom bar disappeared during story wait.");
            }
        }

        public IEnumerator WaitClick(string key, bool waitRegister, bool endOnUnregister, int clickTimes,
            Func<GameObject, Button> selectButton = null)
        {
            Button button = null;
            GameObject registered = null;
            int clicks = 0;
            bool unregistered = false;
            UnityAction clicked = () => clicks++;
            Action<string> removed = removedKey => { if (removedKey == key) unregistered = true; };
            nodes.OnUnregisterNode += removed;
            try
            {
                while (true)
                {
                    registered = nodes.GetNode(key);
                    if (registered) break;
                    if (!waitRegister) yield break;
                    if (endOnUnregister && unregistered) yield break;
                    yield return null;
                }
                // WaitRegisterBaseNode.CheckSkipNode checks activeSelf, not the
                // ancestor's state. Keep that distinction from Button activation.
                if (!waitRegister && !registered.activeSelf) yield break;
                button = selectButton == null ? registered.GetComponent<Button>() : selectButton(registered);
                if (!button) throw new InvalidOperationException("Original registered story Button is missing: " + key);
                button.onClick.AddListener(clicked);
                while (!button.isActiveAndEnabled)
                {
                    if (endOnUnregister && unregistered) yield break;
                    yield return null;
                }
                while (clicks < clickTimes)
                {
                    if (!waitRegister && (!registered || !registered.activeSelf)) yield break;
                    if (endOnUnregister && unregistered) yield break;
                    yield return null;
                }
            }
            finally
            {
                if (button) button.onClick.RemoveListener(clicked);
                nodes.OnUnregisterNode -= removed;
            }
        }

        private static bool IsOpen(GameObject window) => window && window.activeInHierarchy;
    }
}
