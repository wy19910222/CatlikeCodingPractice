using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Hashing {
	public class HashVisualization : MonoBehaviour {
		[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
		private struct HashJob : IJobFor {
			[WriteOnly]
			public NativeArray<uint> hashes;

			public int resolution;
			public float invResolution;
			public SmallXXHash hash;

			public void Execute(int i) {
				int v = (int) floor(invResolution * i + 0.00001f);
				int u = i - resolution * v - resolution / 2;
				v -= resolution / 2;
				hashes[i] = hash.Eat(u).Eat(v);
			}
		}

		private static readonly int hashesId = Shader.PropertyToID("_Hashes");
		private static readonly int configId = Shader.PropertyToID("_Config");

		[SerializeField]
		private Mesh instanceMesh;
		[SerializeField]
		private Material material;
		[SerializeField, Range(1, 512)]
		private int resolution = 16;
		[SerializeField, Range(-2f, 2f)]
		private float verticalOffset = 1f;
		[SerializeField]
		private int seed;

		private NativeArray<uint> hashes;
		private ComputeBuffer hashesBuffer;
		private MaterialPropertyBlock propertyBlock;

		void OnValidate() {
			if (hashesBuffer != null && enabled) {
				OnDisable();
				OnEnable();
			}
		}

		void OnEnable() {
			int length = resolution * resolution;
			hashes = new NativeArray<uint>(length, Allocator.Persistent);
			hashesBuffer = new ComputeBuffer(length, 4);

			new HashJob {
				hashes = hashes,
				resolution = resolution,
				invResolution = 1f / resolution,
				hash = SmallXXHash.Seed(seed)
			}.ScheduleParallel(hashes.Length, resolution, default).Complete();

			hashesBuffer.SetData(hashes);

			propertyBlock ??= new MaterialPropertyBlock();
			propertyBlock.SetBuffer(hashesId, hashesBuffer);
			propertyBlock.SetVector(configId, new Vector4(resolution, 1f / resolution, verticalOffset / resolution));
		}

		void OnDisable() {
			hashes.Dispose();
			hashesBuffer.Release();
			hashesBuffer = null;
		}

		void Update() {
			Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, new Bounds(Vector3.zero, Vector3.one), hashes.Length, propertyBlock);
		}
	}
}
