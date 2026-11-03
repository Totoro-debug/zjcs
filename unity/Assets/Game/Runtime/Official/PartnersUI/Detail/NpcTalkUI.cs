// Migrated original detail presentation; see partner-detail-talk-source-manifest.json.
namespace Zjcs.Official.PartnersUI
{
using Zjcs.Official.WorldUI;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryBubbles;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using Object = UnityEngine.Object;
using System.Collections;
using UnityEngine;

public class NpcTalkUI : MonoBehaviour
{
    public enum NpcTalkState
    {
        Start,
        Talking,
        End
    }

    [SerializeField]
    private MultiLanguageText mNpcTalkText;

    [SerializeField]
    private TMP_TypeWriterEffect mNpcTalkEffect;

    [SerializeField]
    private WorldStateSwitcher<bool> mShowStateSwitcher;

    [SerializeField]
    private WorldStateSwitcher<NpcTalkState> mNpcTalkStateSwitcher;

    [Header("对话文本显示完之后的延迟关闭时间")]
    [SerializeField]
    private float mDelayCloseTime;

    private Coroutine mCoroutine;

    private WaitForSeconds mWaitForSeconds;

    private void Awake()
    {
        mWaitForSeconds = new WaitForSeconds(mDelayCloseTime);
    }

    private void OnDisable()
    {
        StopCoroutine();
    }

    public void SetStateSwitcher(bool value)
    {
        mShowStateSwitcher.SetState(value);
    }

    public void ShowNpcTalk(string key)
    {
        StopCoroutine();
        mNpcTalkText.SetKey(key);
        mShowStateSwitcher.SetState(state: true);
        mNpcTalkStateSwitcher.SetState(NpcTalkState.Start);
        mNpcTalkEffect.StartType(NpcTalkEnd);
    }

    private void StopCoroutine()
    {
        if (mCoroutine != null)
        {
            ((MonoBehaviour)this).StopCoroutine(mCoroutine);
            mCoroutine = null;
        }
    }

    private void NpcTalkEnd(bool isBreakOff)
    {
        if (!(!((Component)this).gameObject.activeInHierarchy | isBreakOff))
        {
            mCoroutine = ((MonoBehaviour)this).StartCoroutine(DelayCloseTalkGo());
        }
    }

    private IEnumerator DelayCloseTalkGo()
    {
        yield return mWaitForSeconds;
        mNpcTalkStateSwitcher.SetState(NpcTalkState.End);
    }
}

}
