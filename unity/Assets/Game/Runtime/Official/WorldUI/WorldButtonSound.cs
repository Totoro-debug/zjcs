using System;
using UnityEngine;
using UnityEngine.UI;
namespace Zjcs.Official.WorldUI
{
    // framework-firstpass-source/ButtonSound.cs: resolve Event to its verified local clip.
    [RequireComponent(typeof(Button))]
    public sealed class WorldButtonSound : MonoBehaviour
    {
        public AudioClip ClickEvent;
        [NonSerialized] public Action<AudioClip, GameObject> Post;
        private Button mBtn;
        private void Start() { mBtn = GetComponent<Button>(); mBtn.onClick.AddListener(OnBtnClick); }
        private void OnBtnClick() { if (ClickEvent != null) Post?.Invoke(ClickEvent, gameObject); }
    }
}
