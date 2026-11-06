using System.Collections.Generic;
using UnityEngine;

public class LinkEffect : MonoBehaviour
{
    public static float StandardLength = 10f;
    public static Vector3 myTargetOffset = new Vector3(StandardLength, 0f, 0f);

    [SerializeField] private float mStartTime;
    [SerializeField] private float mDurationTime = -1f;
    [SerializeField] private List<GameObject> mActiveAtStartList = new List<GameObject>();
    [SerializeField] private GameObject mySourceObj;
    [SerializeField] private GameObject myTargetObj;
    [SerializeField] private List<LinkEffectActiveConditionBase> mActiveConditions =
        new List<LinkEffectActiveConditionBase>();

    private float mTime;
    private float mExecuteTime;

    private void LateUpdate()
    {
        if (myTargetObj == null || mySourceObj == null) return;
        mTime += Time.deltaTime;
        if (mTime < mStartTime) return;
        mExecuteTime += Time.deltaTime;
        if (mDurationTime >= 0f && mExecuteTime > mDurationTime)
        {
            SetLinesActive(false);
            return;
        }

        SetLinesActive(CanActive());
        Vector3 position = mySourceObj.transform.position;
        position.z = 0f;
        transform.position = position;
        Vector3 direction = myTargetObj.transform.position - mySourceObj.transform.position;
        direction.z = 0f;
        if (direction != Vector3.zero)
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(direction.magnitude / StandardLength, 1f, 1f);
    }

    private void OnDisable()
    {
        mTime = 0f;
        mExecuteTime = 0f;
        SetLinesActive(false);
    }

    private void SetLinesActive(bool active)
    {
        if (mActiveAtStartList == null) return;
        foreach (GameObject line in mActiveAtStartList)
            if (line != null) line.SetActive(active);
    }

    private bool CanActive()
    {
        if (mActiveConditions == null) return true;
        foreach (LinkEffectActiveConditionBase condition in mActiveConditions)
            if (condition != null && !condition.CanActive()) return false;
        return true;
    }
}
