using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HashingSpace {
	public class HashVisualization_Vectorization_MoreShapes : MonoBehaviour {
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
	
		private static readonly int hashesId = Shader.PropertyToID("_Hashes");
		private static readonly int positionsId = Shader.PropertyToID("_Positions");
		private static readonly int normalsId = Shader.PropertyToID("_Normals");
		private static readonly int configId = Shader.PropertyToID("_Config");
	
		[SerializeField]
		private Mesh instanceMesh;
		[SerializeField]
		private Material material;
		[SerializeField]
		private Shapes_Vectorization_MoreShapes.Shape shape;
		[SerializeField, Range(0.1f, 10f)]
		private float instanceScale = 2f;
		[SerializeField, Range(1, 512)]
		private int resolution = 16;
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
	
		void OnValidate() {
			if (hashesBuffer != null && enabled) {
				OnDisable();
				OnEnable();
			}
		}
		
		void OnEnable() {
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
			propertyBlock.SetVector(configId, new Vector4(resolution, instanceScale / resolution, displacement));
		}
		
		void OnDisable() {
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
		
		void Update() {
			Transform trans = transform;
			if (isDirty || trans.hasChanged) {
				isDirty = false;
				trans.hasChanged = false;
	
				JobHandle handle = Shapes_Vectorization_MoreShapes.shapeJobs[(int)shape](positions, normals, resolution, trans.localToWorldMatrix, default);
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
						trans.position,
						math.float3(2f * math.cmax(math.abs(trans.lossyScale)) + displacement)
				);
			}
			Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, resolution * resolution, propertyBlock);
		}
	}
}
