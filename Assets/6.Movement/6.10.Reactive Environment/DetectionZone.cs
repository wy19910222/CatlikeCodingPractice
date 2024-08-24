using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace ReactiveEnvironment {
	public class DetectionZone : MonoBehaviour {
		[SerializeField] private UnityEvent onFirstEnter, onLastExit;

		private readonly List<Collider> colliders = new List<Collider>();

		private void Awake () {
			enabled = false;
		}

		private void OnDisable () {
#if UNITY_EDITOR
			if (enabled && gameObject.activeInHierarchy) {
				return;
			}
#endif
			if (colliders.Count > 0) {
				colliders.Clear();
				onLastExit.Invoke();
			}
		}

		private void FixedUpdate () {
			for (int i = 0; i < colliders.Count; i++) {
				Collider collider = colliders[i];
				if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy) {
					colliders.RemoveAt(i--);
					if (colliders.Count == 0) {
						onLastExit.Invoke();
						enabled = false;
					}
				}
			}
		}
		
		private void OnTriggerEnter(Collider other) {
			if (colliders.Count == 0) {
				onFirstEnter.Invoke();
				enabled = true;
			}
			colliders.Add(other);
		}

		private void OnTriggerExit(Collider other) {
			if (colliders.Remove(other) && colliders.Count == 0) {
				onLastExit.Invoke();
				enabled = false;
			}
		}
	}
}
