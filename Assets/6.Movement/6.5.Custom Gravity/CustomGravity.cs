using UnityEngine;

namespace CustomGravity {
	public static class CustomGravity {
		public static Vector3 GetGravity(Vector3 position) {
			return position.normalized * Physics.gravity.y;
			// return Physics.gravity;
		}
		
		public static Vector3 GetUpAxis(Vector3 position) {
			Vector3 up = position.normalized;
			return Physics.gravity.y < 0f ? up : -up;
			// return -Physics.gravity.normalized;
		}
		
		public static Vector3 GetGravity(Vector3 position, out Vector3 upAxis) {
			upAxis = GetUpAxis(position);
			return GetGravity(position);
		}
	}
}
