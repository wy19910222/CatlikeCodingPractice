using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace HashingSpace {
	public static class Shape_Vectorization_MoreShapes {
		public enum Shape { Plane, Sphere, Torus }
		
		public static readonly ScheduleDelegate[] shapeJobs = {
			Job<Plane>.ScheduleParallel,
			Job<Sphere>.ScheduleParallel,
			Job<Torus>.ScheduleParallel
		};
		
		public delegate JobHandle ScheduleDelegate(NativeArray<float3x4> positions, NativeArray<float3x4> normals, int resolution, float4x4 trs, JobHandle dependency);
		
		public struct Point4 {
			public float4x3 positions, normals;
		}
		
		public interface IShape {
			Point4 GetPoint4 (int i, float resolution, float invResolution);
		}
	
		public struct Plane : IShape {
			public Point4 GetPoint4(int i, float resolution, float invResolution) {
				float4x2 uv = IndexTo4UV(i, resolution, invResolution);
				return new Point4 {
					positions = math.float4x3(uv.c0 - 0.5f, 0f, uv.c1 - 0.5f),
					normals = math.float4x3(0f, 1f, 0f)
				};
			}
		}
		
		public struct Sphere : IShape {
			public Point4 GetPoint4 (int i, float resolution, float invResolution) {
				float4x2 uv = IndexTo4UV(i, resolution, invResolution);
	
				Point4 p;
				p.positions.c0 = uv.c0 - 0.5f;
				p.positions.c1 = uv.c1 - 0.5f;
				p.positions.c2 =  0.5f - math.abs(p.positions.c0) - math.abs(p.positions.c1);
				float4 offset = math.max(-p.positions.c2, 0f);
				p.positions.c0 += math.select(-offset, offset, p.positions.c0 < 0f);
				p.positions.c1 += math.select(-offset, offset, p.positions.c1 < 0f);
				float4 scale = 0.5f * math.rsqrt(
						p.positions.c0 * p.positions.c0 +
						p.positions.c1 * p.positions.c1 +
						p.positions.c2 * p.positions.c2
				);
				p.positions.c0 *= scale;
				p.positions.c1 *= scale;
				p.positions.c2 *= scale;
				
				p.normals = p.positions;
				return p;
			}
		}
		
		public struct Torus : IShape {
			public Point4 GetPoint4 (int i, float resolution, float invResolution) {
				float4x2 uv = IndexTo4UV(i, resolution, invResolution);
	
				float r1 = 0.375f;
				float r2 = 0.125f;
				float4 s = r1 + r2 * math.cos(2f * math.PI * uv.c1);
	
				Point4 p;
				p.positions.c0 = s * math.sin(2f * math.PI * uv.c0);
				p.positions.c1 = r2 * math.sin(2f * math.PI * uv.c1);
				p.positions.c2 = s * math.cos(2f * math.PI * uv.c0);
				p.normals = p.positions;
				p.normals.c0 -= r1 * math.sin(2f * math.PI * uv.c0);
				p.normals.c2 -= r1 * math.cos(2f * math.PI * uv.c0);
				return p;
			}
		}
	
		public static float4x2 IndexTo4UV (int i, float resolution, float invResolution) {
			float4x2 uv;
			float4 i4 = 4f * i + math.float4(0f, 1f, 2f, 3f);
			uv.c1 = math.floor(invResolution * i4 + 0.00001f);
			uv.c0 = invResolution * (i4 - resolution * uv.c1 + 0.5f);
			uv.c1 = invResolution * (uv.c1 + 0.5f);
			return uv;
		}
		
		[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
		public struct Job<S> : IJobFor where S : struct, IShape {
			[WriteOnly]
			private NativeArray<float3x4> positions;
			private NativeArray<float3x4> normals;
	
			public float resolution, invResolution;
	
			public float3x4 positionTRS, normalTRS;
			
			private float4x3 TransformVectors(float3x4 trs, float4x3 p, float w = 1f) => math.float4x3(
					trs.c0.x * p.c0 + trs.c1.x * p.c1 + trs.c2.x * p.c2 + trs.c3.x * w,
					trs.c0.y * p.c0 + trs.c1.y * p.c1 + trs.c2.y * p.c2 + trs.c3.y * w,
					trs.c0.z * p.c0 + trs.c1.z * p.c1 + trs.c2.z * p.c2 + trs.c3.z * w
			);
	
			public void Execute (int i) {
				Point4 p = default(S).GetPoint4(i, resolution, invResolution);
	
				positions[i] = math.transpose(TransformVectors(positionTRS, p.positions));
				float3x4 n = math.transpose(TransformVectors(normalTRS, p.positions, 0f));
				normals[i] = math.float3x4(math.normalize(n.c0), math.normalize(n.c1), math.normalize(n.c2), math.normalize(n.c3));
			}
			
			public static JobHandle ScheduleParallel(NativeArray<float3x4> positions, NativeArray<float3x4> normals, int resolution, float4x4 trs, JobHandle dependency) {
				float4x4 tim = math.transpose(math.inverse(trs));
				return new Job<S> {
					positions = positions,
					normals = normals,
					resolution = resolution,
					invResolution = 1f / resolution,
					positionTRS = math.float3x4(trs.c0.xyz, trs.c1.xyz, trs.c2.xyz, trs.c3.xyz),
					normalTRS = math.float3x4(tim.c0.xyz, tim.c1.xyz, tim.c2.xyz, tim.c3.xyz)
				}.ScheduleParallel(positions.Length, resolution, dependency);
			}
		}
	}
}
