using Unity.Mathematics;

namespace NoiseVariants {
	public static partial class Noise {
		public interface IGradient {
			float4 Evaluate(SmallXXHash4 hash, float4 x);

			float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y);

			float4 Evaluate(SmallXXHash4 hash, float4 x, float4 y, float4 z);

			float4 EvaluateAfterInterpolation(float4 value);
		}
		
		public struct Lattice1D<L, G> : INoise where L : struct, ILattice where G : struct, IGradient {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency) {
				LatticeSpan4 x = default(L).GetLatticeSpan4(positions.c0, frequency);
				G g = default(G);
				return g.EvaluateAfterInterpolation(math.lerp(g.Evaluate(hash.Eat(x.p0), x.g0), g.Evaluate(hash.Eat(x.p1), x.g1), x.t));
			}
		}
		
		public struct Lattice2D<L, G> : INoise where L : struct, ILattice where G : struct, IGradient {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency) {
				LatticeSpan4 x = default(L).GetLatticeSpan4(positions.c0, frequency);
				LatticeSpan4 z = default(L).GetLatticeSpan4(positions.c2, frequency);
				SmallXXHash4 h0 = hash.Eat(x.p0), h1 = hash.Eat(x.p1);
				G g = default(G);
				return g.EvaluateAfterInterpolation(math.lerp(
						math.lerp(g.Evaluate(h0.Eat(z.p0), x.g0, z.g0), g.Evaluate(h0.Eat(z.p1), x.g0, z.g1), z.t),
						math.lerp(g.Evaluate(h1.Eat(z.p0), x.g1, z.g0), g.Evaluate(h1.Eat(z.p1), x.g1, z.g1), z.t),
						x.t
				));
			}
		}
		
		public struct Lattice3D<L, G> : INoise where L : struct, ILattice where G : struct, IGradient {
			public float4 GetNoise4(float4x3 positions, SmallXXHash4 hash, int frequency) {
				LatticeSpan4 x = default(L).GetLatticeSpan4(positions.c0, frequency);
				LatticeSpan4 y = default(L).GetLatticeSpan4(positions.c1, frequency);
				LatticeSpan4 z = default(L).GetLatticeSpan4(positions.c2, frequency);
				SmallXXHash4 h0 = hash.Eat(x.p0), h1 = hash.Eat(x.p1);
				SmallXXHash4 h00 = h0.Eat(y.p0), h01 = h0.Eat(y.p1);
				SmallXXHash4 h10 = h1.Eat(y.p0), h11 = h1.Eat(y.p1);
				G g = default(G);
				return g.EvaluateAfterInterpolation(math.lerp(
						math.lerp(
								math.lerp(g.Evaluate(h00.Eat(z.p0), x.g0, y.g0, z.g0), g.Evaluate(h00.Eat(z.p1), x.g0, y.g0, z.g1), z.t),
								math.lerp(g.Evaluate(h01.Eat(z.p0), x.g0, y.g1, z.g0), g.Evaluate(h01.Eat(z.p1), x.g0, y.g1, z.g1), z.t),
								y.t
						),
						math.lerp(
								math.lerp(g.Evaluate(h10.Eat(z.p0), x.g1, y.g0, z.g0), g.Evaluate(h10.Eat(z.p1), x.g1, y.g0, z.g1), z.t),
								math.lerp(g.Evaluate(h11.Eat(z.p0), x.g1, y.g1, z.g0), g.Evaluate(h11.Eat(z.p1), x.g1, y.g1, z.g1), z.t),
								y.t
						),
						x.t
				));
			}
		}
	}
}
