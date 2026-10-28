using System;
using System.Threading;

namespace Zjcs.Official.ProfessionUI
{
    // MessagePipe's consumed token-owner contract: Dispose cancels pending work
    // before releasing its source. No asynchronous callback may outlive a request.
    internal sealed class ProfessionPortraitCancellation : IDisposable
    {
        private readonly CancellationTokenSource source = new CancellationTokenSource();
        public CancellationToken Token => source.Token;
        public void Dispose()
        {
            source.Cancel();
            source.Dispose();
        }
    }
}
