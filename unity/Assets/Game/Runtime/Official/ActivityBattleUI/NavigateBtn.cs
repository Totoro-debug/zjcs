using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;

namespace Zjcs.Official.ActivityBattleUI
{
    // Selected original NavigateBtn. The local map navigation module returns
    // the original result code; the success callback and one-second close stay.
    public sealed class NavigateBtn : MonoBehaviour
    {
        [SerializeField] private string mNavigateKey;
        [SerializeField] private Button mButton;
        [SerializeField] private bool mIsNavigeteCloseWindow;
        private Func<string, CancellationToken, Task<int>> navigate;
        private Action<int> completed;
        private Action<GameObject> close;
        private CancellationTokenSource mCancellationDisposable;
        private readonly CancellationTokenSource destroyed = new CancellationTokenSource();

        public void Configure(Func<string, CancellationToken, Task<int>> operation,
            Action<int> callback, Action<GameObject> closeWindow)
        { navigate = operation; completed = callback; close = closeWindow; }
        private void Awake() { if (mButton) mButton.onClick.AddListener(OnClick); }
        private async void OnClick()
        {
            try
            {
                if (mIsNavigeteCloseWindow) await OnNavigateClose();
                else completed(await navigate(mNavigateKey, CancellationToken.None));
            }
            catch (OperationCanceledException) when (destroyed.IsCancellationRequested
                || mCancellationDisposable?.IsCancellationRequested == true) { }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
        private async Task<bool> OnNavigateClose()
        {
            ClearCancellationDisposable();
            mCancellationDisposable = new CancellationTokenSource();
            int result = await navigate(mNavigateKey, mCancellationDisposable.Token);
            completed(result);
            if (result == 0)
            {
                await WaitCloseDelay();
                close(GetComponentInParent<WorldPopupWindow>().gameObject);
            }
            return result == 0;
        }
        private async Task WaitCloseDelay()
        {
            var ready = new TaskCompletionSource<bool>();
            using (destroyed.Token.Register(() => ready.TrySetCanceled()))
            {
                StartCoroutine(CloseDelay(ready));
                await ready.Task;
            }
        }
        private static IEnumerator CloseDelay(TaskCompletionSource<bool> ready)
        {
            // Original UniTask.Delay(..., ignoreTimeScale: false).
            yield return new WaitForSeconds(1f);
            ready.TrySetResult(true);
        }
        private void OnDestroy()
        {
            if (mButton) mButton.onClick.RemoveListener(OnClick);
            destroyed.Cancel(); destroyed.Dispose();
            ClearCancellationDisposable();
        }
        private void ClearCancellationDisposable()
        { mCancellationDisposable?.Cancel(); mCancellationDisposable?.Dispose(); mCancellationDisposable = null; }
    }
}
