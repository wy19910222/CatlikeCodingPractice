using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class HashingSpace : MonoBehaviour {
	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct ShapesJob : IJobFor {
		[WriteOnly]
		private NativeArray<float3> positions;
		private NativeArray<float3> normals;

		public float resolution, invResolution;

		public float3x4 positionTRS;

		public void Execute (int i) {
			float2 uv;
			uv.y = math.floor(invResolution * i + 0.00001f);
			uv.x = invResolution * (i - resolution * uv.y + 0.5f) - 0.5f;
			uv.y = invResolution * (uv.y + 0.5f) - 0.5f;

			positions[i] = math.mul(positionTRS, math.float4(uv.x, 0f, uv.y, 1F));
			normals[i] = math.normalize(math.mul(positionTRS, math.float4(0f, 1f, 0f, 1f)));
		}
		
		public static JobHandle ScheduleParallel(NativeArray<float3> positions, NativeArray<float3> normals, int resolution, float4x4 trs, JobHandle dependency) {
			return new ShapesJob {
				positions = positions,
				normals = normals,
				resolution = resolution,
				invResolution = 1f / resolution,
				positionTRS = math.float3x4(trs.c0.xyz, trs.c1.xyz, trs.c2.xyz, trs.c3.xyz)
			}.ScheduleParallel(positions.Length, resolution, dependency);
		}
	}
	
	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	private struct HashJob : IJobFor {
		[ReadOnly]
		public NativeArray<float3> positions;
		
		[WriteOnly]
		public NativeArray<uint> hashes;
		
		public SmallXXHash hash;
		public float3x4 domainTRS;

		public void Execute(int i) {
			float3 p = math.mul(domainTRS, math.float4(positions[i], 1f));
			
			int u = (int) math.floor(p.x);
			int v = (int) math.floor(p.y);
			int w = (int) math.floor(p.z);

			hashes[i] = hash.Eat(u).Eat(v).Eat(w);
		}
	}

	private static int hashesId = Shader.PropertyToID("_Hashes");
	private static int positionsId = Shader.PropertyToID("_Positions");
	private static int normalsId = Shader.PropertyToID("_Normals");
	private static int configId = Shader.PropertyToID("_Config");

	[SerializeField]
	private Mesh instanceMesh;
	[SerializeField]
	private Material material;
	[SerializeField, Range(1, 512)]
	private int resolution = 16;
	[SerializeField, Range(-2f, 2f)]
	private float verticalOffset = 1f;
	[SerializeField, Range(-0.5f, 0.5f)]
	private float displacement = 0.1f;
	[SerializeField]
	private int seed;
	[SerializeField]
	private SpaceTRS domain = new SpaceTRS {
		scale = 8f
	};

	private NativeArray<uint> hashes;
	private NativeArray<float3> positions;
	private NativeArray<float3> normals;
	private ComputeBuffer hashesBuffer;
	private ComputeBuffer positionsBuffer;
	private ComputeBuffer normalsBuffer;
	private MaterialPropertyBlock propertyBlock;
	
	private bool isDirty;
	private Bounds bounds;

	void OnValidate () {
		if (hashesBuffer != null && enabled) {
			OnDisable();
			OnEnable();
		}
	}
	
	void OnEnable () {
		isDirty = true;
		
		int length = resolution * resolution;
		hashes = new NativeArray<uint>(length, Allocator.Persistent);
		positions = new NativeArray<float3>(length, Allocator.Persistent);
		normals = new NativeArray<float3>(length, Allocator.Persistent);
		hashesBuffer = new ComputeBuffer(length, 4);
		positionsBuffer = new ComputeBuffer(length, 3 * 4);
		normalsBuffer = new ComputeBuffer(length, 3 * 4);

		propertyBlock ??= new MaterialPropertyBlock();
		propertyBlock.SetBuffer(hashesId, hashesBuffer);
		propertyBlock.SetBuffer(positionsId, positionsBuffer);
		propertyBlock.SetBuffer(normalsId, normalsBuffer);
		propertyBlock.SetVector(configId, new Vector4(resolution, 1f / resolution, displacement));
	}
	
	void OnDisable () {
		hashes.Dispose();
		positions.Dispose();
		normals.Dispose();
		hashesBuffer.Release();
		positionsBuffer.Release();
		normalsBuffer.Release();
		hashesBuffer = null;
		positionsBuffer = null;
		normalsBuffer = null;
	}
	
	void Update () {
		if (isDirty || transform.hasChanged) {
			isDirty = false;
			transform.hasChanged = false;

			JobHandle handle = ShapesJob.ScheduleParallel(positions, normals, resolution, transform.localToWorldMatrix, default);
			new HashJob {
				positions = positions,
				hashes = hashes,
				hash = SmallXXHash.Seed(seed),
				domainTRS = domain.Matrix
			}.ScheduleParallel(hashes.Length, resolution, handle).Complete();

			hashesBuffer.SetData(hashes);
			positionsBuffer.SetData(positions);
			normalsBuffer.SetData(normals);
			
			bounds = new Bounds(
					transform.position,
					math.float3(2f * math.cmax(math.abs(transform.lossyScale)) + displacement)
			);
		}
		Graphics.DrawMeshInstancedProcedural( instanceMesh, 0, material, bounds, hashes.Length, propertyBlock);
	}

	public readonly struct SmallXXHash {
		const uint primeA = 0b10011110001101110111100110110001;
		const uint primeB = 0b10000101111010111100101001110111;
		const uint primeC = 0b11000010101100101010111000111101;
		const uint primeD = 0b00100111110101001110101100101111;
		const uint primeE = 0b00010110010101100110011110110001;
	
		readonly uint accumulator;
	
		public SmallXXHash(uint accumulator) {
			this.accumulator = accumulator;
		}
	
		public SmallXXHash Eat(int data) => RotateLeft(accumulator + (uint) data * primeC, 17) * primeD;
	
		public SmallXXHash Eat(byte data) => RotateLeft(accumulator + data * primeE, 11) * primeA;
	
		public static SmallXXHash Seed(int seed) => (uint) seed + primeE;
	
		public static implicit operator uint(SmallXXHash hash) {
			uint avalanche = hash.accumulator;
			avalanche ^= avalanche >> 15;
			avalanche *= primeB;
			avalanche ^= avalanche >> 13;
			avalanche *= primeC;
			avalanche ^= avalanche >> 16;
			return avalanche;
		}
	
		public static implicit operator SmallXXHash(uint accumulator) => new SmallXXHash(accumulator);
		
		private static uint RotateLeft(uint data, int steps) => (data << steps) | (data >> 32 - steps);
	}
	
	[System.Serializable]
	public struct SpaceTRS {
		public float3 translation, rotation, scale;

		public float3x4 Matrix {
			get {
				float4x4 m = float4x4.TRS(
						translation, quaternion.EulerZXY(math.radians(rotation)), scale
				);
				return math.float3x4(m.c0.xyz, m.c1.xyz, m.c2.xyz, m.c3.xyz);
			}
		}
	}
}