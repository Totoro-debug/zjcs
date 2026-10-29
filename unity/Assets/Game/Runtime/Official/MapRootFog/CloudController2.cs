// Complete original application class; platform services and consumed native types only.
using System.Collections.Generic;
using Zjcs.Official.World.NativeTerrain.Common;
using Object = UnityEngine.Object;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Sprites;

namespace Zjcs.Official.MapRootFog
{
[DefaultExecutionOrder(101)]
public class CloudController2 : MonoBehaviour
{
	public struct DisappearData
	{
		public Vector2 Center;

		public float StartTime;

		public float EndTime;
	}

	public IRootFogPlatform Platform {get;set;}

	public SpriteRenderer CloudTemplate;

	[Header("消失速度")]
	public float DiffusionSpeed = 3f;

	public Material DisappearMat;

	public Material CloudMat;

	public MeshRenderer Cloud;

	public Material CloudMaskMat;

	private FogGroundData mGridGround;

	private bool mInitialized;

	private HashSet<Pos2d> mClouds;

	private readonly Dictionary<Pos2d, DisappearData> mFadingClouds = new Dictionary<Pos2d, DisappearData>();

	private readonly List<Pos2d> mPending2Remove = new List<Pos2d>();

	private RenderTexture mRenderTexture;

	private Camera mMainCamera;

	private bool mCloudDirty;

	private Vector3 mLastCamPos;

	private float mLastCamOrthographicSize;

	private CommandBuffer mCommandBuffer;

	private Mesh mCloudMesh;

	private readonly List<Vector3> mCloudVertices = new List<Vector3>();

	private Mesh mMaskMesh;

	private readonly List<Vector3> mNormalVertices = new List<Vector3>();

	private Mesh mFadingMesh;

	private readonly List<Vector3> mFadingVertices = new List<Vector3>();

	private readonly List<Vector3> mFadingData = new List<Vector3>();

	private readonly List<Vector2> mUVs = new List<Vector2>();

	private readonly List<ushort> mTriangles = new List<ushort>();

	private Vector4 mBaseMaskQuad;

	private Vector4 mBaseMaskUV;

	private bool mLastClear;

	private readonly HashSet<Pos2d> mVisited = new HashSet<Pos2d>();

	private readonly Vector2Int[] mVicinities = new Vector2Int[9]
	{
		new Vector2Int(0, 0),
		new Vector2Int(-1, 0),
		new Vector2Int(1, 0),
		new Vector2Int(0, -1),
		new Vector2Int(0, 1),
		new Vector2Int(-1, -1),
		new Vector2Int(-1, 1),
		new Vector2Int(1, -1),
		new Vector2Int(1, 1)
	};

	private void Awake()
	{
		mCommandBuffer = new CommandBuffer();
		mCommandBuffer.name = "CloudMaskRendering";
		mCloudMesh = new Mesh
		{
			name = "CloudMesh",
			hideFlags = (HideFlags)52
		};
		mCloudMesh.MarkDynamic();
		mMaskMesh = new Mesh
		{
			name = "CloudMask",
			hideFlags = (HideFlags)52
		};
		mMaskMesh.MarkDynamic();
		mFadingMesh = new Mesh
		{
			name = "CloudFadingMask",
			hideFlags = (HideFlags)52
		};
		mFadingMesh.MarkDynamic();
		((Component)Cloud).GetComponent<MeshFilter>().sharedMesh = mCloudMesh;
		CalcBaseQuad();
		mMainCamera = Platform.MapCamera;
		float num = 1f;
		RenderPipelineAsset currentRenderPipeline = GraphicsSettings.currentRenderPipeline;
		UniversalRenderPipelineAsset val = (UniversalRenderPipelineAsset)(object)((currentRenderPipeline is UniversalRenderPipelineAsset) ? currentRenderPipeline : null);
		if ((Object)(object)val != (Object)null)
		{
			num = val.renderScale;
		}
		int num2 = (int)((double)((float)Screen.width * num) * 0.5);
		int num3 = (int)((double)((float)Screen.height * num) * 0.5);
		mRenderTexture = RenderTexture.GetTemporary(num2, num3, 0, (RenderTextureFormat)16);
		((Object)mRenderTexture).name = "CloudRT";
		((Renderer)Cloud).sharedMaterial.SetTexture("_MaskTex", (Texture)(object)mRenderTexture);
	}

