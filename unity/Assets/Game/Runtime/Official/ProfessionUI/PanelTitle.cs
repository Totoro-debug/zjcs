using UnityEngine;
using Zjcs.Official.WorldUI;
namespace Zjcs.Official.ProfessionUI
{
    public sealed class PanelTitle : MonoBehaviour
    {
        public WorldMultiLanguageText TitleTxt;
        public void SetKey(string key) => TitleTxt.SetKey(key);
        public void SetKey(string key, params object[] args) => TitleTxt.SetKey(key, args);
    }
}
