using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    /// <summary>Port of UnityEngine.UI.Ext.TabItem; serialized field names and callback order are retained.</summary>
    [AddComponentMenu("UI/Official Tab Item", 31)]
    [RequireComponent(typeof(RectTransform))]
    public class WorldTabItem : Selectable, IPointerClickHandler, ISubmitHandler, ICanvasElement
    {
        [Serializable] public class TabEvent : UnityEvent<bool> { }
        public enum ToggleTransition { None, Custom }
        public Graphic graphic;
        [SerializeField] private WorldTabGroup m_Group;
        [SerializeField] private bool m_IgnoreClickIfIsOn;
        [SerializeField] private TabEvent onValueChanged = new TabEvent();
        [SerializeField] private bool m_IsOn;
        public ToggleTransition tabTransition;
        [SerializeField] private WorldTabTransition m_TabTransitionScript;

        public TabEvent OnValueChanged => onValueChanged;
        public bool isOn { get => m_IsOn; set => SetIsOn(value); }
        public event Func<WorldTabItem, bool> TabClickOnHook;

        public virtual void Rebuild(CanvasUpdate executing) { }
        public virtual void LayoutComplete() { }
        public virtual void GraphicUpdateComplete() { }

        protected override void OnEnable()
        {
            base.OnEnable();
            PlayEffect(true);
        }

        protected override void OnDestroy()
        {
            if (m_Group != null) m_Group.UnregisterTab(this);
            base.OnDestroy();
        }

        protected override void OnDidApplyAnimationProperties()
        {
            if (graphic != null)
            {
                bool value = !Mathf.Approximately(graphic.canvasRenderer.GetColor().a, 0f);
                if (m_IsOn != value)
                {
                    m_IsOn = value;
                    SetIsOn(!value);
                }
            }
            base.OnDidApplyAnimationProperties();
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!m_IgnoreClickIfIsOn || !m_IsOn) base.OnPointerDown(eventData);
        }

        public void SetIsOnWithoutNotify(bool value, bool instant = false) => SetIsOn(value, false, instant);

        public void SetIsOn(bool value, bool sendCallback = true, bool instant = false)
        {
            if (m_IsOn == value) return;
            m_IsOn = value;
            if (m_Group != null && m_Group.isActiveAndEnabled && (IsActive() || m_IsOn)
                && (m_IsOn || (!m_Group.AnyTabOn() && !m_Group.allowSwitchOff)))
            {
                m_IsOn = true;
                m_Group.NotifyTabOn(this, sendCallback, instant);
            }
            PlayEffect(instant);
            if (sendCallback)
            {
                UISystemProfilerApi.AddMarker("TabItem.value", this);
                onValueChanged.Invoke(m_IsOn);
                if (m_Group != null) m_Group.OnTabItemChanged.Invoke(this, m_IsOn);
            }
        }

        public void SetGroup(WorldTabGroup newGroup)
        {
            if (m_Group != null) m_Group.UnregisterTab(this);
            m_Group = newGroup;
            if (m_Group != null) m_Group.RegisterTab(this);
        }

        private void InternalTab()
        {
            if (!IsActive() || !IsInteractable() || (m_IgnoreClickIfIsOn && m_IsOn)) return;
            try
            {
                if (!m_IsOn && TabClickOnHook != null && TabClickOnHook(this)) return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                return;
            }
            isOn = !isOn;
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left) InternalTab();
        }

        public virtual void OnSubmit(BaseEventData eventData) => InternalTab();

        private void PlayEffect(bool instant)
        {
            if (tabTransition == ToggleTransition.Custom && m_TabTransitionScript != null)
                m_TabTransitionScript.PlayEffect(m_IsOn, instant);
            if (graphic != null) graphic.CrossFadeAlpha(m_IsOn ? 1f : 0f, instant ? 0f : 0.1f, true);
        }
    }
}
