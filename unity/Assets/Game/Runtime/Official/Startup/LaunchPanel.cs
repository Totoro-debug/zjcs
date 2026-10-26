using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Official.Startup
{
    // Official Launch/LaunchPanel progress consumers. MessagePipe/online update
    // subscriptions are supplied by the offline loader; localized TMP content
    // replaces MultiLanguageText.SetKey("{0}", text) after APK text extraction.
    [DefaultExecutionOrder(-1)]
    public sealed class LaunchPanel : MonoBehaviour
    {
        [SerializeField] private Slider mProgressSlider;
        [SerializeField] private TMP_Text mStatusText;
        [SerializeField] private Button mClientRepairBtn;

        public Slider ProgressSlider => mProgressSlider;
        public TMP_Text StatusText => mStatusText;
        public void OnProgress(string text, float progress)
        {
            mStatusText.text = text;
            mProgressSlider.value = progress;
        }
        public void SetResourceUpdateState(bool updatingOrDone) => mClientRepairBtn.gameObject.SetActive(!updatingOrDone);
    }
}
