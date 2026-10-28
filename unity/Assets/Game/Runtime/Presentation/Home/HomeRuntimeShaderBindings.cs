using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Zjcs.Presentation.Home
{
    // StreamingAssets retain material names, but do not retain shaders in a Player.
    // These references also keep the verified native programs in the build.
    public sealed class HomeRuntimeShaderBindings : ScriptableObject
    {
        public Shader Effect;
        public Shader SkyColorShift;
        public Shader SkyDefault;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void RestoreStartupPipeline()
        {
            if (Application.isEditor) return;
            GraphicsSettings.defaultRenderPipeline = null;
            QualitySettings.renderPipeline = null;
        }

        public static Shader Resolve(string originalName)
        {
            var binding = Resources.Load<HomeRuntimeShaderBindings>("Home/RuntimeShaderBindings");
            if (binding == null) throw new InvalidOperationException("Home runtime shader bindings are not installed.");
            Shader shader;
            switch (originalName) {
                case "Zjcs/Map10/Official Effect": shader = binding.Effect; break;
                case "Universal Render Pipeline/2D/Sprite-Lit-ColorShift": shader = binding.SkyColorShift; break;
                case "Universal Render Pipeline/2D/Sprite-Lit-Default": shader = binding.SkyDefault; break;
                default: throw new InvalidOperationException("Unbound original Home shader: " + originalName);
            }
            if (shader == null) throw new InvalidOperationException("Missing Home shader reference: " + originalName);
            return shader;
        }
    }
}
