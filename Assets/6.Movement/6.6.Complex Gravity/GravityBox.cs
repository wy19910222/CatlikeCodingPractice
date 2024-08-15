using UnityEngine;

namespace ComplexGravity {
	public class GravityBox : GravitySource {
		[SerializeField] private float gravity = 9.81f;
		[SerializeField] private Vector3 boundaryDistance = Vector3.one;
		[SerializeField, Min(0f)] private float innerDistance, innerFalloffDistance;
		[SerializeField, Min(0f)] private float outerDistance, outerFalloffDistance;

		private float innerFalloffFactor, outerFalloffFactor;

		public override Vector3 GetGravity(Vector3 position) {
			position = transform.InverseTransformDirection(position - transform.position);
			Vector3 vector = Vector3.zero;
			int outside = 0;
			if (position.x > boundaryDistance.x) {
				vector.x = boundaryDistance.x - position.x;
				outside = 1;
			}
			else if (position.x < -boundaryDistance.x) {
				vector.x = -boundaryDistance.x - position.x;
				outside = 1;
			}

			if (position.y > boundaryDistance.y) {
				vector.y = boundaryDistance.y - position.y;
				outside += 1;
			}
			else if (position.y < -boundaryDistance.y) {
				vector.y = -boundaryDistance.y - position.y;
				outside += 1;
			}

			if (position.z > boundaryDistance.z) {
				vector.z = boundaryDistance.z - position.z;
				outside += 1;
			}
			else if (position.z < -boundaryDistance.z) {
				vector.z = -boundaryDistance.z - position.z;
				outside += 1;
			}
			
			if (outside > 0) {
				float distance = outside == 1 ? Mathf.Abs(vector.x + vector.y + vector.z) : vector.magnitude;
				if (distance > outerFalloffDistance) {
					return Vector3.zero;
				}
				float g = gravity / distance;
				if (distance > outerDistance) {
					g *= 1f - (distance - outerDistance) * outerFalloffFactor;
				}
				return transform.TransformDirection(g * vector);
			}
			
			Vector3 distances;
			distances.x = boundaryDistance.x - Mathf.Abs(position.x);
			distances.y = boundaryDistance.y - Mathf.Abs(position.y);
			distances.z = boundaryDistance.z - Mathf.Abs(position.z);
			if (distances.x < distances.y) {
				if (distances.x < distances.z) {
					vector.x = GetGravityComponent(position.x, distances.x);
				}
				else {
					vector.z = GetGravityComponent(position.z, distances.z);
				}
			}
			else if (distances.y < distances.z) {
				vector.y = GetGravityComponent(position.y, distances.y);
			}
			else {
				vector.z = GetGravityComponent(position.z, distances.z);
			}
			return transform.TransformDirection(vector);
		}

		private float GetGravityComponent(float coordinate, float distance) {
			if (distance > innerFalloffDistance) {
				return 0f;
			}
			float g = gravity;
			if (distance > innerDistance) {
				g *= 1f - (distance - innerDistance) * innerFalloffFactor;
			}
			return coordinate > 0f ? -g : g;
		}

		private void Awake() {
			OnValidate();
		}

		private void OnValidate() {
			boundaryDistance = Vector3.Max(boundaryDistance, Vector3.zero);
			float maxInner = Mathf.Min(Mathf.Min(boundaryDistance.x, boundaryDistance.y), boundaryDistance.z);
			innerDistance = Mathf.Min(innerDistance, maxInner);
			innerFalloffDistance = Mathf.Max(Mathf.Min(innerFalloffDistance, maxInner), innerDistance);
			outerFalloffDistance = Mathf.Max(outerFalloffDistance, outerDistance);
			
			innerFalloffFactor = 1f / (innerFalloffDistance - innerDistance);
			outerFalloffFactor = 1f / (outerFalloffDistance - outerDistance);
		}

