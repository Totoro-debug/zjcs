using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Combat
{
    // Explicit binding to the APK's global UniversalRenderPipeline asset. The previous
    // scene's pipeline is restored when the offline battle session releases its renderers.
    public sealed class OfficialBattleRenderPipelineAssets : ScriptableObject
    {
        public RenderPipelineAsset Pipeline;
        public IDisposable Enter()
        {
            if(!Pipeline)throw new InvalidOperationException("Original battle render pipeline is not bound.");
            return new Scope(Pipeline);
        }
        private sealed class Scope : IDisposable
        {
            private readonly RenderPipelineAsset previousDefault,previousQuality,owned;
            private bool disposed;
            public Scope(RenderPipelineAsset pipeline)
            {
                owned=pipeline;previousDefault=GraphicsSettings.defaultRenderPipeline;previousQuality=QualitySettings.renderPipeline;
                QualitySettings.renderPipeline=null;GraphicsSettings.defaultRenderPipeline=pipeline;
            }
            public void Dispose()
            {
                if(disposed)return;disposed=true;
                if(GraphicsSettings.defaultRenderPipeline==owned && QualitySettings.renderPipeline==null)
                {GraphicsSettings.defaultRenderPipeline=previousDefault;QualitySettings.renderPipeline=previousQuality;}
            }
        }
    }
}