	private void OnDestroy()
	{
		if ((Object)(object)mRenderTexture != (Object)null)
		{
			RenderTexture.ReleaseTemporary(mRenderTexture);
			mRenderTexture = null;
		}
		CommandBuffer val = mCommandBuffer;
		if (val != null)
		{
			val.Dispose();
		}
		Object.Destroy((Object)(object)mCloudMesh);
		mCloudMesh = null;
		Object.Destroy((Object)(object)mMaskMesh);
		mMaskMesh = null;
		Object.Destroy((Object)(object)mFadingMesh);
		mFadingMesh = null;
	}

	private void CalcBaseQuad()
	{
		Sprite sprite = CloudTemplate.sprite;
		Vector2 val = new Vector2(0.8f, 0.8f);
		float pixelsPerUnit = sprite.pixelsPerUnit;
		Rect rect = sprite.rect;
		Vector4 val2 = (((Object)(object)sprite == (Object)null) ? Vector4.zero : DataUtility.GetPadding(sprite));
		mBaseMaskUV = (((Object)(object)sprite != (Object)null) ? DataUtility.GetOuterUV(sprite) : Vector4.zero);
		Vector4 val3 = new Vector4(rect.x + val2.x, rect.y + val2.y, rect.width - val2.z, rect.height - val2.w) / pixelsPerUnit;
		Vector2 val4 = (sprite.pivot - rect.position) / pixelsPerUnit;
		mBaseMaskQuad = new Vector4((val3.x - val4.x) * val.x, (val3.y - val4.y) * val.y, (val3.z - val4.x) * val.x, (val3.w - val4.y) * val.y);
	}

	public void Initialize(FogVisibilityData gridVisAgentComp, FogGroundData gridGround)
	{
		DisappearMat.SetFloat("_DiffusionSpeed", DiffusionSpeed);
		((Component)Cloud).gameObject.SetActive(true);
		mGridGround = gridGround;
		int x = gridGround.Info.Size.X;
		int y = gridGround.Info.Size.Y;
		mClouds = new HashSet<Pos2d>(x * y / 4);
		Pos2d item = new Pos2d(0, 0);
		for (int i = 0; i < x; i++)
		{
			for (int j = 0; j < y; j++)
			{
				if (!gridVisAgentComp.GetPosVisibility(i, j))
				{
					item.X = i;
					item.Y = j;
					mClouds.Add(item);
				}
			}
		}
		PopulateCornerMask(x, y);
		SetupMat();
		mInitialized = true;
	}

	private void SetupMat()
	{
		Material sharedMaterial = Object.Instantiate<Material>(CloudMat);
		((Renderer)Cloud).sharedMaterial = sharedMaterial;
		((Renderer)Cloud).sharedMaterial.SetTexture("_MaskTex", (Texture)(object)mRenderTexture);
		Transform transform = ((Component)Cloud).transform;
		transform.localPosition = Vector3.zero;
		transform.localScale = Vector3.one;
	}

	private void PopulateCornerMask(int sizeX, int sizeY)
	{
		int num = 2;
		int i = -num;
		for (int num2 = sizeX + num; i < num2; i++)
		{
			for (int j = 0; j < num; j++)
			{
				mClouds.Add(new Pos2d(i, sizeY + j));
				mClouds.Add(new Pos2d(i, -1 - j));
			}
		}
		for (int k = 0; k < sizeY; k++)
		{
			for (int l = 0; l < num; l++)
			{
				mClouds.Add(new Pos2d(-1 - l, k));
				mClouds.Add(new Pos2d(sizeX + l, k));
			}
		}
	}

	public void Reveal(Pos2d pos, Pos2d playerPos)
	{
		if (mClouds.Contains(pos))
		{
			float time = Time.time;
			DisappearData value = new DisappearData
			{
				Center = (Vector2)mGridGround.GridToPos(playerPos.X, playerPos.Y),
				StartTime = time,
				EndTime = time + (float)Pos2d.Distance(pos, playerPos) / DiffusionSpeed + 0.2f
			};
			mFadingClouds.Add(pos, value);
			mClouds.Remove(pos);
		}
	}

	public void Remove(Pos2d pos)
	{
		mCloudDirty = true;
		mClouds.Remove(pos);
		mFadingClouds.Remove(pos);
	}

