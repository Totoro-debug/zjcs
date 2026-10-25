using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.WorldUI
{
    // ItemIcon.Clear / Init / UpdateQualityImage / SetCount. Dynamic assets are
    // resolved locally; all transforms and references come from ItemIconDefault.
    public sealed class WorldItemIcon : MonoBehaviour
    {
        public Image Icon, QualityImg, QualityShadowImg;
        public TMP_Text myText;
        public TMP_Text myBottomText;
        public GameObject myTextGo;
        public GameObject BottomGo;
        public GameObject[] ClearObjects;
        public Transform BgTransform;

        public void Init(Sprite icon, Sprite quality, string count)
        {
            foreach (GameObject child in ClearObjects) if (child != null) child.SetActive(false);
            foreach (Transform child in BgTransform) Destroy(child.gameObject);
            QualityImg.gameObject.SetActive(true);
            if (QualityShadowImg != null) QualityShadowImg.gameObject.SetActive(true);
            QualityImg.sprite = quality;
            Icon.gameObject.SetActive(true);
            Icon.sprite = icon;
            myTextGo.SetActive(true);
            myText.text = count;
        }

        public void SetBottomName(string name)
        {
            if (BottomGo != null) BottomGo.SetActive(true);
            if (myBottomText != null) myBottomText.text = name;
        }
    }
}
