using System;
using UnityEngine;

[Serializable]
public class AfterimageData
{
    public float RecordTime;
    public float FadeInTime;
    public float FadeCompleteTime;
    public float FadeOutTime;
    public float EndTime;
    public Color Color = Color.white;
    [NonSerialized] public RenderTexture RTHandle;
    [NonSerialized] public RTRenderer RTRenderer;

    public Color GetColor(float time)
    {
        Color color = Color;
        if (time <= FadeInTime) color.a = 0f;
        else if (time <= FadeCompleteTime)
            color.a = Color.a * (time - FadeInTime) / (FadeCompleteTime - FadeInTime);
        else if (time <= FadeOutTime) { }
        else if (time <= EndTime)
            color.a = Color.a * (EndTime - time) / (EndTime - FadeOutTime);
        else color.a = 0f;
        return color;
    }
}
