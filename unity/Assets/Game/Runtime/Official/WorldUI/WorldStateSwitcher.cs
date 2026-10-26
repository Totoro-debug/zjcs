using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    [Serializable]
    public struct WorldStateConfigValue
    {
        [SerializeField] private int mValue;
        public static implicit operator int(WorldStateConfigValue value) => value.mValue;
        public static implicit operator WorldStateConfigValue(int value) => new WorldStateConfigValue { mValue = value };
        public override int GetHashCode() => mValue.GetHashCode();
        public override string ToString() => mValue.ToString();
    }

    [Serializable] public struct WorldMonoKeyValuePair<TKey, TValue> { public TKey Key; public TValue Value; }

    [Serializable]
    public class WorldStateKVMatch<TValue> : IEnumerable<WorldMonoKeyValuePair<WorldStateConfigValue, TValue>>
    {
        [SerializeField] private List<WorldMonoKeyValuePair<WorldStateConfigValue, TValue>> mSerializeBuffer;
        public bool TryGetValue(WorldStateConfigValue key, out TValue value)
        {
            foreach (var item in mSerializeBuffer)
                if ((int)item.Key == (int)key) { value = item.Value; return true; }
            value = default;
            return false;
        }
        public IEnumerator<WorldMonoKeyValuePair<WorldStateConfigValue, TValue>> GetEnumerator() => mSerializeBuffer.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => mSerializeBuffer.GetEnumerator();
    }

    public interface IWorldStateConfig
    {
        void SetState(WorldStateConfigValue state);
        void Clear();
    }

    [Serializable]
    public class WorldStateSwitcher<T> where T : struct, IConvertible
    {
        [SerializeReference] private List<IWorldStateConfig> mStateConfigs;
        [SerializeField] private GameObject mOwner;
        public T State { get; protected set; }
        public void SetState(T state)
        {
            if (state is int integer) Switch(integer);
            else if (state is bool flag) Switch(flag ? 1 : 0);
            else
            {
                if (!typeof(T).IsEnum) { Debug.LogError("StateSwitcher does not support " + typeof(T)); return; }
                Switch(Convert.ToInt32(state));
            }
            State = state;
        }
        private void Switch(int state)
        {
            try { foreach (IWorldStateConfig config in mStateConfigs) config.SetState(state); }
            catch (Exception exception) { Debug.LogException(exception, mOwner); }
        }
        public void Clear() { foreach (IWorldStateConfig config in mStateConfigs) config.Clear(); }
    }

    [Serializable]
    public class WorldStateGameObjectData
    {
        [Serializable] public class StateGameObjectKV { public WorldStateConfigValue State; public List<GameObject> GameObjects; }
        public List<StateGameObjectKV> GoConfig;
    }

    [Serializable]
    public class WorldStateGameObjectConfig : IWorldStateConfig
    {
        [SerializeField] private WorldStateGameObjectData mStateGameObjectData;
        public void SetState(WorldStateConfigValue state)
        {
            List<GameObject> selected = null;
            foreach (var item in mStateGameObjectData.GoConfig)
                if ((int)item.State == (int)state) { selected = item.GameObjects; break; }
            foreach (var item in mStateGameObjectData.GoConfig)
            {
                if ((int)item.State == (int)state) continue;
                foreach (GameObject gameObject in item.GameObjects)
                {
                    if (gameObject == null) throw new Exception("StateSwitcher contains a missing GameObject for state " + state);
                    if (selected == null || !selected.Contains(gameObject)) gameObject.SetActive(false);
                }
            }
            if (selected == null) return;
            foreach (GameObject gameObject in selected)
            {
                if (gameObject == null) throw new Exception("StateSwitcher contains a missing selected GameObject.");
                gameObject.SetActive(true);
            }
        }
        public void Clear() { }
    }

    [Serializable]
    public class WorldStateUIColorData
    {
        [Serializable] public class GraphicConfig { public List<Graphic> Graphics; public WorldStateKVMatch<Color> Color; }
        public List<GraphicConfig> GraphicConfigs;
    }

    [Serializable]
    public class WorldStateUIColorConfig : IWorldStateConfig
    {
        [SerializeField] private WorldStateUIColorData mStateColorData;
        public void SetState(WorldStateConfigValue state)
        {
            foreach (var item in mStateColorData.GraphicConfigs)
            {
                if (!item.Color.TryGetValue(state, out Color color)) continue;
                foreach (Graphic graphic in item.Graphics)
                {
                    if (graphic == null) throw new Exception("StateSwitcher contains a missing Graphic.");
                    graphic.color = color;
                }
            }
        }
        public void Clear() { }
    }
}
