namespace Zjcs.Official.HomeHangUpWindowsUI
{
    // Optional local data boundary for earned activity income. The native
    // window retains its hierarchy, controls, formatting and progress image.
    public interface IHomeIncomeWindowPreview
    {
        bool TryGetIncomePreview(int hangUpId, out long count, out long capacity);
    }
}
