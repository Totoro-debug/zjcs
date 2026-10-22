using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.CharacterCreation
{
    // Native adaptation of EditCustomColorBlock.GenerateTexture in the pinned client.
    internal sealed class CharacterCreationHsvGradient : MonoBehaviour
    {
        private readonly Texture2D[] textures = new Texture2D[3];

        public void Refresh()
        {
            Slider[] sliders = new Slider[3];
            string[] names = { "HSlider", "SSlider", "VSlider" };
            for (int channel = 0; channel < 3; channel++)
                foreach (Slider slider in GetComponentsInChildren<Slider>(true))
                    if (slider.name == names[channel]) sliders[channel] = slider;
            if (sliders[0] == null || sliders[1] == null || sliders[2] == null) return;
            for (int channel = 0; channel < 3; channel++)
            {
                Slider slider = sliders[channel];
                RawImage image = slider.GetComponentInChildren<RawImage>(true);
                if (image == null) continue;
                int width = (int)(slider.maxValue - slider.minValue);
                if (width <= 0) continue;
                if (textures[channel] == null) textures[channel] = new Texture2D(width, 1);
                else if (textures[channel].width != width) textures[channel].Reinitialize(width, 1);
                Texture2D texture = textures[channel];
                var pixels = new Color[width];
                for (int i = 0; i < width; i++)
                {
                    if (channel == 0) pixels[i] = Color.HSVToRGB((sliders[0].minValue + i) / 360f, 1, 1);
                    // The original client uses HSlider.minValue for the S gradient.
                    else if (channel == 1) pixels[i] = Color.HSVToRGB(sliders[0].value / 360f, (sliders[0].minValue + i) / 100f, 1);
                    else pixels[i] = Color.HSVToRGB(0, 0, (sliders[2].minValue + i) / 100f);
                }
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.SetPixels(pixels); texture.Apply(); image.texture = texture;
            }
            foreach (Image swatch in GetComponentsInChildren<Image>(true))
                if (swatch.name == "Color" && swatch.transform.parent.name == "CloseCustomColorBtn")
                    swatch.color = Color.HSVToRGB(sliders[0].value / 360f, sliders[1].value / 100f, sliders[2].value / 100f);
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in textures) if (texture != null) Destroy(texture);
        }
    }
}
