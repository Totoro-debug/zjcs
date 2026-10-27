using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    public sealed class WorldStageOperationWindowClickUi : MonoBehaviour
    {
        [Serializable] public sealed class ClickData
        {
            public int ClickType;
            public Button Button;
            [NonSerialized] public UnityAction Listener;
        }
        public ClickData[] mDatas;
        private Action<int> handler;
        private void Awake()
        {
            foreach (ClickData item in mDatas)
            {
                item.Listener = () => OnClick(item.ClickType);
                item.Button.onClick.AddListener(item.Listener);
            }
        }
        private void OnDestroy()
        {
            foreach (ClickData item in mDatas)
                if (item.Button != null) item.Button.onClick.RemoveListener(item.Listener);
        }
        public void InitWindowClick(Action<int> click) => handler = click;
        public void OnClick(int type) => handler?.Invoke(type);
    }
}
