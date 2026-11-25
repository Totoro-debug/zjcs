using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.CharacterCreation
{
    // Native adaptation of EditCustomColorBlock.GenerateTexture in the pinned client.
    internal sealed class CharacterCreationHsvGradient : MonoBehaviour
    {
        private readonly Texture2D[] textures = new Texture2D[3];
        private readonly Color[][] pixels = new Color[3][];
        private readonly Vector3[] textureInputs = new Vector3[3];
        private readonly Slider[] sliders = new Slider[3];
        private readonly RawImage[] images = new RawImage[3];
        private Image previewSwatch;

        public void Refresh()
        {
            if (!ResolveControls()) return;
            for (int channel = 0; channel < 3; channel++)
            {
                Slider slider = sliders[channel];
                RawImage image = images[channel];
                if (image == null) continue;
                int width = (int)(slider.maxValue - slider.minValue);
                if (width <= 0) continue;
                // The original client uses HSlider.minValue for the S gradient.
                float minimum = channel == 2 ? slider.minValue : sliders[0].minValue;
                float hue = channel == 1 ? sliders[0].value : 0f;
                var inputs = new Vector3(width, minimum, hue);
                Texture2D texture = textures[channel];
                if (texture != null && textureInputs[channel].Equals(inputs)) continue;
                if (texture == null)
                {
                    texture = textures[channel] = new Texture2D(width, 1);
                    texture.wrapMode = TextureWrapMode.Clamp;
                    image.texture = texture;
                }
                else if (texture.width != width)
                    texture.Reinitialize(width, 1);
                if (pixels[channel] == null || pixels[channel].Length != width)
                    pixels[channel] = new Color[width];
                Color[] colors = pixels[channel];
                for (int i = 0; i < width; i++)
                {
                    if (channel == 0) colors[i] = Color.HSVToRGB((minimum + i) / 360f, 1, 1);
                    else if (channel == 1) colors[i] = Color.HSVToRGB(hue / 360f, (minimum + i) / 100f, 1);
                    else colors[i] = Color.HSVToRGB(0, 0, (minimum + i) / 100f);
                }
                texture.SetPixels(colors);
                texture.Apply();
                textureInputs[channel] = inputs;
            }
            if (previewSwatch != null)
                previewSwatch.color = Color.HSVToRGB(sliders[0].value / 360f, sliders[1].value / 100f, sliders[2].value / 100f);
        }

        private bool ResolveControls()
        {
            if (sliders[0] != null && sliders[1] != null && sliders[2] != null) return true;
            foreach (Slider slider in GetComponentsInChildren<Slider>(true))
            {
                switch (slider.name)
                {
                    case "HSlider": sliders[0] = slider; break;
                    case "SSlider": sliders[1] = slider; break;
                    case "VSlider": sliders[2] = slider; break;
                }
            }
            if (sliders[0] == null || sliders[1] == null || sliders[2] == null) return false;
            for (int channel = 0; channel < sliders.Length; channel++)
                images[channel] = sliders[channel].GetComponentInChildren<RawImage>(true);
            foreach (Image swatch in GetComponentsInChildren<Image>(true))
                if (swatch.name == "Color" && swatch.transform.parent.name == "CloseCustomColorBtn")
                    previewSwatch = swatch;
            return true;
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        }
    }
}
