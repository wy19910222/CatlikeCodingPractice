using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace HashingSpace {
	public class HashVisualization : MonoBehaviour {
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

		private static readonly int hashesId = Shader.PropertyToID("_Hashes");
		private static readonly int positionsId = Shader.PropertyToID("_Positions");
		private static readonly int normalsId = Shader.PropertyToID("_Normals");
		private static readonly int configId = Shader.PropertyToID("_Config");

		[SerializeField]
		private Mesh instanceMesh;
		[SerializeField]
		private Material material;
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
	
		private NativeArray<uint> hashes;
		private NativeArray<float3> positions;
		private NativeArray<float3> normals;
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
	
				JobHandle handle = Shapes.Job.ScheduleParallel(positions, normals, resolution, trans.localToWorldMatrix, default);
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
						trans.position,
						math.float3(2f * math.cmax(math.abs(trans.lossyScale)) + displacement)
				);
			}
			Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, hashes.Length, propertyBlock);
		}
	}
}