		private void OnDrawGizmos() {
			Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
			Vector3 size;
			if (innerFalloffDistance > innerDistance) {
				Gizmos.color = Color.cyan;
				size.x = 2f * (boundaryDistance.x - innerFalloffDistance);
				size.y = 2f * (boundaryDistance.y - innerFalloffDistance);
				size.z = 2f * (boundaryDistance.z - innerFalloffDistance);
				Gizmos.DrawWireCube(Vector3.zero, size);
			}
			if (innerDistance > 0f) {
				Gizmos.color = Color.yellow;
				size.x = 2f * (boundaryDistance.x - innerDistance);
				size.y = 2f * (boundaryDistance.y - innerDistance);
				size.z = 2f * (boundaryDistance.z - innerDistance);
				Gizmos.DrawWireCube(Vector3.zero, size);
			}
			Gizmos.color = Color.red;
			Gizmos.DrawWireCube(Vector3.zero, 2f * boundaryDistance);
			
			if (outerDistance > 0f) {
				Gizmos.color = Color.yellow;
				// DrawGizmosOuterCube(outerDistance);
				DrawExtendedWireCube(Vector3.zero, boundaryDistance * 2, outerDistance);
			}
			if (outerFalloffDistance > outerDistance) {
				Gizmos.color = Color.cyan;
				// DrawGizmosOuterCube(outerFalloffDistance);
				DrawExtendedWireCube(Vector3.zero, boundaryDistance * 2, outerFalloffDistance);
			}
		}

		private void DrawGizmosOuterCube(float distance) {
			Vector3 a, b, c, d;
			a.y = b.y = boundaryDistance.y;
			d.y = c.y = -boundaryDistance.y;
			b.z = c.z = boundaryDistance.z;
			d.z = a.z = -boundaryDistance.z;
			a.x = b.x = c.x = d.x = boundaryDistance.x + distance;
			DrawGizmosRect(a, b, c, d);
			a.x = b.x = c.x = d.x = -a.x;
			DrawGizmosRect(a, b, c, d);

			a.x = d.x = boundaryDistance.x;
			b.x = c.x = -boundaryDistance.x;
			a.z = b.z = boundaryDistance.z;
			c.z = d.z = -boundaryDistance.z;
			a.y = b.y = c.y = d.y = boundaryDistance.y + distance;
			DrawGizmosRect(a, b, c, d);
			a.y = b.y = c.y = d.y = -a.y;
			DrawGizmosRect(a, b, c, d);

			a.x = d.x = boundaryDistance.x;
			b.x = c.x = -boundaryDistance.x;
			a.y = b.y = boundaryDistance.y;
			c.y = d.y = -boundaryDistance.y;
			a.z = b.z = c.z = d.z = boundaryDistance.z + distance;
			DrawGizmosRect(a, b, c, d);
			a.z = b.z = c.z = d.z = -a.z;
			DrawGizmosRect(a, b, c, d);
			
			distance *= 0.5773502692f;
			Vector3 size = boundaryDistance;
			size.x = 2f * (size.x + distance);
			size.y = 2f * (size.y + distance);
			size.z = 2f * (size.z + distance);
			Gizmos.DrawWireCube(Vector3.zero, size);
		}

		private void DrawGizmosRect(Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
		}

		private static void DrawExtendedWireCube(Vector3 center, Vector3 size, float extent, bool detailed = false) {
			if (extent <= 0) {
				Gizmos.DrawWireCube(center, size);
				return;
			}
			size *= 0.5F;
			const float INV_SQRT_2 = 0.7071F;
			
			// 6个面
			Vector3 a, b, c, d;
			a.y = b.y = size.y;
			d.y = c.y = -size.y;
			b.z = c.z = size.z;
			d.z = a.z = -size.z;
			a.x = b.x = c.x = d.x = size.x + extent;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
			a.x = b.x = c.x = d.x = -a.x;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
			a.x = b.x = size.x;
			d.x = c.x = -size.x;
			b.z = c.z = size.z;
			d.z = a.z = -size.z;
			a.y = b.y = c.y = d.y = size.y + extent;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
			a.y = b.y = c.y = d.y = -a.y;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
			a.x = b.x = size.x;
			d.x = c.x = -size.x;
			b.y = c.y = size.y;
			d.y = a.y = -size.y;
			a.z = b.z = c.z = d.z = size.z + extent;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);
			a.z = b.z = c.z = d.z = -a.z;
			Gizmos.DrawLine(a, b);
			Gizmos.DrawLine(b, c);
			Gizmos.DrawLine(c, d);
			Gizmos.DrawLine(d, a);

