using UnityEngine;

namespace Mathematical_Surfaces {
	public class Graph : MonoBehaviour {
		public Transform pointPrefab;
		[Range(10, 100)]
		public int resolution = 10;
		public FunctionLibrary.FunctionName function;
		public enum TransitionMode { Cycle, Random }
		public TransitionMode transitionMode;
		[Min(0f)]
		public float functionDuration = 1f, transitionDuration = 1f;
		
		private Transform[] m_Points;
		private float duration;
		private bool transitioning;
		private FunctionLibrary.FunctionName transitionFunction;
		
		void Awake () {
			float step = 2f / resolution;
			Vector3 scale = Vector3.one * step;
			m_Points = new Transform[resolution * resolution];
			for (int i = 0, length = m_Points.Length; i < length; i++) {
				Transform point = m_Points[i] = Instantiate(pointPrefab, transform, false);
				point.localScale = scale;
			}
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
			if (transitioning) {
				UpdateFunctionTransition();
			}
			else {
				UpdateFunction();
			}
		}
	
		void PickNextFunction () {
			function = transitionMode == TransitionMode.Cycle ?
					FunctionLibrary.GetNextFunctionName(function) :
					FunctionLibrary.GetRandomFunctionNameOtherThan(function);
		}

		void UpdateFunctionTransition () {
			FunctionLibrary.Function from = FunctionLibrary.GetFunction(transitionFunction);
			FunctionLibrary.Function to = FunctionLibrary.GetFunction(function);
			float progress = duration / transitionDuration;
			float time = Time.time;
			float step = 2f / resolution;
			float v = 0.5f * step - 1f;
			for (int i = 0, x = 0, z = 0; i < m_Points.Length; i++, x++) {
				if (x == resolution) {
					x = 0;
					z += 1;
					v = (z + 0.5f) * step - 1f;
				}
				float u = (x + 0.5f) * step - 1f;
				m_Points[i].localPosition = FunctionLibrary.Morph(u, v, time, from, to, progress);
			}
		}

		void UpdateFunction () {
			FunctionLibrary.Function f = FunctionLibrary.GetFunction(function);
			float time = Time.time;
			float step = 2f / resolution;
			float v = 0.5f * step - 1f;
			for (int i = 0, x = 0, z = 0; i < m_Points.Length; i++, x++) {
				if (x == resolution) {
					x = 0;
					z += 1;
					v = (z + 0.5f) * step - 1f;
				}
				float u = (x + 0.5f) * step - 1f;
				m_Points[i].localPosition = f(u, v, time);
			}
		}
	}
}
