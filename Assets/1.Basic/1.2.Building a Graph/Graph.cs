using UnityEngine;

namespace Building_a_Graph {
	public class Graph : MonoBehaviour {
		public Transform pointPrefab;
		[Range(10, 100)]
		public int resolution = 10;
		public Transform[] m_Points;
	
		void Awake () {
			float step = 2f / resolution;
			Vector3 position = Vector3.zero;
			Vector3 scale = Vector3.one * step;
			m_Points = new Transform[resolution];
			for (int i = 0; i < resolution; i++) {
				Transform point = m_Points[i] = Instantiate(pointPrefab, transform, false);
				position.x = (i + 0.5f) * step - 1f;
				point.localPosition = position;
				point.localScale = scale;
			}
		}

		private void Update() {
			float time = Time.time;
			for (int i = 0, length = m_Points.Length; i < length; i++) {
				Transform point = m_Points[i];
				Vector3 position = point.localPosition;
				position.y = Mathf.Sin(Mathf.PI * (position.x + time));
				point.localPosition = position;
			}
		}
	}
}
