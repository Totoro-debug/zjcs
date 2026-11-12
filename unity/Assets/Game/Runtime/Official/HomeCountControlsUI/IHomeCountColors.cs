// Original quantity control; language/color/input services explicitly bound.
namespace Zjcs.Official.HomeCountControlsUI
{
using Object=UnityEngine.Object;
using Zjcs.Official.WorldUI;
using Zjcs.Official.World.NativeLabelUI.Framework;
using UnityEngine;
public interface IHomeCountColors
{
    Color LightRed { get; }
    string WrapColor(string text, Color color);
}
}
