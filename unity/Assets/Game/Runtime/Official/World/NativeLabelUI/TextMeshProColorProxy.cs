// Migrated from selected official managed code; see Map13 label-core source receipt.
namespace Zjcs.Official.World.NativeLabelUI
{
using Object = UnityEngine.Object;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(TextMeshPro))]
public class TextMeshProColorProxy : MonoBehaviour
{
	[SerializeField]
	private TextMeshPro mTextMeshPro;

	[SerializeField]
	private Color mColor;

	[SerializeField]
	private ColorGradientData mGradientData;

	public Color Color
	{
		get
		{

			return mColor;
		}
		set
		{



			mColor = value;
			SetColor(value);
		}
	}

	public void SetColor(Color color)
	{

		((Graphic)mTextMeshPro).color = color.linear;
	}

	public void SetColorGradientData(ColorGradientData colorGradientData)
	{










		((TMP_Text)mTextMeshPro).colorGradient = new VertexGradient
		{
			bottomLeft = colorGradientData.BottomLeft.linear,
			bottomRight = colorGradientData.BottomRight.linear,
			topLeft = colorGradientData.TopLeft.linear,
			topRight = colorGradientData.TopRight.linear
		};
	}
}

}
