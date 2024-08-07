Shader "Fractal/Fractal Surface GPU Organic Variety" {
	SubShader {
		CGPROGRAM
		#pragma surface ConfigureSurface Standard fullforwardshadows addshadow
		#pragma instancing_options assumeuniformscaling procedural:ConfigureProcedural
		#pragma editor_sync_compilation
		#pragma target 4.5
		
		#include "FractalGPUOrganicVariety.hlsl"
		
		struct Input {
			float3 worldPos;
		};

		void ConfigureSurface(Input input, inout SurfaceOutputStandard surface) {
			surface.Albedo = GetFractalColor().rgb;
			surface.Smoothness = GetFractalColor().a;
		}
		ENDCG
	}
	FallBack "Diffuse"
}