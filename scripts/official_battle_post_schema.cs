// Extraction-only serialized layouts, matched to the pinned IL2CPP field order.
// Enums use their original int32 representation; engine objects remain PPtrs.
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine.Rendering.Universal {
 public class UniversalRenderPipelineAsset : ScriptableObject {
public int k_AssetVersion;
public int k_AssetPreviousVersion;
public int m_RendererType;
public UnityEngine.Object m_RendererData;
public UnityEngine.Object[] m_RendererDataList;
public int m_DefaultRendererIndex;
public bool m_RequireDepthTexture;
public bool m_RequireOpaqueTexture;
public int m_OpaqueDownsampling;
public bool m_SupportsTerrainHoles;
public bool m_SupportsHDR;
public int m_HDRColorBufferPrecision;
public int m_MSAA;
public float m_RenderScale;
public int m_UpscalingFilter;
public bool m_FsrOverrideSharpness;
public float m_FsrSharpness;
public bool m_EnableLODCrossFade;
public int m_LODCrossFadeDitheringType;
public int m_ShEvalMode;
public int m_MainLightRenderingMode;
public bool m_MainLightShadowsSupported;
public int m_MainLightShadowmapResolution;
public int m_AdditionalLightsRenderingMode;
public int m_AdditionalLightsPerObjectLimit;
public bool m_AdditionalLightShadowsSupported;
public int m_AdditionalLightsShadowmapResolution;
public int m_AdditionalLightsShadowResolutionTierLow;
public int m_AdditionalLightsShadowResolutionTierMedium;
public int m_AdditionalLightsShadowResolutionTierHigh;
public bool m_ReflectionProbeBlending;
public bool m_ReflectionProbeBoxProjection;
public float m_ShadowDistance;
public int m_ShadowCascadeCount;
public float m_Cascade2Split;
public Vector2 m_Cascade3Split;
public Vector3 m_Cascade4Split;
public float m_CascadeBorder;
public float m_ShadowDepthBias;
public float m_ShadowNormalBias;
public bool m_SoftShadowsSupported;
public bool m_ConservativeEnclosingSphere;
public int m_NumIterationsEnclosingSphere;
public int m_SoftShadowQuality;
public int m_AdditionalLightsCookieResolution;
public int m_AdditionalLightsCookieFormat;
public bool m_UseSRPBatcher;
public bool m_SupportsDynamicBatching;
public bool m_MixedLightingSupported;
public bool m_SupportsLightCookies;
public bool m_SupportsLightLayers;
public int m_DebugLevel;
public int m_StoreActionsOptimization;
public bool m_EnableRenderGraph;
public bool m_UseAdaptivePerformance;
public int m_ColorGradingMode;
public int m_ColorGradingLutSize;
public bool m_UseFastSRGBLinearConversion;
public bool m_SupportDataDrivenLensFlare;
public int m_ShadowType;
public bool m_LocalShadowsSupported;
public int m_LocalShadowsAtlasResolution;
public int m_MaxPixelLights;
public int m_ShadowAtlasResolution;
public int m_VolumeFrameworkUpdateMode;
public PipelineTextures m_Textures;
 public int m_ShaderVariantLogLevel,m_ShadowCascades;
 }
 [Serializable] public class PipelineTextures { public Texture2D blueNoise64LTex,bayerMatrixTex; }
 public class ScriptableRendererData : ScriptableObject {
  public DebugShaders debugShaders; public List<UnityEngine.Object> m_RendererFeatures;
  public List<long> m_RendererFeatureMap; public bool m_UseNativeRenderPass;
 }
 [Serializable] public class DebugShaders { public UnityEngine.Object debugReplacementPS,hdrDebugViewPS; }
 [Serializable] public class LightStyle { public string name; public int maskTextureChannel,blendMode; }
 public class Renderer2DData : ScriptableRendererData {
  public int m_TransparencySortMode; public Vector3 m_TransparencySortAxis;
  public float m_HDREmulationScale,m_LightRenderTextureScale; public LightStyle[] m_LightBlendStyles;
  public bool m_UseDepthStencilBuffer,m_UseCameraSortingLayersTexture;
  public int m_CameraSortingLayersTextureBound,m_CameraSortingLayerDownsamplingMethod;
  public uint m_MaxLightRenderTextureCount,m_MaxShadowRenderTextureCount;
  public UnityEngine.Object m_ShapeLightShader,m_ShapeLightVolumeShader,m_PointLightShader,m_PointLightVolumeShader,
   m_CoreBlitShader,m_BlitHDROverlay,m_CoreBlitColorAndDepthPS,m_SamplingShader,m_ProjectedShadowShader,
   m_SpriteShadowShader,m_SpriteUnshadowShader,m_GeometryUnshadowShader,m_FallbackErrorShader;
  public PostProcessData m_PostProcessData; public Texture2D m_FallOffLookup;
 }
 public class PostProcessData : ScriptableObject { public ShaderResources shaders; public PostTextures textures; }
 [Serializable] public class ShaderResources {
  public UnityEngine.Object stopNanPS,subpixelMorphologicalAntialiasingPS,gaussianDepthOfFieldPS,bokehDepthOfFieldPS,
   cameraMotionBlurPS,paniniProjectionPS,lutBuilderLdrPS,lutBuilderHdrPS,bloomPS,temporalAntialiasingPS,
   LensFlareDataDrivenPS,scalingSetupPS,easuPS,uberPostPS,finalPostPassPS;
 }
 [Serializable] public class PostTextures {
  public Texture2D[] blueNoise16LTex,filmGrainTex; public Texture2D smaaAreaTex,smaaSearchTex;
 }
 public class UniversalAdditionalCameraData : MonoBehaviour {
  public bool m_RenderShadows; public int m_RequiresDepthTextureOption,m_RequiresOpaqueTextureOption,m_CameraType;
  public List<Camera> m_Cameras; public int m_RendererIndex; public Mask m_VolumeLayerMask;
  public Transform m_VolumeTrigger; public int m_VolumeFrameworkUpdateModeOption; public bool m_RenderPostProcessing;
  public int m_Antialiasing,m_AntialiasingQuality; public bool m_StopNaN,m_Dithering,m_ClearDepth,m_AllowXRRendering,
   m_AllowHDROutput,m_UseScreenCoordOverride; public Vector4 m_ScreenSizeOverride,m_ScreenCoordScaleBias;
  public bool m_RequiresDepthTexture,m_RequiresColorTexture; public float m_Version;
  public TaaSettings m_TaaSettings;
 }
 [Serializable] public struct TaaSettings {
  public int m_Quality; public float m_FrameInfluence,m_JitterScale,m_MipBias,m_VarianceClampScale,m_ContrastAdaptiveSharpening;
 }
 [Serializable] public class Mask { public uint m_Bits; }
 public class Bloom : UnityEngine.Rendering.VolumeComponent {
  public IntParam skipIterations; public FloatParam threshold,intensity,scatter,clamp;
  public ColorParam tint; public BoolParam highQualityFiltering; public IntParam downscale,maxIterations;
  public TextureParam dirtTexture; public FloatParam dirtIntensity;
 }
 [Serializable] public class IntParam { public bool m_OverrideState; public int m_Value; }
 [Serializable] public class FloatParam { public bool m_OverrideState; public float m_Value; }
 [Serializable] public class ColorParam { public bool m_OverrideState; public Color m_Value; }
 [Serializable] public class BoolParam { public bool m_OverrideState,m_Value; }
 [Serializable] public class TextureParam { public bool m_OverrideState; public Texture m_Value; public int dimension; }
}
namespace UnityEngine.Rendering {
 public class Volume : MonoBehaviour {
  public bool m_IsGlobal; public float priority,blendDistance,weight; public VolumeProfile sharedProfile;
 }
 public class VolumeProfile : ScriptableObject { public List<VolumeComponent> components; }
 public class VolumeComponent : ScriptableObject { public bool active; }
}
