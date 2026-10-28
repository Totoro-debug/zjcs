using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Native AkEvent / AkTriggerHandler source scope used by the profession bundle.
    // Callbacks and actions are rejected until their platform host is migrated.
    public sealed class AkEvent : MonoBehaviour
    {
        [Serializable] public sealed class CallbackData
        {
            public int Flags;
            public string FunctionName;
            public GameObject GameObject;
        }
        public const int EnableTrigger = unchecked((int)0xece0d9f2);
        public const int StartTrigger = 0x4c66e1f7;
        public List<int> triggerList;
        public bool useOtherObject;
        public int actionOnEventType, curveInterpolation;
        public bool enableActionOnEvent;
        public OfficialWwiseEvent data;
        public bool useCallbacks;
        public CallbackData[] Callbacks;
        public uint playingId;
        public GameObject soundEmitterObject;
        public float transitionDuration;
        public int eventIdInternal;
        public byte[] valueGuidInternal;
        public MonoBehaviour m_callbackDataInternal;

        private void Awake()
        {
            if (enableActionOnEvent || useCallbacks || (Callbacks != null && Callbacks.Length != 0))
                throw new InvalidOperationException("This native profession event needs its original callback/action host.");
            if (triggerList == null || triggerList.Exists(value => value != EnableTrigger && value != StartTrigger))
                throw new InvalidOperationException("This native profession event needs its original trigger handler.");
        }
        private void OnEnable()
        {
            if (triggerList.Contains(EnableTrigger)) HandleEvent(null);
        }
        private void Start()
        {
            soundEmitterObject = gameObject;
            if (triggerList.Contains(StartTrigger)) HandleEvent(null);
        }
        public void HandleEvent(GameObject otherObject)
        {
            soundEmitterObject = useOtherObject && otherObject != null ? otherObject : gameObject;
            playingId = data.Post(soundEmitterObject);
        }
    }
}
