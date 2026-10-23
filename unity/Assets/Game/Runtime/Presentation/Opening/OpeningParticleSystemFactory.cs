using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Opening
{
    public static class OpeningParticleSystemFactory
    {
        public static void Configure(ParticleSystem system, Dictionary<string, object> source)
        {
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.duration = Float(source, "lengthInSec", 2f);
            main.loop = Bool(source, "looping");
            main.prewarm = Bool(source, "prewarm");
            main.playOnAwake = Bool(source, "playOnAwake");
            main.useUnscaledTime = Bool(source, "useUnscaledTime");
            main.simulationSpeed = Float(source, "simulationSpeed", 1f);
            main.scalingMode = (ParticleSystemScalingMode)Integer(source, "scalingMode", 1);
            main.cullingMode = (ParticleSystemCullingMode)Integer(source, "cullingMode");
            main.stopAction = (ParticleSystemStopAction)Integer(source, "stopAction");
            main.ringBufferMode = (ParticleSystemRingBufferMode)Integer(source, "ringBufferMode");
            main.startDelay = Curve(source, "startDelay", 0f);
            main.maxParticles = Integer(Object(source, "InitialModule"), "maxNumParticles", 1000);
            system.useAutoRandomSeed = Bool(source, "autoRandomSeed", true);
            system.randomSeed = (uint)Math.Max(0, Integer(source, "randomSeed"));

            Dictionary<string, object> initial = Object(source, "InitialModule");
            main.startLifetime = Curve(initial, "startLifetime", 1f);
            main.startSpeed = Curve(initial, "startSpeed", 0f);
            main.startColor = MinMaxColor(initial, "startColor");
            main.startSize3D = Bool(initial, "size3D");
            if (main.startSize3D)
            {
                main.startSizeX = Curve(initial, "startSize", 1f);
                main.startSizeY = Curve(initial, "startSizeY", 1f);
                main.startSizeZ = Curve(initial, "startSizeZ", 1f);
            }
            else
            {
                main.startSize = Curve(initial, "startSize", 1f);
            }
            main.startRotation3D = Bool(initial, "rotation3D");
            if (main.startRotation3D)
            {
                main.startRotationX = Curve(initial, "startRotationX", 0f);
                main.startRotationY = Curve(initial, "startRotationY", 0f);
                main.startRotationZ = Curve(initial, "startRotation", 0f);
            }
            else
            {
                main.startRotation = Curve(initial, "startRotation", 0f);
            }
            main.gravityModifier = Curve(initial, "gravityModifier", 0f);
            main.flipRotation = Float(initial, "randomizeRotationDirection");

            Dictionary<string, object> shapeData = Object(source, "ShapeModule");
            var shape = system.shape;
            shape.enabled = Bool(shapeData, "enabled");
            shape.shapeType = (ParticleSystemShapeType)Integer(shapeData, "type");
            shape.angle = Float(shapeData, "angle");
            shape.length = Float(shapeData, "length");
            shape.radius = Float(Object(shapeData, "radius"), "value");
            shape.radiusThickness = Float(shapeData, "radiusThickness", 1f);
            shape.donutRadius = Float(shapeData, "donutRadius");
            shape.arc = Float(Object(shapeData, "arc"), "value", 360f);
            shape.position = Vector3Value(shapeData, "m_Position");
            shape.rotation = Vector3Value(shapeData, "m_Rotation");
            shape.scale = Vector3Value(shapeData, "m_Scale", Vector3.one);
            shape.alignToDirection = Bool(shapeData, "alignToDirection");
            shape.randomPositionAmount = Float(shapeData, "randomPositionAmount");
            shape.randomDirectionAmount = Float(shapeData, "randomDirectionAmount");
            shape.sphericalDirectionAmount = Float(shapeData, "sphericalDirectionAmount");

            Dictionary<string, object> emissionData = Object(source, "EmissionModule");
            var emission = system.emission;
            emission.enabled = Bool(emissionData, "enabled");
            emission.rateOverTime = Curve(emissionData, "rateOverTime", 0f);
            emission.rateOverDistance = Curve(emissionData, "rateOverDistance", 0f);

            Dictionary<string, object> colorData = Object(source, "ColorModule");
            var color = system.colorOverLifetime;
            color.enabled = Bool(colorData, "enabled");
            color.color = MinMaxColor(colorData, "gradient");

            Dictionary<string, object> sizeData = Object(source, "SizeBySpeedModule");
            var sizeBySpeed = system.sizeBySpeed;
            sizeBySpeed.enabled = Bool(sizeData, "enabled");
            sizeBySpeed.separateAxes = Bool(sizeData, "separateAxes");
            if (sizeBySpeed.separateAxes)
            {
                sizeBySpeed.x = Curve(sizeData, "curve", 1f);
                sizeBySpeed.y = Curve(sizeData, "y", 1f);
                sizeBySpeed.z = Curve(sizeData, "z", 1f);
            }
            else
            {
                sizeBySpeed.size = Curve(sizeData, "curve", 1f);
            }
            sizeBySpeed.range = Vector2Value(sizeData, "range", new Vector2(0f, 1f));
        }

        public static void ConfigureRenderer(ParticleSystemRenderer renderer,
            Dictionary<string, object> source)
        {
            if (renderer == null || source == null) return;
            renderer.enabled = Bool(source, "m_Enabled", true);
            renderer.renderMode = (ParticleSystemRenderMode)Integer(source, "m_RenderMode");
            renderer.sortingLayerID = Integer(source, "m_SortingLayerID");
            renderer.sortingOrder = Integer(source, "m_SortingOrder");
            renderer.sortMode = (ParticleSystemSortMode)Integer(source, "m_SortMode");
            renderer.minParticleSize = Float(source, "m_MinParticleSize");
            renderer.maxParticleSize = Float(source, "m_MaxParticleSize", 0.5f);
            renderer.lengthScale = Float(source, "m_LengthScale", 2f);
            renderer.normalDirection = Float(source, "m_NormalDirection", 1f);
            renderer.pivot = Vector3Value(source, "m_Pivot");
            renderer.flip = Vector3Value(source, "m_Flip");
            renderer.maskInteraction = (SpriteMaskInteraction)Integer(source, "m_MaskInteraction");
        }

        private static ParticleSystem.MinMaxCurve Curve(Dictionary<string, object> owner, string key,
            float fallback)
        {
            if (!owner.TryGetValue(key, out object raw) || !(raw is Dictionary<string, object> source))
                return new ParticleSystem.MinMaxCurve(fallback);
            int mode = Integer(source, "minMaxState");
            float value = Float(source, "scalar", fallback);
            float minimum = Float(source, "minScalar", value);
            switch ((ParticleSystemCurveMode)mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(value);
                case ParticleSystemCurveMode.Curve:
                    return new ParticleSystem.MinMaxCurve(value, AnimationCurveValue(source, "maxCurve"));
                case ParticleSystemCurveMode.TwoCurves:
                    return new ParticleSystem.MinMaxCurve(value,
                        AnimationCurveValue(source, "minCurve"), AnimationCurveValue(source, "maxCurve"));
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(minimum, value);
                default:
                    return new ParticleSystem.MinMaxCurve(fallback);
            }
        }

        private static ParticleSystem.MinMaxGradient MinMaxColor(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = Object(owner, key);
            switch ((ParticleSystemGradientMode)Integer(source, "minMaxState"))
            {
                case ParticleSystemGradientMode.Color:
                    return new ParticleSystem.MinMaxGradient(ColorValue(source, "maxColor", Color.white));
                case ParticleSystemGradientMode.Gradient:
                    return new ParticleSystem.MinMaxGradient(GradientValue(source, "maxGradient"));
                case ParticleSystemGradientMode.TwoColors:
                    return new ParticleSystem.MinMaxGradient(
                        ColorValue(source, "minColor", Color.white), ColorValue(source, "maxColor", Color.white));
                case ParticleSystemGradientMode.TwoGradients:
                    return new ParticleSystem.MinMaxGradient(
                        GradientValue(source, "minGradient"), GradientValue(source, "maxGradient"));
                case ParticleSystemGradientMode.RandomColor:
                    var random = new ParticleSystem.MinMaxGradient(GradientValue(source, "maxGradient"));
                    random.mode = ParticleSystemGradientMode.RandomColor;
                    return random;
                default:
                    return new ParticleSystem.MinMaxGradient(Color.white);
            }
        }

        private static AnimationCurve AnimationCurveValue(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = Object(owner, key);
            var keys = new List<Keyframe>();
            foreach (object entry in Array(source, "m_Curve"))
            {
                Dictionary<string, object> item = OpeningJson.Object(entry, "particle curve key");
                var frame = new Keyframe(Float(item, "time"), Float(item, "value"),
                    Float(item, "inSlope"), Float(item, "outSlope"),
                    Float(item, "inWeight", 1f / 3f), Float(item, "outWeight", 1f / 3f));
                frame.weightedMode = (WeightedMode)Integer(item, "weightedMode");
                keys.Add(frame);
            }
            var curve = new AnimationCurve(keys.ToArray())
            {
                preWrapMode = (WrapMode)Integer(source, "m_PreInfinity", (int)WrapMode.Loop),
                postWrapMode = (WrapMode)Integer(source, "m_PostInfinity", (int)WrapMode.Loop),
            };
            return curve;
        }

        private static Gradient GradientValue(Dictionary<string, object> owner, string key)
        {
            Dictionary<string, object> source = Object(owner, key);
            int colorCount = Mathf.Clamp(Integer(source, "m_NumColorKeys", 2), 1, 8);
            int alphaCount = Mathf.Clamp(Integer(source, "m_NumAlphaKeys", 2), 1, 8);
            var colors = new GradientColorKey[colorCount];
            var alphas = new GradientAlphaKey[alphaCount];
            for (int index = 0; index < colorCount; index++)
            {
                string suffix = index.ToString();
                colors[index] = new GradientColorKey(ColorValue(source, "key" + suffix, Color.white),
                    Integer(source, "ctime" + suffix) / 65535f);
            }
            for (int index = 0; index < alphaCount; index++)
            {
                string suffix = index.ToString();
                Color color = ColorValue(source, "key" + suffix, Color.white);
                alphas[index] = new GradientAlphaKey(color.a, Integer(source, "atime" + suffix) / 65535f);
            }
            var gradient = new Gradient
            {
                mode = (GradientMode)Integer(source, "m_Mode"),
            };
            gradient.SetKeys(colors, alphas);
            return gradient;
        }

        private static object[] Array(Dictionary<string, object> owner, string key)
        {
            return owner.TryGetValue(key, out object value) && value is List<object> entries
                ? entries.ToArray() : System.Array.Empty<object>();
        }

        private static Dictionary<string, object> Object(Dictionary<string, object> owner, string key)
        {
            return owner.TryGetValue(key, out object value) && value is Dictionary<string, object> result
                ? result : new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static float Float(Dictionary<string, object> owner, string key, float fallback = 0f)
        {
            return (float)OpeningJson.Number(owner, key, fallback);
        }

        private static int Integer(Dictionary<string, object> owner, string key, int fallback = 0)
        {
            return OpeningJson.Int(owner, key, fallback);
        }

        private static bool Bool(Dictionary<string, object> owner, string key, bool fallback = false)
        {
            return OpeningJson.Bool(owner, key, fallback);
        }

        private static Color ColorValue(Dictionary<string, object> owner, string key, Color fallback)
        {
            Dictionary<string, object> source = Object(owner, key);
            return source.Count == 0 ? fallback : new Color(Float(source, "r"), Float(source, "g"),
                Float(source, "b"), Float(source, "a", 1f));
        }

        private static Vector2 Vector2Value(Dictionary<string, object> owner, string key, Vector2 fallback = default)
        {
            Dictionary<string, object> source = Object(owner, key);
            return source.Count == 0 ? fallback : new Vector2(Float(source, "x"), Float(source, "y"));
        }

        private static Vector3 Vector3Value(Dictionary<string, object> owner, string key, Vector3 fallback = default)
        {
            Dictionary<string, object> source = Object(owner, key);
            return source.Count == 0 ? fallback : new Vector3(Float(source, "x"), Float(source, "y"), Float(source, "z"));
        }
    }
}
