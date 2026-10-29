using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Character
{
    internal static class OfficialSkillNativeTextStyle
    {
        public static void Apply(TMP_Text text, Dictionary<string, object> bindings)
        {
            if (bindings == null) return;
            if (bindings.TryGetValue("TextMeshProUGUI", out object rawText))
            {
                var fields = CharacterModelPackage.AsObject(rawText, "original TMP vertex style");
                text.enableVertexGradient = Convert.ToBoolean(fields["m_enableVertexGradient"]);
                var colors = CharacterModelPackage.AsObject(fields["m_fontColorGradient"], "original TMP gradient");
                text.colorGradient = new VertexGradient(Color(colors["topLeft"]), Color(colors["topRight"]),
                    Color(colors["bottomLeft"]), Color(colors["bottomRight"]));
            }
            if (!bindings.TryGetValue("TMPEffect", out object rawEffect)) return;
            var effectFields = CharacterModelPackage.AsObject(rawEffect, "original TMP effect");
            var effect = text.gameObject.AddComponent<TMPEffect>();
            if (effectFields.TryGetValue("m_Enabled", out object enabled)) effect.enabled = Convert.ToBoolean(enabled);
            foreach (var field in typeof(TMPEffect).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!effectFields.TryGetValue(field.Name, out object value)) continue;
                field.SetValue(effect, field.FieldType == typeof(Color) ? (object)Color(value)
                    : Convert.ChangeType(value, field.FieldType, CultureInfo.InvariantCulture));
            }
            effect.SetupEffect(effect.enabled);
        }

        // BlessLevelTextViewChanger.Refresh keeps the prefab's gradient and effect widths.
        public static void Bless(RectTransform textNode, bool blessing)
        {
            var text = textNode.GetComponent<TMP_Text>();
            if (text == null) throw new InvalidOperationException("Original blessing level requires TMP text.");
            text.enableVertexGradient = blessing;
            var effect = text.GetComponent<TMPEffect>();
            if (effect == null) return;
            effect.outline = blessing;
            effect.SetupEffect(true);
        }

        private static Color Color(object raw)
        {
            var value = CharacterModelPackage.AsObject(raw, "original text color");
            return new Color(Convert.ToSingle(value["r"], CultureInfo.InvariantCulture),
                Convert.ToSingle(value["g"], CultureInfo.InvariantCulture),
                Convert.ToSingle(value["b"], CultureInfo.InvariantCulture),
                Convert.ToSingle(value["a"], CultureInfo.InvariantCulture));
        }
    }
}
