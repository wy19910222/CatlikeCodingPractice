using Unity.Mathematics;

namespace NoiseVariants {
	public static partial class Noise {
		
		public struct Value : IGradient {
			public float4 Evaluate(SmallXXHash4 hash, float4 x) => hash.Floats01A * 2f - 1f;

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y) => hash.Floats01A * 2f - 1f;

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y, float4 z) => hash.Floats01A * 2f - 1f;
			
			public float4 EvaluateAfterInterpolation(float4 value) => value;
		}
		
		public struct Perlin : IGradient {
			public float4 Evaluate(SmallXXHash4 hash, float4 x) => (1f + hash.Floats01A) * math.select(-x, x, ((uint4) hash & 1 << 8) == 0);

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y) {
				float4 gx = hash.Floats01A * 2f - 1f;
				float4 gy = 0.5f - math.abs(gx);
				gx -= math.floor(gx + 0.5f);
				return (gx * x + gy * y) * (2f / 0.53528f);
			}

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y, float4 z) {
				float4 gx = hash.Floats01A * 2f - 1f, gy = hash.Floats01D * 2f - 1f;
				float4 gz = 1f - math.abs(gx) - math.abs(gy);
				float4 offset = math.max(-gz, 0f);
				gx += math.select(-offset, offset, gx < 0f);
				gy += math.select(-offset, offset, gy < 0f);
				return (gx * x + gy * y + gz * z) * (1f / 0.56290f);
			}
			
			public float4 EvaluateAfterInterpolation(float4 value) => value;
		}
		
		public struct Turbulence<G> : IGradient where G : struct, IGradient {
			public float4 Evaluate(SmallXXHash4 hash, float4 x) => default(G).Evaluate(hash, x);

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y) => default(G).Evaluate(hash, x, y);

			public float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y, float4 z) => default(G).Evaluate(hash, x, y, z);

			public float4 EvaluateAfterInterpolation(float4 value) => math.abs(default(G).EvaluateAfterInterpolation(value));
		}
	}
}