			// 8个角上的圆弧三角形
			Vector3 cornerCenter = size;
			Vector3[] cornerTypes = {
				Vector3.one,
				new Vector3(-1, 1, 1),
				new Vector3(1, -1, 1),
				new Vector3(-1, -1, 1),
				new Vector3(1, 1, -1),
				new Vector3(-1, 1, -1),
				new Vector3(1, -1, -1),
				-Vector3.one,
			};
			const int ARC_SEGMENT_COUNT = 10;
			for (int axis = 0; axis < 3; ++axis) {
				Vector3 rotateAxis = Vector3.zero;
				rotateAxis[axis] = 1;
				Vector3 direction = Vector3.zero;
				direction[axis == 2 ? 0 : axis + 1] = extent;
				Vector3 temp1 = cornerCenter + direction;
				for (int i = 1; i <= ARC_SEGMENT_COUNT; ++i) {
					Vector3 temp2 = cornerCenter + Quaternion.AngleAxis(i * 90F / ARC_SEGMENT_COUNT, rotateAxis) * direction;
					for (int j = 0; j < 8; ++j) {
						Gizmos.DrawLine(Vector3.Scale(temp1, cornerTypes[j]), Vector3.Scale(temp2, cornerTypes[j]));
					}
					temp1 = temp2;
				}
			}

			if (detailed) {
				// 12条棱
				Vector3 e, f;
				float extent2 = extent * INV_SQRT_2;
				
				a.x = -size.x;
				b.x = size.x;
				c.x = d.x = e.x = f.x = size.x + extent2;
				c.y = -size.y;
				d.y = size.y;
				a.y = b.y = e.y = f.y = size.y + extent2;
				e.z = -size.z;
				f.z = size.z;
				a.z = b.z = c.z = d.z = size.z + extent2;
				Gizmos.DrawLine(a, b);
				Gizmos.DrawLine(c, d);
				Gizmos.DrawLine(e, f);
				c.x = d.x = e.x = f.x = -c.x;
				a.z = b.z = c.z = d.z = -a.z;
				Gizmos.DrawLine(a, b);
				Gizmos.DrawLine(c, d);
				Gizmos.DrawLine(e, f);
				c.x = d.x = e.x = f.x = -c.x;
				a.y = b.y = e.y = f.y = -a.y;
				Gizmos.DrawLine(a, b);
				Gizmos.DrawLine(c, d);
				Gizmos.DrawLine(e, f);
				c.x = d.x = e.x = f.x = -c.x;
				a.z = b.z = c.z = d.z = -a.z;
				Gizmos.DrawLine(a, b);
				Gizmos.DrawLine(c, d);
				Gizmos.DrawLine(e, f);
				
				// 8个角上的圆弧棱
				for (int axis = 0; axis < 3; ++axis) {
					Vector3 rotateAxis = Vector3.zero;
					rotateAxis[axis] = INV_SQRT_2;
					rotateAxis[axis == 2 ? 0 : axis + 1] = -INV_SQRT_2;
					Vector3 direction = Vector3.zero;
					direction[axis] = extent2;
					direction[axis == 2 ? 0 : axis + 1] = extent2;
					Vector3 temp1 = cornerCenter + direction;
					for (int i = 1; i <= ARC_SEGMENT_COUNT; ++i) {
						Vector3 temp2 = cornerCenter + Quaternion.AngleAxis(i * 90F / ARC_SEGMENT_COUNT, rotateAxis) * direction;
						for (int j = 0; j < 8; ++j) {
							Gizmos.DrawLine(Vector3.Scale(temp1, cornerTypes[j]), Vector3.Scale(temp2, cornerTypes[j]));
						}
						temp1 = temp2;
					}
				}
			}
		}
	}
}
