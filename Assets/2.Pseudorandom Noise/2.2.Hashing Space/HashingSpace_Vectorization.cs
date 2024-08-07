using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class HashingSpace_Vectorization : MonoBehaviour {
	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	public struct ShapesJob : IJobFor {
		[WriteOnly]
		private NativeArray<float3x4> positions;
		private NativeArray<float3x4> normals;

		public float resolution, invResolution;

		public float3x4 positionTRS;
		
		private float4x3 TransformVectors(float3x4 trs, float4x3 p, float w = 1f) => math.float4x3(
				trs.c0.x * p.c0 + trs.c1.x * p.c1 + trs.c2.x * p.c2 + trs.c3.x * w,
				trs.c0.y * p.c0 + trs.c1.y * p.c1 + trs.c2.y * p.c2 + trs.c3.y * w,
				trs.c0.z * p.c0 + trs.c1.z * p.c1 + trs.c2.z * p.c2 + trs.c3.z * w
		);

		public void Execute (int i) {
			float4x2 uv;
			float4 i4 = 4f * i + math.float4(0f, 1f, 2f, 3f);
			uv.c1 = math.floor(invResolution * i4 + 0.00001f);
			uv.c0 = invResolution * (i4 - resolution * uv.c1 + 0.5f) - 0.5f;
			uv.c1 = invResolution * (uv.c1 + 0.5f) - 0.5f;

			positions[i] = math.transpose(TransformVectors(positionTRS, math.float4x3(uv.c0, 0f, uv.c1)));
			float3x4 n = math.transpose(TransformVectors(positionTRS, math.float4x3(0f, 1f, 0f), 0f));
			normals[i] = math.float3x4(math.normalize(n.c0), math.normalize(n.c1), math.normalize(n.c2), math.normalize(n.c3));
		}
		
		public static JobHandle ScheduleParallel(NativeArray<float3x4> positions, NativeArray<float3x4> normals, int resolution, float4x4 trs, JobHandle dependency) {
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
		public NativeArray<float3x4> positions;
		
		[WriteOnly]
		public NativeArray<uint4> hashes;
		
		public SmallXXHash4 hash;
		public float3x4 domainTRS;
		
		private float4x3 TransformPositions(float3x4 trs, float4x3 p) => math.float4x3(
				trs.c0.x * p.c0 + trs.c1.x * p.c1 + trs.c2.x * p.c2 + trs.c3.x,
				trs.c0.y * p.c0 + trs.c1.y * p.c1 + trs.c2.y * p.c2 + trs.c3.y,
				trs.c0.z * p.c0 + trs.c1.z * p.c1 + trs.c2.z * p.c2 + trs.c3.z
		);

		public void Execute(int i) {
			float4x3 p = TransformPositions(domainTRS, math.transpose(positions[i]));
			
			int4 u = (int4) math.floor(p.c0);
			int4 v = (int4) math.floor(p.c1);
			int4 w = (int4) math.floor(p.c2);

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

	private NativeArray<uint4> hashes;
	private NativeArray<float3x4> positions;
	private NativeArray<float3x4> normals;
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
		length = length / 4 + (length & 1);
		hashes = new NativeArray<uint4>(length, Allocator.Persistent);
		positions = new NativeArray<float3x4>(length, Allocator.Persistent);
		normals = new NativeArray<float3x4>(length, Allocator.Persistent);
		hashesBuffer = new ComputeBuffer(length * 4, 4);
		positionsBuffer = new ComputeBuffer(length * 4, 3 * 4);
		normalsBuffer = new ComputeBuffer(length * 4, 3 * 4);

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

			hashesBuffer.SetData(hashes.Reinterpret<uint>(4 * 4));
			positionsBuffer.SetData(positions.Reinterpret<float3>(3 * 4 * 4));
			normalsBuffer.SetData(normals.Reinterpret<float3>(3 * 4 * 4));
			
			bounds = new Bounds(
					transform.position,
					math.float3(2f * math.cmax(math.abs(transform.lossyScale)) + displacement)
			);
		}
		Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, resolution * resolution, propertyBlock);
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
		
		public static implicit operator SmallXXHash4(SmallXXHash hash) => new SmallXXHash4(hash.accumulator);
	
		private static uint RotateLeft(uint data, int steps) => (data << steps) | (data >> 32 - steps);
	}

	public readonly struct SmallXXHash4 {
		const uint primeB = 0b10000101111010111100101001110111;
		const uint primeC = 0b11000010101100101010111000111101;
		const uint primeD = 0b00100111110101001110101100101111;
		const uint primeE = 0b00010110010101100110011110110001;
	
		readonly uint4 accumulator;
	
		public SmallXXHash4(uint4 accumulator) {
			this.accumulator = accumulator;
		}
	
		public SmallXXHash4 Eat(int4 data) => RotateLeft(accumulator + (uint4) data * primeC, 17) * primeD;
	
		public static SmallXXHash4 Seed(int4 seed) => (uint4) seed + primeE;
	
		public static implicit operator uint4(SmallXXHash4 hash) {
			uint4 avalanche = hash.accumulator;
			avalanche ^= avalanche >> 15;
			avalanche *= primeB;
			avalanche ^= avalanche >> 13;
			avalanche *= primeC;
			avalanche ^= avalanche >> 16;
			return avalanche;
		}
	
		public static implicit operator SmallXXHash4(uint4 accumulator) => new SmallXXHash4(accumulator);
	
		private static uint4 RotateLeft(uint4 data, int steps) => (data << steps) | (data >> 32 - steps);
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