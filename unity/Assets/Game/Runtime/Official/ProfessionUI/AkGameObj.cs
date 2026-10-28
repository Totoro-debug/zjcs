using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionSoundObjectHost
    {
        bool IsInitialized { get; }
        float DefaultScalingFactor { get; }
        int RegisterGameObject(GameObject emitter, string objectName);
        void SetObjectPosition(GameObject emitter, Vector3 position, Vector3 forward, Vector3 up);
        void InitializeEmptyEnvironment(GameObject emitter, Collider sourceCollider);
        void UpdateEmptyEnvironment(GameObject emitter, Vector3 position);
        void SetDefaultListeners(GameObject emitter);
        void SetScalingFactor(GameObject emitter, float factor);
        void UnregisterGameObject(GameObject emitter);
    }

    // Reconstructed from the original APK native methods. This is the exact
    // collider-free/default-listener branch used by the three profession objects.
    // Other Wwise environment/listener configurations require their own binding.
    public class AkGameObj : MonoBehaviour
    {
        [Serializable] public sealed class ListenerList
        {
            public List<Component> initialListenerList = new List<Component>();
            public bool useDefaultListeners = true;
        }
        [Serializable] public sealed class PositionOffsetData
        {
            public bool KeepMe;
            public Vector3 positionOffset;
        }
        private sealed class PositionData { public Vector3 forward, position, up; }
        public ListenerList m_listeners = new ListenerList();
        public bool isEnvironmentAware = true, isStaticObject;
        public PositionOffsetData m_positionOffsetData;
        public Component m_posOffsetData;
        public int listenerMask = 1;
        [NonSerialized] private float scalingFactor = -1;
        [NonSerialized] private bool isRegistered;
        [NonSerialized] private PositionData m_posData;
        [NonSerialized] private Collider m_Collider;
        [NonSerialized] private bool m_envData;
        [NonSerialized] private IProfessionSoundObjectHost mHost;

        private IProfessionSoundObjectHost Host => mHost ?? (mHost = OfficialWwiseEvent.Host as IProfessionSoundObjectHost) ??
            throw new InvalidOperationException("Original profession sound-object host is unbound.");

        // Native Register sets the flag before calling the sound engine, even
        // when the first registration fails. A later call returns AK_Success.
        public int Register()
        {
            if (isRegistered) return 1;
            isRegistered = true;
            return Host.RegisterGameObject(gameObject, gameObject.name);
        }
        protected virtual void Awake()
        {
            ValidateSourceScope();
            if (!isStaticObject) m_posData = new PositionData();
            m_Collider = GetComponent<Collider>();
            if (Register() != 1) return;
            SetPosition();
            if (isEnvironmentAware)
            {
                m_envData = true;
                Host.InitializeEmptyEnvironment(gameObject, m_Collider);
                Host.UpdateEmptyEnvironment(gameObject, transform.position);
            }
            Host.SetDefaultListeners(gameObject);
            if (scalingFactor < 0) scalingFactor = Host.DefaultScalingFactor;
            Host.SetScalingFactor(gameObject, scalingFactor);
        }
        private void ValidateSourceScope()
        {
            if (m_listeners == null || !m_listeners.useDefaultListeners || m_listeners.initialListenerList == null ||
                m_listeners.initialListenerList.Count != 0 || m_posOffsetData != null || GetComponent<Collider>() != null)
                throw new InvalidOperationException("This Wwise object needs its original non-default listener, legacy offset or collider/environment implementation.");
        }
        protected virtual void OnEnable() { enabled = !isStaticObject; }
        protected virtual void Update()
        {
            if (m_envData) Host.UpdateEmptyEnvironment(gameObject, transform.position);
            if (!isStaticObject) SetPosition();
        }
        public virtual Vector3 GetPosition() => m_positionOffsetData == null ? transform.position :
            transform.position + transform.rotation * m_positionOffsetData.positionOffset;
        public virtual Vector3 GetForward() => transform.forward;
        public virtual Vector3 GetUpward() => transform.up;
        public void SetPosition()
        {
            Vector3 position = GetPosition(), forward = GetForward(), up = GetUpward();
            if (m_posData != null)
            {
                // The source compares each squared delta against this exact
                // float from APK address 0x3982ee8, before updating all fields.
                const float epsilonSquared = 9.999999439624929e-11f;
                if ((m_posData.position - position).sqrMagnitude < epsilonSquared &&
                    (m_posData.forward - forward).sqrMagnitude < epsilonSquared &&
                    (m_posData.up - up).sqrMagnitude < epsilonSquared) return;
                m_posData.position = position; m_posData.forward = forward; m_posData.up = up;
            }
            Host.SetObjectPosition(gameObject, position, forward, up);
        }
        protected virtual void OnDestroy()
        {
            // The original visits destroy-trigger handlers before unregistering.
            // The existing AkEvent port rejects such triggers at Awake; all eight
            // source profession events use only OnEnable/Start.
            foreach (var handler in GetComponents<AkEvent>())
                if (handler.triggerList != null && handler.triggerList.Contains(unchecked((int)0xeaa08c95)))
                    throw new InvalidOperationException("Original Wwise destroy-trigger dispatch is not bound.");
            if (mHost != null && mHost.IsInitialized) mHost.UnregisterGameObject(gameObject);
        }
    }
}
