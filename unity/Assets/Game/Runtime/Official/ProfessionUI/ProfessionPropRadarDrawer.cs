using UnityEngine;
using UnityEngine.UI;

// Source: official client ProfessionPropRadarDrawer; namespace and IL cast syntax adapted.
namespace Zjcs.Official.ProfessionUI
{
[RequireComponent(typeof(CanvasRenderer))]
public class ProfessionPropRadarDrawer : Graphic
{
	private Vector2[] points = new Vector2[4];

	public void DrawRadar(float[] values)
	{
		Rect rect = ((Graphic)this).rectTransform.rect;
		float num = rect.height / 2f * values[0];
		float num2 = rect.width / 2f * values[1];
		float num3 = rect.height / 2f * values[2];
		float num4 = rect.width / 2f * values[3];
		points[0] = new Vector2(0f, num);
		points[1] = new Vector2(num2, 0f);
		points[2] = new Vector2(0f, 0f - num3);
		points[3] = new Vector2(0f - num4, 0f);
		((Graphic)this).SetVerticesDirty();
	}

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		if (points.Length == 4)
		{
			vh.Clear();
			UIVertex simpleVert = UIVertex.simpleVert;
			simpleVert.color = ((Graphic)this).color;
			Vector2[] array = points;
			foreach (Vector2 val in array)
			{
				simpleVert.position = val;
				vh.AddVert(simpleVert);
			}
			vh.AddTriangle(0, 1, 2);
			vh.AddTriangle(2, 3, 0);
		}
	}
}
}