	public void Add(Pos2d pos)
	{
		mCloudDirty = true;
		mFadingClouds.Remove(pos);
		mClouds.Add(pos);
	}

	public bool Contains(Pos2d pos)
	{
		if (!mClouds.Contains(pos))
		{
			return mFadingClouds.ContainsKey(pos);
		}
		return true;
	}

	private void Update()
	{
		if (!mInitialized)
		{
			return;
		}
		float time = Time.time;
		mPending2Remove.Clear();
		foreach (KeyValuePair<Pos2d, DisappearData> mFadingCloud in mFadingClouds)
		{
			mCloudDirty = true;
			if (mFadingCloud.Value.EndTime < time)
			{
				mPending2Remove.Add(mFadingCloud.Key);
			}
		}
		int i = 0;
		for (int count = mPending2Remove.Count; i < count; i++)
		{
			mFadingClouds.Remove(mPending2Remove[i]);
		}
	}

	private void LateUpdate()
	{
		if (!Platform.IsMapScene)
		{
			return;
		}
		if (mInitialized)
		{
			Vector3 localPosition = ((Component)mMainCamera).transform.localPosition;
			float orthographicSize = mMainCamera.orthographicSize;
			bool num = !(localPosition == mLastCamPos) || orthographicSize != mLastCamOrthographicSize || mCloudDirty;
			mLastCamPos = localPosition;
			mLastCamOrthographicSize = orthographicSize;
			if (!num)
			{
				return;
			}
			mCloudDirty = false;
		}
		ShowCloudWithinView();
	}

	private void ShowCloudWithinView()
	{
		if (!mInitialized)
		{
			return;
		}
		mMaskMesh.Clear();
		mNormalVertices.Clear();
		mFadingMesh.Clear();
		mFadingVertices.Clear();
		mFadingData.Clear();
		mCloudMesh.Clear();
		mCloudVertices.Clear();
		mVisited.Clear();
		Camera val = mMainCamera;
		float orthographicSize = val.orthographicSize;
		Vector3 position = ((Component)val).transform.position;
		Vector3 val2 = new Vector3(val.aspect * orthographicSize, orthographicSize, 0f);
		Vector3 pos = position - val2;
		Vector3 pos2 = position + val2;
		FogGroundData gridGroundComponent = mGridGround;
		Pos2d pos2d = gridGroundComponent.PosToGrid(pos);
		pos2d.X--;
		pos2d.Y--;
		Pos2d pos2d2 = gridGroundComponent.PosToGrid(pos2);
		pos2d2.X++;
		pos2d2.Y++;
		Pos2d pos2d3 = new Pos2d(0, 0);
		for (int i = pos2d.X; i <= pos2d2.X; i++)
		{
			for (int j = pos2d.Y; j <= pos2d2.Y; j++)
			{
				pos2d3.X = i;
				pos2d3.Y = j;
				bool flag = false;
				DisappearData value;
				if (mClouds.Contains(pos2d3))
				{
					Vector3 val3 = gridGroundComponent.GridToPos(i, j);
					mNormalVertices.Add(new Vector3(mBaseMaskQuad.x + val3.x, mBaseMaskQuad.y + val3.y));
					mNormalVertices.Add(new Vector3(mBaseMaskQuad.x + val3.x, mBaseMaskQuad.w + val3.y));
					mNormalVertices.Add(new Vector3(mBaseMaskQuad.z + val3.x, mBaseMaskQuad.w + val3.y));
					mNormalVertices.Add(new Vector3(mBaseMaskQuad.z + val3.x, mBaseMaskQuad.y + val3.y));
					flag = true;
				}
				else if (mFadingClouds.TryGetValue(pos2d3, out value))
				{
					Vector3 val3 = gridGroundComponent.GridToPos(i, j);
					mFadingVertices.Add(new Vector3(mBaseMaskQuad.x + val3.x, mBaseMaskQuad.y + val3.y));
					mFadingVertices.Add(new Vector3(mBaseMaskQuad.x + val3.x, mBaseMaskQuad.w + val3.y));
					mFadingVertices.Add(new Vector3(mBaseMaskQuad.z + val3.x, mBaseMaskQuad.w + val3.y));
					mFadingVertices.Add(new Vector3(mBaseMaskQuad.z + val3.x, mBaseMaskQuad.y + val3.y));
					mFadingData.Add(new Vector3(value.Center.x, value.Center.y, value.StartTime));
					mFadingData.Add(new Vector3(value.Center.x, value.Center.y, value.StartTime));
					mFadingData.Add(new Vector3(value.Center.x, value.Center.y, value.StartTime));
					mFadingData.Add(new Vector3(value.Center.x, value.Center.y, value.StartTime));
					flag = true;
				}
				if (!flag)
				{
					continue;
				}
				int k = 0;
				for (int num = mVicinities.Length; k < num; k++)
				{
					Vector2Int val4 = mVicinities[k];
					Pos2d pos2d4 = new Pos2d(pos2d3.X + val4.x, pos2d3.Y + val4.y);
					if (mVisited.Add(pos2d4))
					{
						Vector3 val3 = gridGroundComponent.GridToPos(pos2d4);
						mCloudVertices.Add(new Vector3(val3.x - 0.5f, val3.y - 0.5f));
						mCloudVertices.Add(new Vector3(val3.x - 0.5f, val3.y + 0.5f));
						mCloudVertices.Add(new Vector3(val3.x + 0.5f, val3.y + 0.5f));
						mCloudVertices.Add(new Vector3(val3.x + 0.5f, val3.y - 0.5f));
					}
				}
			}
		}
		int count = mNormalVertices.Count;
		int count2 = mFadingVertices.Count;
		bool flag2 = count == 0 && count2 == 0;
		if (!(mLastClear & flag2))
		{
			EnsureUVAndTriangleCount();
			mCloudMesh.SetVertices(mCloudVertices);
			mCloudMesh.SetTriangles(mTriangles, 0, mCloudVertices.Count / 4 * 6, 0, false, 0);
			mMaskMesh.SetVertices(mNormalVertices);
			mMaskMesh.SetUVs(0, mUVs, 0, count);
			mMaskMesh.SetTriangles(mTriangles, 0, count / 4 * 6, 0, false, 0);
			if (count2 > 0)
			{
				mFadingMesh.SetVertices(mFadingVertices);
				mFadingMesh.SetUVs(0, mUVs, 0, count2);
				mFadingMesh.SetTriangles(mTriangles, 0, count2 / 4 * 6, 0, false, 0);
				mFadingMesh.SetUVs(1, mFadingData);
			}
			mLastClear = flag2;
			DrawMask();
		}
	}

