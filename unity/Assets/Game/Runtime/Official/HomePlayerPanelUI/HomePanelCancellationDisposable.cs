// Selected original player-panel implementation; typed service boundaries only.
using System;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.WorldUI;
using Zjcs.Official.HomeHudUI;
using Zjcs.Official.HomePlayerInfoUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
namespace Zjcs.Official.HomePlayerPanelUI
{
using Object = UnityEngine.Object;
using MultiLanguageText = Zjcs.Official.WorldUI.WorldMultiLanguageText;
using System.Threading;
public sealed class HomePanelCancellationDisposable : IDisposable
{
    readonly CancellationTokenSource cancellationTokenSource;
    public HomePanelCancellationDisposable(CancellationTokenSource cancellationTokenSource) { this.cancellationTokenSource=cancellationTokenSource; }
    public HomePanelCancellationDisposable()
        : this(new CancellationTokenSource())
    {
    }

    public CancellationToken Token => cancellationTokenSource.Token;

    public bool IsDisposed => cancellationTokenSource.IsCancellationRequested;

    public void Dispose() => cancellationTokenSource.Cancel();
}
}
