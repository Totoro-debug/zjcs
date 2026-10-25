// Migrated from the pinned official Framework assembly; decompiler casts and namespace syntax are adapted.
namespace UGUIJszx
{
    public class VerticalLayoutGroupJSZX : HorizontalOrVerticalLayoutGroupJSZX
    {
        public override void CalculateLayoutInputHorizontal()
        {
            base.CalculateLayoutInputHorizontal();
            CalcAlongAxis(0, isVertical: true);
        }

        public override void CalculateLayoutInputVertical() => CalcAlongAxis(1, isVertical: true);
        public override void SetLayoutHorizontal() => SetChildrenAlongAxis(0, isVertical: true);
        public override void SetLayoutVertical() => SetChildrenAlongAxis(1, isVertical: true);
    }
}
