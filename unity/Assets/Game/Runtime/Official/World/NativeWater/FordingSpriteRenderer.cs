// Migrated from selected official CustomWater.dll; see Map13 source receipt.
namespace Zjcs.Official.World.NativeWater
{
using UnityEngine;
using UnityEngine.Sprites;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class FordingSpriteRenderer : MonoBehaviour
{
	public Sprite Sprite;

	public Color Color = Color.white;

	[Min(0f)]
	public float FordingScale = 1f;

	public float FordingOffset;

	private Mesh mMesh;

	private MaterialPropertyBlock mMPB;

	private MeshRenderer mRenderer;

	private static readonly Vector3[] sPositions = new Vector3[4];

	private static readonly Vector2[] sUVs = new Vector2[4];

	private static readonly Color[] sColors = new Color[4];

	private static readonly int[] sTriangles = new int[6];

	private static readonly Vector3[] sFording = new Vector3[4];

	private MaterialPropertyBlock MPB
	{
		get
		{
			if (mMPB == null)
			{
				mMPB = new MaterialPropertyBlock();
			}
			return mMPB;
		}
	}

	private void Awake()
	{
		mRenderer = GetComponent<MeshRenderer>();
		mMesh = new Mesh();
		mMesh.name = "WaterSprite";
		mMesh.hideFlags = HideFlags.DontSave;
		GetComponent<MeshFilter>().mesh = mMesh;
	}

	private void OnDestroy()
	{
		if (Application.isPlaying)
		{
			Object.Destroy(mMesh);
		}
		else
		{
			Object.DestroyImmediate(mMesh);
		}
		mMesh = null;
	}

	private void OnEnable()
	{
		if (transform.hasChanged)
		{
			GenerateMesh();
			transform.hasChanged = false;
		}
		SetMPB();
	}

	private void GenerateMesh()
	{
		if (mMesh != null)
		{
			mMesh.Clear();
		}
		if (!(Sprite == null))
		{
			Vector4 vector = ((Sprite == null) ? Vector4.zero : DataUtility.GetPadding(Sprite));
			Vector4 vector2 = ((Sprite != null) ? DataUtility.GetOuterUV(Sprite) : Vector4.zero);
			float pixelsPerUnit = Sprite.pixelsPerUnit;
			Rect rect = Sprite.rect;
			Vector4 vector3 = new Vector4(rect.x + vector.x, rect.y + vector.y, rect.width - vector.z, rect.height - vector.w) / pixelsPerUnit;
			Vector2 vector4 = (Sprite.pivot - rect.position) / pixelsPerUnit;
			sPositions[0] = new Vector3(vector3.x - vector4.x, vector3.y - vector4.y, 0f);
			sPositions[1] = new Vector3(vector3.x - vector4.x, vector3.w - vector4.y, 0f);
			sPositions[2] = new Vector3(vector3.z - vector4.x, vector3.w - vector4.y, 0f);
			sPositions[3] = new Vector3(vector3.z - vector4.x, vector3.y - vector4.y, 0f);
			mMesh.vertices = sPositions;
			sUVs[0] = new Vector2(vector2.x, vector2.y);
			sUVs[1] = new Vector2(vector2.x, vector2.w);
			sUVs[2] = new Vector2(vector2.z, vector2.w);
			sUVs[3] = new Vector2(vector2.z, vector2.y);
			mMesh.uv = sUVs;
			sColors[0] = Color;
			sColors[1] = Color;
			sColors[2] = Color;
			sColors[3] = Color;
			mMesh.colors = sColors;
			Vector3 lossyScale = transform.lossyScale;
			sFording[0] = new Vector3(sPositions[0].y * lossyScale.y, FordingScale, FordingOffset);
			sFording[1] = new Vector3(sPositions[1].y * lossyScale.y, FordingScale, FordingOffset);
			sFording[2] = new Vector3(sPositions[2].y * lossyScale.y, FordingScale, FordingOffset);
			sFording[3] = new Vector3(sPositions[3].y * lossyScale.y, FordingScale, FordingOffset);
			mMesh.SetUVs(1, sFording);
			sTriangles[0] = 0;
			sTriangles[1] = 1;
			sTriangles[2] = 2;
			sTriangles[3] = 2;
			sTriangles[4] = 3;
			sTriangles[5] = 0;
			mMesh.triangles = sTriangles;
		}
	}

	private void SetMPB()
	{
		Texture2D texture2D = null;
		if (Sprite != null)
		{
			texture2D = Sprite.texture;
		}
		if (texture2D != null)
		{
			MPB.SetTexture("_MainTex", texture2D);
			mRenderer.SetPropertyBlock(MPB);
		}
	}

	private void OnDidApplyAnimationProperties()
	{
		GenerateMesh();
	}

	[ContextMenu("Refresh")]
	public void Refresh()
	{
		GenerateMesh();
		SetMPB();
	}
}

}
