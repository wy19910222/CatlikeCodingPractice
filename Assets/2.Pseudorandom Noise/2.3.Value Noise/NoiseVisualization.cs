using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ValueNoise {
	public class NoiseVisualization : Visualization {
		private static readonly int noiseId = Shader.PropertyToID("_Noise");
	
		[SerializeField]
		private int seed;
		[SerializeField, Range(1, 3)]
		private int dimensions = 3;
		[SerializeField]
		private SpaceTRS domain = new SpaceTRS {
			scale = 8f
		};
	
		private NativeArray<float4> noise;
		private ComputeBuffer noiseBuffer;
		
		protected override void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock) {
			noise = new NativeArray<float4>(dataLength, Allocator.Persistent);
			noiseBuffer = new ComputeBuffer(dataLength * 4, 4);
			propertyBlock.SetBuffer(noiseId, noiseBuffer);
		}
		
		protected override void DisableVisualization() {
			noise.Dispose();
			noiseBuffer.Release();
			noiseBuffer = null;
		}
		
		protected override void UpdateVisualization(NativeArray<float3x4> positions, int resolution, JobHandle handle) {
			Noise.noiseJobs[dimensions - 1](positions, noise, seed, domain, resolution, handle).Complete();
			noiseBuffer.SetData(noise.Reinterpret<float>(4 * 4));
		}
	}
}
