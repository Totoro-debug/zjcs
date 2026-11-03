using System;
using UnityEngine;
using Zjcs.Official.StoryPlayables;

[MyPlayableApply(typeof(AppearanceCombineSkin))]
public class MyPlayableAppearanceCombineSkin : MyPlayable
{
    public enum OperateType
    {
        Remove,
        Add
    }

    [Serializable]
    public class SkinOperate
    {
        public string SkinName;
        public OperateType OperateType;
    }

    public SkinOperate[] SkinOperates;
    [Tooltip("持续时间，单位：秒，-1表示无限持续")]
    public float Duration;

    private AppearanceCombineSkin appearanceCombine;

    protected override void PlayImpl()
    {
        base.PlayImpl();
        appearanceCombine = myApplyComponent as AppearanceCombineSkin;
        if (appearanceCombine == null)
        {
            Debug.LogError("MyPlayableAppearanceCombineSkin requires AppearanceCombineSkin.", this);
            return;
        }

        foreach (SkinOperate operation in SkinOperates)
            HandleSkinOperation(operation);
    }

    protected override void StopImpl()
    {
        appearanceCombine = null;
        base.StopImpl();
    }

    public override float GetDuration() => Duration;

    private void HandleSkinOperation(SkinOperate operation)
    {
        if (appearanceCombine == null) return;
        if (operation.OperateType == OperateType.Remove)
            appearanceCombine.RemoveSkin(operation.SkinName);
        else
            appearanceCombine.AddSkin(operation.SkinName);
    }
}
