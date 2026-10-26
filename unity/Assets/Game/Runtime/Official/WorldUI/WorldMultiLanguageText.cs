using System;
using System.Text;
using TMPro;
using UnityEngine;

namespace Zjcs.Official.WorldUI
{
    public interface IWorldTextProcessor { string Process(string text); }

    /// <summary>Original MultiLanguageText key/format/processor behavior. The offline host supplies
    /// localization and font services; its typography-effect service is migrated separately.</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class WorldMultiLanguageText : MonoBehaviour
    {
        public string myKey;
        private object[] mArgs;
        private IWorldTextProcessor mRawKeyValueProcessor, mLocalizedTextProcessor;
        private TMP_Text mCacheText;
        public static Func<string, string> Localize { get; set; }
        public static Func<TMP_FontAsset> LanguageFont { get; set; }
        private TMP_Text CacheText => mCacheText != null ? mCacheText : (mCacheText = GetComponent<TMP_Text>());
        public string Text => CacheText.text;
        public Color Color { get => CacheText.color; set => CacheText.color = value; }
        public float FontSize { get => CacheText.fontSize; set => CacheText.fontSize = value; }
        public RectTransform RectTransform => CacheText.rectTransform;
        public event Action OnTextRefresh;
        public event Action OnFontChanged;

        private void Start() => Refresh();
        public void SetKey(string key) => SetKey(key, null, null, null);
        public void SetKey(string key, params object[] args) => SetKey(key, null, null, args);
        public void SetKey(string key, IWorldTextProcessor rawKeyValueProcessor, IWorldTextProcessor localizedTextProcessor, params object[] args)
        {
            CacheText.text = string.Empty;
            myKey = key;
            mArgs = args;
            mRawKeyValueProcessor = rawKeyValueProcessor;
            mLocalizedTextProcessor = localizedTextProcessor;
            Refresh();
        }
        public void Format(params object[] args) => Format(null, null, args);
        public void Format(IWorldTextProcessor rawKeyValueProcessor, IWorldTextProcessor localizedTextProcessor, params object[] args)
        {
            mArgs = args;
            mRawKeyValueProcessor = rawKeyValueProcessor;
            mLocalizedTextProcessor = localizedTextProcessor;
            Refresh();
        }
        public void Refresh()
        {
            TMP_FontAsset font = LanguageFont?.Invoke();
            if (font != null && CacheText.font != font)
            {
                CacheText.font = font;
                OnFontChanged?.Invoke();
            }
            if (string.IsNullOrEmpty(myKey)) return;
            string text = Localize != null ? Localize(myKey) : myKey;
            if (mArgs == null || mArgs.Length == 0) SetLanguageText(GetRawKeyValueProcessed(text));
            else
            {
                try { SetLanguageText(string.Format(GetRawKeyValueProcessed(text), mArgs)); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
            OnTextRefresh?.Invoke();
        }
        public static string GetKeyBySplit(int count, string splitKey)
        {
            if (count <= 0) return null;
            var builder = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) builder.Append(splitKey);
                builder.Append("{").Append(i).Append("}");
            }
            return builder.ToString();
        }
        private void SetLanguageText(string text)
        {
            if (mLocalizedTextProcessor != null) text = mLocalizedTextProcessor.Process(text);
            CacheText.text = text;
        }
        private string GetRawKeyValueProcessed(string text) => string.IsNullOrEmpty(text) || mRawKeyValueProcessor == null
            ? text : mRawKeyValueProcessor.Process(text);
    }
}
