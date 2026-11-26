using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.GachaUI
{
    public class AdditionInfoTip : MonoBehaviour
    {
        [SerializeField] private WorldMultiLanguageText mInfoTxt;
        public void ShowInfo(GachaTextKeyArgWrap textData)
        {
            mInfoTxt.SetKey(textData.Key, textData.Args);
            gameObject.SetActive(true);
        }
        public void HideInfo() => gameObject.SetActive(false);
    }
}