	private void EnsureUVAndTriangleCount()
	{
		int num = mCloudVertices.Count / 4;
		int num2 = mUVs.Count / 4;
		if (num2 < num)
		{
			for (int i = num2; i < num; i++)
			{
				mUVs.Add(new Vector2(mBaseMaskUV.x, mBaseMaskUV.y));
				mUVs.Add(new Vector2(mBaseMaskUV.x, mBaseMaskUV.w));
				mUVs.Add(new Vector2(mBaseMaskUV.z, mBaseMaskUV.w));
				mUVs.Add(new Vector2(mBaseMaskUV.z, mBaseMaskUV.y));
				ushort num3 = (ushort)(i * 4);
				mTriangles.Add(num3);
				mTriangles.Add((ushort)(num3 + 1));
				mTriangles.Add((ushort)(num3 + 2));
				mTriangles.Add((ushort)(num3 + 2));
				mTriangles.Add((ushort)(num3 + 3));
				mTriangles.Add(num3);
			}
		}
	}

	private void DrawMask()
	{
		mCommandBuffer.Clear();
		mCommandBuffer.SetRenderTarget(new RenderTargetIdentifier((Texture)mRenderTexture), (RenderBufferLoadAction)2, (RenderBufferStoreAction)0, (RenderBufferLoadAction)2, (RenderBufferStoreAction)3);
		mCommandBuffer.ClearRenderTarget(false, true, Color.black);
		mCommandBuffer.SetViewProjectionMatrices(mMainCamera.worldToCameraMatrix, mMainCamera.projectionMatrix);
		mCommandBuffer.DrawMesh(mMaskMesh, Matrix4x4.identity, CloudMaskMat);
		if (mFadingMesh.vertexCount > 0)
		{
			mCommandBuffer.DrawMesh(mFadingMesh, Matrix4x4.identity, DisappearMat);
		}
		Graphics.ExecuteCommandBuffer(mCommandBuffer);
	}

	public CloudController2()
	{
	}
}

}
