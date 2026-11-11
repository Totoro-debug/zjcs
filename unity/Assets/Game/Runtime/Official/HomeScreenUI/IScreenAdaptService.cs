// Selected original screen adaptation; local platform/service boundaries only.
using System.Collections.Generic;
namespace Zjcs.Official.HomeScreenUI
{

public interface IScreenAdaptService
{
	IEnumerable<CanvasWidthAdjuster> CanvasWidthAdjusters { get; }

	void PauseShowBorder();

	void ResumeShowBorder();

	void RegisterCanvasWidthAdjuster(CanvasWidthAdjuster adjuster);

	void UnregisterCanvasWidthAdjuster(CanvasWidthAdjuster adjuster);
}

}
