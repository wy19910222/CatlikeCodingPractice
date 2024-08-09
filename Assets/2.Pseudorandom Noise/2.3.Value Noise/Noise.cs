using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace ValueNoise {
	public partial class Noise {
		public static readonly ScheduleDelegate[] noiseJobs = {
			Job<Lattice1D>.ScheduleParallel,
			Job<Lattice2D>.ScheduleParallel,
			Job<Lattice3D>.ScheduleParallel
		};
		
		public interface INoise {
			float4 GetNoise4(float4x3 positions, SmallXXHash4 hash);
		}
		
		private struct LatticeSpan4 {
			public int4 p0, p1;
			public float4 t;
		}
		
		private static LatticeSpan4 GetLatticeSpan4(float4 coordinates) {
			float4 points = math.floor(coordinates);
			LatticeSpan4 span;
			span.p0 = (int4) points;
			span.p1 = span.p0 + 1;
			span.t = coordinates - points;
			span.t = span.t * span.t * span.t * (span.t * (span.t * 6f - 15f) + 10f);
			return span;
		}

		[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
		public struct Job<N> : IJobFor where N : struct, INoise {
			[ReadOnly]
			public NativeArray<float3x4> positions;

			[WriteOnly]
			public NativeArray<float4> noise;

			public SmallXXHash4 hash;

			public float3x4 domainTRS;

			public void Execute (int i) {
				noise[i] = default(N).GetNoise4(domainTRS.TransformVectors(math.transpose(positions[i])), hash);
			}
			
			public static JobHandle ScheduleParallel(
					NativeArray<float3x4> positions, NativeArray<float4> noise,
					int seed, SpaceTRS domainTRS, int resolution, JobHandle dependency
			) => new Job<N> {
				positions = positions,
				noise = noise,
				hash = SmallXXHash.Seed(seed),
				domainTRS = domainTRS.Matrix,
			}.ScheduleParallel(positions.Length, resolution, dependency);
		}
	
		public delegate JobHandle ScheduleDelegate (
				NativeArray<float3x4> positions, NativeArray<float4> noise,
				int seed, SpaceTRS domainTRS, int resolution, JobHandle dependency
		);
	}
}
