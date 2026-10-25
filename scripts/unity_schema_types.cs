// Extraction schema for Unity's built-in serialized value types. Never shipped
// or loaded by the game; game-specific field layouts come from the pinned DLLs.
using System;
// Public serialized fields of the pinned global TMPColorGradient component.
// Its private text cache and rotation matrix are runtime values.
public class TMPColorGradient : UnityEngine.MonoBehaviour {
    public UnityEngine.Gradient Gradient;
    public float Angle;
}
namespace UnityEngine {
    public class Object {} public class Component : Object {} public class Behaviour : Component {}
    public class MonoBehaviour : Behaviour {} public class ScriptableObject : Object {}
    public class Transform : Component {} public class RectTransform : Transform {}
    public class GridLayout : Behaviour {} public class Grid : GridLayout {}
    public class GameObject : Object {} public class Sprite : Object {} public class Material : Object {}
    public class Texture : Object {} public class Texture2D : Texture {} public class AnimationClip : Object {}
    public class AudioClip : Object {}
    // Serialized as an integer by Unity; values are interpreted by the pinned client.
    public enum RuntimePlatform { }
    [Serializable] public class Gradient {} public class RuntimeAnimatorController : Object {}
    public class Animator : Behaviour {} public class Animation : Behaviour {}
    public enum AnimatorControllerParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }
    public class ParticleSystem : Component {} public class ParticleSystemRenderer : Renderer {}
    public class TextAsset : Object {} public class CanvasGroup : Behaviour {} public class Canvas : Behaviour {}
    public class RenderTexture : Texture {} public class Camera : Behaviour {}
    public class Renderer : Component {} public class SpriteRenderer : Renderer {}
    public class LineRenderer : Renderer {}
    public class MeshRenderer : Renderer {} public class MeshFilter : Component {}
    public class SpriteMask : Renderer {} public class Mesh : Object {}
    public enum FilterMode { Point,Bilinear,Trilinear }
    public enum RenderTextureFormat { ARGB32,Depth,ARGBHalf,Shadowmap,RGB565,ARGB4444,ARGB1555,Default,ARGB2101010,DefaultHDR,ARGB64,ARGBFloat,RGFloat,RGHalf,RFloat,RHalf,R8,ARGBInt,RGInt,RInt,BGRA32,RGB111110Float,RG32,RGBAUShort,RG16,R16 }
    public class SerializeField : Attribute {} public class SerializeReference : Attribute {}
    public class HideInInspector : Attribute {}
    [Serializable] public struct Vector2 { public float x,y; }
    [Serializable] public struct Vector3 { public float x,y,z; }
    [Serializable] public struct Vector4 { public float x,y,z,w; }
    [Serializable] public struct Vector2Int { public int m_X,m_Y; }
    [Serializable] public struct Vector3Int { public int m_X,m_Y,m_Z; }
    [Serializable] public struct Quaternion { public float x,y,z,w; }
    [Serializable] public struct Color { public float r,g,b,a; }
    [Serializable] public struct Rect { public float x,y,width,height; }
    [Serializable] public struct Bounds { public Vector3 center,extents; }
    [Serializable] public struct BoundsInt { public Vector3Int m_Position,m_Size; }
    // Unity's native serialized Matrix4x4 uses row order, independently of
    // the managed struct's in-memory field order.
    [Serializable] public struct Matrix4x4 { public float e00,e01,e02,e03,e10,e11,e12,e13,e20,e21,e22,e23,e30,e31,e32,e33; }
    [Serializable] public struct AnimationCurve { public Keyframe[] m_Curve; public int m_PreInfinity,m_PostInfinity,m_RotationOrder; }
    [Serializable] public struct Keyframe { public float time,value,inSlope,outSlope; public int weightedMode; public float inWeight,outWeight; }
}
namespace UnityEngine.Tilemaps {
    public class Tilemap : UnityEngine.Component {}
    public class TilemapRenderer : UnityEngine.Renderer {}
    public class TileBase : UnityEngine.ScriptableObject {}
    public class Tile : TileBase {
        public UnityEngine.Sprite m_Sprite; public UnityEngine.Color m_Color;
        public UnityEngine.Matrix4x4 m_Transform; public UnityEngine.GameObject m_InstancedGameObject;
        public int m_Flags,m_ColliderType;
    }
}
namespace UnityEngine.Playables {
    public class PlayableDirector : UnityEngine.Behaviour {}
    public class PlayableAsset : UnityEngine.ScriptableObject {}
    [Serializable] public class PlayableBehaviour {}
}
namespace UnityEngine {
    public class AvatarMask : Object {}
    [Serializable] public struct PropertyName { public string id; }
    [Serializable] public struct ExposedReference<T> where T : Object { public PropertyName exposedName; public T defaultValue; }
}
// Serialized Timeline layouts from Unity's official UPM package 1.7.6.
// Partial upgrade fields precede the runtime fields in the native assembly.
namespace UnityEngine.Timeline {
    using System.Collections.Generic;
    using UnityEngine.Playables;
    public class TimelineAsset : PlayableAsset {
        public int m_Version;
        public List<ScriptableObject> m_Tracks; public double m_FixedDuration;
        public EditorSettings m_EditorSettings; public int m_DurationMode; public MarkerTrack m_MarkerTrack;
        [Serializable] public class EditorSettings { public double m_Framerate; public bool m_ScenePreview; }
    }
    public class TrackAsset : PlayableAsset {
        public int m_Version; public AnimationClip m_AnimClip;
        public bool m_Locked,m_Muted; public string m_CustomPlayableFullTypename;
        public AnimationClip m_Curves; public PlayableAsset m_Parent;
        public List<ScriptableObject> m_Children; public List<TimelineClip> m_Clips; public MarkerList m_Markers;
    }
    [Serializable] public class TimelineClip {
        public int m_Version; public double m_Start,m_ClipIn; public Object m_Asset;
        public double m_Duration,m_TimeScale; public TrackAsset m_ParentTrack;
        public double m_EaseInDuration,m_EaseOutDuration,m_BlendInDuration,m_BlendOutDuration;
        public AnimationCurve m_MixInCurve,m_MixOutCurve; public int m_BlendInCurveMode,m_BlendOutCurveMode;
        public List<string> m_ExposedParameterNames; public AnimationClip m_AnimationCurves;
        public bool m_Recordable; public int m_PostExtrapolationMode,m_PreExtrapolationMode;
        public double m_PostExtrapolationTime,m_PreExtrapolationTime; public string m_DisplayName;
    }
    [Serializable] public struct MarkerList { public List<ScriptableObject> m_Objects; }
    public class MarkerTrack : TrackAsset {}
    public class SignalTrack : TrackAsset {}
    public class Marker : ScriptableObject { public double m_Time; }
    public class SignalAsset : ScriptableObject {}
    public class SignalReceiver : MonoBehaviour {
        public EventKeyValue m_Events;
        [Serializable] public class EventKeyValue { public List<SignalAsset> m_Signals; public List<UnityEngine.Events.UnityEvent> m_Events; }
    }
    public class SignalEmitter : Marker { public bool m_Retroactive,m_EmitOnce; public SignalAsset m_Asset; }
    public class GroupTrack : TrackAsset {}
    public class ActivationTrack : TrackAsset { public int m_PostPlaybackState; }
    public class ActivationPlayableAsset : PlayableAsset {}
    public class ControlTrack : TrackAsset {}
    public class ControlPlayableAsset : PlayableAsset {
        public ExposedReference<GameObject> sourceGameObject; public GameObject prefabGameObject;
        public bool updateParticle; public uint particleRandomSeed;
        public bool updateDirector,updateITimeControl,searchHierarchy,active; public int postPlayback;
    }
    public class AnimationPlayableAsset : PlayableAsset {
        public AnimationClip m_Clip; public Vector3 m_Position,m_EulerAngles;
        public bool m_UseTrackMatchFields; public int m_MatchTargetFields;
        public bool m_RemoveStartOffset,m_ApplyFootIK; public int m_Loop;
        public int m_Version; public Quaternion m_Rotation;
    }
    public class AnimationTrack : TrackAsset {
        public int m_InfiniteClipPreExtrapolation,m_InfiniteClipPostExtrapolation;
        public Vector3 m_InfiniteClipOffsetPosition,m_InfiniteClipOffsetEulerAngles; public double m_InfiniteClipTimeOffset;
        public bool m_InfiniteClipRemoveOffset,m_InfiniteClipApplyFootIK; public int mInfiniteClipLoop,m_MatchTargetFields;
        public Vector3 m_Position,m_EulerAngles; public AvatarMask m_AvatarMask;
        public bool m_ApplyAvatarMask; public int m_TrackOffset; public AnimationClip m_InfiniteClip;
        public Quaternion m_OpenClipOffsetRotation,m_Rotation; public bool m_ApplyOffsets;
    }
}
// Cinemachine's official UPM 2.9.7 Timeline binding layout.
namespace UnityEngine.Events {
    [Serializable] public class UnityEvent { public PersistentCallGroup m_PersistentCalls; }
    [Serializable] public class UnityEvent<T> : UnityEvent {}
    [Serializable] public class UnityEvent<T0, T1> : UnityEvent {}
    [Serializable] public class PersistentCallGroup { public System.Collections.Generic.List<PersistentCall> m_Calls; }
    [Serializable] public class PersistentCall {
        public UnityEngine.Object m_Target; public string m_TargetAssemblyTypeName,m_MethodName; public int m_Mode;
        public ArgumentCache m_Arguments; public int m_CallState;
    }
    [Serializable] public class ArgumentCache {
        public UnityEngine.Object m_ObjectArgument; public string m_ObjectArgumentAssemblyTypeName;
        public int m_IntArgument; public float m_FloatArgument; public string m_StringArgument; public bool m_BoolArgument;
    }
}
namespace Cinemachine {
    using UnityEngine;
    public class CinemachineVirtualCameraBase : MonoBehaviour {
        public string[] m_ExcludedPropertiesInInspector; public int[] m_LockStageInInspector;
        public int m_StreamingVersion,m_Priority,m_StandbyUpdate;
        [Serializable] public struct TransitionParams { public int m_BlendHint; public bool m_InheritPosition; public UnityEngine.Events.UnityEvent m_OnCameraLive; }
    }
    [Serializable] public struct LensSettings {
        public float FieldOfView,OrthographicSize,NearClipPlane,FarClipPlane,Dutch;
        public int ModeOverride; public Vector2 LensShift; public int GateFit; public float FocusDistance;
        public Vector2 m_SensorSize;
    }
    public class CinemachineVirtualCamera : CinemachineVirtualCameraBase {
        public Transform m_LookAt,m_Follow; public LensSettings m_Lens; public TransitionParams m_Transitions;
        public int m_LegacyBlendHint; public Transform m_ComponentOwner;
    }
    public class CinemachineComponentBase : MonoBehaviour {}
    public class CinemachineTransposer : CinemachineComponentBase {
        public int m_BindingMode; public Vector3 m_FollowOffset;
        public float m_XDamping,m_YDamping,m_ZDamping; public int m_AngularDampingMode;
        public float m_PitchDamping,m_YawDamping,m_RollDamping,m_AngularDamping;
    }
    [Serializable] public struct CinemachineBlendDefinition {
        public int m_Style; public float m_Time; public AnimationCurve m_CustomCurve;
    }
    public class CinemachineBlenderSettings : ScriptableObject {}
    public class CinemachineBrain : MonoBehaviour {
        public bool m_ShowDebugText,m_ShowCameraFrustum,m_IgnoreTimeScale;
        public Transform m_WorldUpOverride; public int m_UpdateMethod,m_BlendUpdateMethod;
        public CinemachineBlendDefinition m_DefaultBlend; public CinemachineBlenderSettings m_CustomBlends;
        public UnityEngine.Events.UnityEvent m_CameraCutEvent,m_CameraActivatedEvent;
    }
    [Serializable] public struct ImpulseEnvelope {
        public AnimationCurve m_AttackShape,m_DecayShape;
        public float m_AttackTime,m_SustainTime,m_DecayTime;
        public bool m_ScaleWithImpact,m_HoldForever;
    }
    [Serializable] public class CinemachineImpulseDefinition {
        public int m_ImpulseChannel,m_ImpulseShape; public AnimationCurve m_CustomImpulseShape;
        public float m_ImpulseDuration; public int m_ImpulseType; public float m_DissipationRate;
        public SignalSourceAsset m_RawSignal; public float m_AmplitudeGain,m_FrequencyGain;
        public int m_RepeatMode; public bool m_Randomize; public ImpulseEnvelope m_TimeEnvelope;
        public float m_ImpactRadius; public int m_DirectionMode,m_DissipationMode;
        public float m_DissipationDistance,m_PropagationSpeed;
    }
    public class CinemachineImpulseSource : MonoBehaviour {
        public CinemachineImpulseDefinition m_ImpulseDefinition; public Vector3 m_DefaultVelocity;
    }
    public class CinemachineImpulseListener : MonoBehaviour {
        public int m_ApplyAfter,m_ChannelMask; public float m_Gain;
        public bool m_Use2DDistance,m_UseCameraSpace;
        [Serializable] public struct ImpulseReaction {
            public NoiseSettings m_SecondaryNoise; public float m_AmplitudeGain,m_FrequencyGain,m_Duration;
            public Vector3 m_NoiseOffsets;
        }
        public ImpulseReaction m_ReactionSettings;
    }
    public class CinemachineBasicMultiChannelPerlin : CinemachineComponentBase {
        public NoiseSettings m_NoiseProfile; public Vector3 m_PivotOffset; public float m_AmplitudeGain,m_FrequencyGain;
        public Vector3 mNoiseOffsets;
    }
    public class SignalSourceAsset : ScriptableObject {}
    public class NoiseSettings : SignalSourceAsset {
        [Serializable] public struct NoiseParams { public float Frequency,Amplitude; public bool Constant; }
        [Serializable] public struct TransformNoiseParams { public NoiseParams X,Y,Z; }
        public TransformNoiseParams[] PositionNoise,OrientationNoise;
    }
}
public class CinemachineTrack : UnityEngine.Timeline.TrackAsset {}
public class CinemachineShot : UnityEngine.Playables.PlayableAsset {
    public string DisplayName;
    public UnityEngine.ExposedReference<Cinemachine.CinemachineVirtualCameraBase> VirtualCamera;
}
namespace AK.Wwise.Unity.Timeline {
    public class AkTimelineEventTrack : UnityEngine.Timeline.TrackAsset {}
}
public class AkTimelineEventTrack : UnityEngine.Timeline.TrackAsset {}
public class SimpleAnimation : UnityEngine.MonoBehaviour {}
public class AkTimelineEventPlayable : UnityEngine.Playables.PlayableAsset {
    public AK.Wwise.Event akEvent; public int blendInCurve,blendOutCurve;
    public float eventDurationMax,eventDurationMin;
    public bool retriggerEvent,UseWwiseEventDuration,PrintDebugInformation,StopEventAtClipEnd;
}
namespace UnityEngine.Rendering.Universal { public class Light2D : UnityEngine.Behaviour {} }
namespace UnityEngine.EventSystems { public class UIBehaviour : UnityEngine.MonoBehaviour {} }
namespace UnityEngine.UI {
    public class Graphic : UnityEngine.MonoBehaviour {
        public UnityEngine.Material m_Material; public UnityEngine.Color m_Color;
        public bool m_RaycastTarget; public UnityEngine.Vector4 m_RaycastPadding;
    }
    public class MaskableGraphic : Graphic {
        [Serializable] public class CullStateChangedEvent : UnityEngine.Events.UnityEvent<bool> {}
        public bool m_Maskable; public CullStateChangedEvent m_OnCullStateChanged;
    }
    public class Image : MaskableGraphic {
        public UnityEngine.Sprite m_Sprite; public int m_Type; public bool m_PreserveAspect,m_FillCenter;
        public int m_FillMethod; public float m_FillAmount; public bool m_FillClockwise;
        public int m_FillOrigin; public bool m_UseSpriteMesh; public float m_PixelsPerUnitMultiplier;
    }
    public class RawImage : MaskableGraphic {} public class Text : MaskableGraphic {}
    public class Selectable : UnityEngine.MonoBehaviour {} public class Button : Selectable {}
    public class ToggleGroup : UnityEngine.EventSystems.UIBehaviour {}
    public class Scrollbar : Selectable {}
    public class ScrollRect : UnityEngine.MonoBehaviour {} public class Toggle : Selectable {}
    public class ContentSizeFitter : UnityEngine.EventSystems.UIBehaviour {}
    public class LayoutElement : UnityEngine.EventSystems.UIBehaviour {}
    public class LayoutGroup : UnityEngine.MonoBehaviour {} public class GridLayoutGroup : LayoutGroup {} public class Slider : Selectable {}
    public class RectMask2D : UnityEngine.MonoBehaviour {} public class Mask : UnityEngine.MonoBehaviour {}
}
namespace Spine.Unity {
    public class AnimationReferenceAsset : UnityEngine.ScriptableObject {
        public SkeletonDataAsset skeletonDataAsset; public string animationName;
    }
    public class SkeletonRenderer : UnityEngine.MonoBehaviour {}
    public class SkeletonAnimation : SkeletonRenderer {}
    public class SkeletonMecanim : SkeletonRenderer {}
    public class SkeletonGraphic : UnityEngine.MonoBehaviour {}
    public class SkeletonDataAsset : UnityEngine.ScriptableObject {}
    public class AtlasAssetBase : UnityEngine.ScriptableObject {}
    public class SpineAtlasAsset : AtlasAssetBase {}
}
namespace Spine.Unity.Playables {
    public class SpineAnimationStateClip : UnityEngine.Playables.PlayableAsset { public SpineAnimationStateBehaviour template; }
    [Serializable] public class SpineAnimationStateBehaviour : UnityEngine.Playables.PlayableBehaviour {
        public Spine.Unity.AnimationReferenceAsset animationReference; public bool loop,customDuration,useBlendDuration,isInitialized;
        public float mixDuration; public bool holdPrevious,dontPauseWithDirector,dontEndWithClip;
        public float endMixOutDuration,attachmentThreshold,eventThreshold,drawOrderThreshold,alpha;
    }
}
// Only Unity object identity is needed when these components occur as PPtr fields.
namespace TMPro {
    public class TMP_Text : UnityEngine.MonoBehaviour {}
    public class TextMeshProUGUI : TMP_Text {}
    public class TMP_InputField : UnityEngine.MonoBehaviour {}
    // Serialized fields from the pinned TMPro.TMPEffect metadata, in order.
    public class TMPEffect : UnityEngine.MonoBehaviour {
        public float dilate;
        public bool linearDodge,useVertexColor,RealtimeUpdate,outline;
        public UnityEngine.Color outlineColor; public float outlineWidth;
        public bool outline2; public UnityEngine.Color outlineColor2; public float outlineWidth2;
        public bool underlay; public UnityEngine.Color underlayColor;
        public float shadowAngle,shadowDis,shadowBlur;
        public bool glow; public UnityEngine.Color glowColor;
        public float glowOffset,glowInner,glowOuter,glowPower;
        public bool gray;
    }
}
// AOT-only Wwise data. Names and field types are retained in the pinned
// global-metadata.dat; the full EC payload must still pass byte validation.
public class WwiseObjectReference : UnityEngine.ScriptableObject {
    public string objectName; public uint id; public string guid;
}
public class WwiseEventReference : WwiseObjectReference {}
public class WwiseGroupValueObjectReference : WwiseObjectReference {}
public class WwiseSwitchGroupReference : WwiseObjectReference {}
public class WwiseSwitchReference : WwiseGroupValueObjectReference { public WwiseSwitchGroupReference WwiseSwitchGroupReference; }
namespace AK.Wwise {
    [Serializable] public class BaseType { public int idInternal; public byte[] valueGuidInternal; }
    [Serializable] public class Event : BaseType { public WwiseEventReference WwiseObjectReference; }
    [Serializable] public class BaseGroupType : BaseType { public int groupIdInternal; public byte[] groupGuidInternal; }
    [Serializable] public class Switch : BaseGroupType { public WwiseSwitchReference WwiseObjectReference; }
}
