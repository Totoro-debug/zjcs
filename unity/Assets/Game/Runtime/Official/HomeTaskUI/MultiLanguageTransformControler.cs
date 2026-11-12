using Zjcs.Official.ProfessionUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomeTaskUI
{
using Object = UnityEngine.Object;
using System;
using UnityEngine;
using UnityEngine.UI;

public class MultiLanguageTransformControler : MonoBehaviour
{
    [Serializable]
    public struct TransformSetting
    {
        public float Width;

        public float Height;

        public float ScaleX;

        public float ScaleY;

        public float ScaleZ;

        public bool Equals(TransformSetting other)
        {
            if (Mathf.Approximately(Width, other.Width) && Mathf.Approximately(Height, other.Height) && Mathf.Approximately(ScaleX, other.ScaleX) && Mathf.Approximately(ScaleY, other.ScaleY))
            {
                return Mathf.Approximately(ScaleZ, other.ScaleZ);
            }
            return false;
        }
    }

    [SerializeField]
    private MonoDictionary<int, TransformSetting> mLanguageType2Transform;

    private RectTransform mRectTransform;

    private TransformSetting? mDefaultTransform;

    public Func<LanguageType, int> GetLanguageGroup { get; set; }

    public ILanguageTypeService LanguageTypeService { get; set; }

    private void Awake()
    {
        if ((Object)(object)mRectTransform == (Object)null)
        {
            mRectTransform = ((Component)this).GetComponent<RectTransform>();
        }
        if ((Object)(object)mRectTransform != (Object)null && !mDefaultTransform.HasValue)
        {
            mDefaultTransform = GetCurrentTransform();
        }
    }

    private void OnEnable()
    {
        LanguageTypeService.OnLanguageChanged += OnLanguageChanged;
        Refresh();
    }

    private void OnDisable()
    {
        if (LanguageTypeService != null)
        {
            LanguageTypeService.OnLanguageChanged -= OnLanguageChanged;
        }
    }

    private void OnLanguageChanged(LanguageType languageType)
    {
        Refresh();
    }

    private TransformSetting GetCurrentTransform()
    {
        //IL_0038: Unknown result type (might be due to invalid IL or missing references)
        //IL_003d: Unknown result type (might be due to invalid IL or missing references)
        //IL_0044: Unknown result type (might be due to invalid IL or missing references)
        //IL_0049: Unknown result type (might be due to invalid IL or missing references)
        //IL_0054: Unknown result type (might be due to invalid IL or missing references)
        //IL_0061: Unknown result type (might be due to invalid IL or missing references)
        //IL_006e: Unknown result type (might be due to invalid IL or missing references)
        //IL_007b: Unknown result type (might be due to invalid IL or missing references)
        //IL_0088: Unknown result type (might be due to invalid IL or missing references)
        if ((Object)(object)mRectTransform == (Object)null)
        {
            mRectTransform = ((Component)this).GetComponent<RectTransform>();
            if ((Object)(object)mRectTransform == (Object)null)
            {
                return default;
            }
        }
        Vector2 sizeDelta = mRectTransform.sizeDelta;
        Vector3 localScale = ((Transform)mRectTransform).localScale;
        return new TransformSetting
        {
            Width = sizeDelta.x,
            Height = sizeDelta.y,
            ScaleX = localScale.x,
            ScaleY = localScale.y,
            ScaleZ = localScale.z
        };
    }

    private void Refresh()
    {
        if (!((Object)(object)mRectTransform == (Object)null) && LanguageTypeService != null)
        {
            if (!mDefaultTransform.HasValue)
            {
                mDefaultTransform = GetCurrentTransform();
            }
            int languageGroupByType = GetLanguageGroup(LanguageTypeService.CurrentLanguage);
            TransformSetting transformSetting = ((mLanguageType2Transform == null || !mLanguageType2Transform.TryGetValue(languageGroupByType, out var value)) ? mDefaultTransform.Value : value);
            if (!GetCurrentTransform().Equals(transformSetting))
            {
                ApplyTransform(transformSetting);
            }
        }
    }

    private void ApplyTransform(TransformSetting s)
    {
        //IL_0021: Unknown result type (might be due to invalid IL or missing references)
        //IL_0043: Unknown result type (might be due to invalid IL or missing references)
        if (!((Object)(object)mRectTransform == (Object)null))
        {
            mRectTransform.sizeDelta = new Vector2(s.Width, s.Height);
            ((Transform)mRectTransform).localScale = new Vector3(s.ScaleX, s.ScaleY, s.ScaleZ);
            LayoutElement component = ((Component)this).GetComponent<LayoutElement>();
            if ((Object)(object)component != (Object)null)
            {
                component.preferredWidth = s.Width;
                component.preferredHeight = s.Height;
            }
        }
    }
}

}
