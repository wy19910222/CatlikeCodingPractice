using UnityEngine;

namespace Rolling {
	public class PositionInterpolator : MonoBehaviour {
		[SerializeField] private Rigidbody body;
		[SerializeField] private Vector3 from, to;
		[SerializeField] private Transform relativeTo;
	
		public void Interpolate(float t) {
			Vector3 p;
			if (relativeTo) {
				p = Vector3.LerpUnclamped(relativeTo.TransformPoint(from), relativeTo.TransformPoint(to), t);
			} else {
				p = Vector3.LerpUnclamped(from, to, t);
			}
			body.MovePosition(p);
		}
	}
}
