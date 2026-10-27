using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionStoryNodeRegister
    {
        void RegisterNode(string key, GameObject node);
        void UnregisterNode(string key);
    }
    public sealed class StoryNodeRegister : MonoBehaviour
    {
        public List<string> StoryNodeKeys;
        public List<GameObject> StoryNodes;
        private readonly List<string> mMissingRefKeys = new List<string>();
        private IProfessionStoryNodeRegister nodes;
        private bool registered;
        public void Init(IProfessionStoryNodeRegister value)
        {
            if (registered) Unregister();
            nodes = value ?? throw new ArgumentNullException(nameof(value));
            if (isActiveAndEnabled) Register();
        }
        private void OnEnable() { if (nodes != null) Register(); }
        private void OnDisable() { if (registered) Unregister(); }
        private void Register()
        {
            mMissingRefKeys.Clear();
            int count = StoryNodes.Count;
            for (int i = 0; i < StoryNodeKeys.Count && i < count; i++)
            {
                if (StoryNodes[i] == null) mMissingRefKeys.Add(StoryNodeKeys[i]);
                else nodes.RegisterNode(StoryNodeKeys[i], StoryNodes[i]);
            }
            registered = true;
            if (mMissingRefKeys.Count > 0)
                Debug.LogError("[StoryNodeRegister] " + name + " 注册key(" + string.Join(",", mMissingRefKeys) + ") 对应引用为空", this);
        }
        private void Unregister()
        {
            for (int i = 0; i < StoryNodeKeys.Count; i++) nodes.UnregisterNode(StoryNodeKeys[i]);
            registered = false;
        }
    }
}
