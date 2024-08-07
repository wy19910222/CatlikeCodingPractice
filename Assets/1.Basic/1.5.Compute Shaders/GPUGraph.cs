using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Compute_Shaders {
	public class GPUGraph : MonoBehaviour {
		private static readonly int positionsId = Shader.PropertyToID("_Positions");
		private static readonly int resolutionId = Shader.PropertyToID("_Resolution");
		private static readonly int stepId = Shader.PropertyToID("_Step");
		private static readonly int timeId = Shader.PropertyToID("_Time");
		private static readonly int transitionProgressId = Shader.PropertyToID("_TransitionProgress");
		
		private static readonly int scaleId = Shader.PropertyToID("_Scale");
		private static readonly int positionId = Shader.PropertyToID("_Position");
		
		const int maxResolution = 1000;
		
		public ComputeShader computeShader;
		public Material material;
		public Mesh mesh;
		
		[Range(10, maxResolution)]
		public int resolution = 10;
		
		public enum FunctionName { Wave, MultiWave, Ripple, Sphere, Torus }
		public FunctionName function;
		public enum TransitionMode { Cycle, Random }
		public TransitionMode transitionMode;
		[Min(0f)]
		public float functionDuration = 1f, transitionDuration = 1f;
		
		private float duration;
		private bool transitioning;
		private FunctionName transitionFunction;
		
		private ComputeBuffer positionsBuffer;

		void OnEnable () {
			positionsBuffer = new ComputeBuffer(maxResolution * maxResolution, 3 * 4);
		}
		
		void OnDisable () {
			positionsBuffer.Release();
			positionsBuffer = null;
		}
		
		void Update () {
			duration += Time.deltaTime;
			if (transitioning) {
				if (duration >= transitionDuration) {
					duration -= transitionDuration;
					transitioning = false;
				}
			}
			else if (duration >= functionDuration) {
				duration -= functionDuration;
				transitioning = true;
				transitionFunction = function;
				PickNextFunction();
			}
			UpdateFunctionOnGPU();
		}
		
		void UpdateFunctionOnGPU() {
			float step = 2f / resolution;
			computeShader.SetInt(resolutionId, resolution);
			computeShader.SetFloat(stepId, step);
			computeShader.SetFloat(timeId, Time.time);
			if (transitioning) {
				computeShader.SetFloat(transitionProgressId, Mathf.SmoothStep(0f, 1f, duration / transitionDuration));
			}
			
			int kernelIndex = (int) function + (int) (transitioning ? transitionFunction : function) * FunctionCount;
			computeShader.SetBuffer(kernelIndex, positionsId, positionsBuffer);
			
			int groups = Mathf.CeilToInt(resolution / 8f);
			computeShader.Dispatch(kernelIndex, groups, groups, 1);
			
			material.SetBuffer(positionsId, positionsBuffer);
			material.SetVector(scaleId, transform.localScale);
			material.SetVector(positionId, transform.position);
			material.SetFloat(stepId, step);
			var bounds = new Bounds(Vector3.zero, Vector3.one * (2f + 2f / resolution));
			Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, resolution * resolution);
		}
	
		void PickNextFunction () {
			function = transitionMode == TransitionMode.Cycle ? GetNextFunctionName(function) : GetRandomFunctionNameOtherThan(function);
		}
		
		public static int FunctionCount => Enum.GetValues(typeof(FunctionName)).Length;
		
		public static FunctionName GetNextFunctionName(FunctionName name) => (int) name < FunctionCount - 1 ? name + 1 : 0;
		
		public static FunctionName GetRandomFunctionNameOtherThan(FunctionName name) {
			FunctionName choice = (FunctionName) Random.Range(1, FunctionCount);
			return choice == name ? 0 : choice;
		}
	}
}
