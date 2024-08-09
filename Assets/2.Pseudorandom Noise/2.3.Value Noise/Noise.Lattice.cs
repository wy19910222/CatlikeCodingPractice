using Unity.Mathematics;

namespace ValueNoise {
	public static partial class Noise {
		public struct Lattice1D : INoise {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash) {
				LatticeSpan4 x = GetLatticeSpan4(positions.c0);
				return math.lerp(hash.Eat(x.p0).Floats01A, hash.Eat(x.p1).Floats01A, x.t) * 2f - 1f;
			}
		}
		
		public struct Lattice2D : INoise {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash) {
				LatticeSpan4 x = GetLatticeSpan4(positions.c0);
				LatticeSpan4 z = GetLatticeSpan4(positions.c2);
				SmallXXHash4 h0 = hash.Eat(x.p0), h1 = hash.Eat(x.p1);
				return math.lerp(
						math.lerp(h0.Eat(z.p0).Floats01A, h0.Eat(z.p1).Floats01A, z.t),
						math.lerp(h1.Eat(z.p0).Floats01A, h1.Eat(z.p1).Floats01A, z.t),
						x.t
				) * 2f - 1f;
			}
		}
		
		public struct Lattice3D : INoise {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash) {
				LatticeSpan4 x = GetLatticeSpan4(positions.c0);
				LatticeSpan4 y = GetLatticeSpan4(positions.c1);
				LatticeSpan4 z = GetLatticeSpan4(positions.c2);
				SmallXXHash4 h0 = hash.Eat(x.p0), h1 = hash.Eat(x.p1);
				SmallXXHash4 h00 = h0.Eat(y.p0), h01 = h0.Eat(y.p1);
				SmallXXHash4 h10 = h1.Eat(y.p0), h11 = h1.Eat(y.p1);
				return math.lerp(
						math.lerp(
								math.lerp(h00.Eat(z.p0).Floats01A, h00.Eat(z.p1).Floats01A, z.t),
								math.lerp(h01.Eat(z.p0).Floats01A, h01.Eat(z.p1).Floats01A, z.t),
								y.t
						),
						math.lerp(
								math.lerp(h10.Eat(z.p0).Floats01A, h10.Eat(z.p1).Floats01A, z.t),
								math.lerp(h11.Eat(z.p0).Floats01A, h11.Eat(z.p1).Floats01A, z.t),
								y.t
						),
						x.t
				) * 2f - 1f;
			}
		}
	}
}
