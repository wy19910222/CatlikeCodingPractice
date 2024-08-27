using UnityEngine;
using UnityEngine.Events;

namespace Rolling {
	public class AutomaticSlider : MonoBehaviour {
		[SerializeField, Min(0.01f)] public float duration = 1f;
		[SerializeField] private bool autoReverse, smoothStep;
		[SerializeField] private UnityEvent<float> onValueChanged;

		private float value;
		
		public bool AutoReverse {
			get => autoReverse;
			set => autoReverse = value;
		}
		
		public bool Reversed { get; set; }
		
		private float SmoothedValue => 3f * value * value - 2f * value * value * value;

		private void FixedUpdate() {
			float delta = Time.deltaTime / duration;
			if (Reversed) {
				value -= delta;
				if (value <= 0f) {
					if (autoReverse) {
						value = Mathf.Min(1f, -value);
						Reversed = false;
					} else {
						value = 0f;
						enabled = false;
					}
				}
			} else {
				value += delta;
				if (value >= 1f) {
					if (autoReverse) {
						value = Mathf.Max(0f, 2f - value);
						Reversed = true;
					} else {
						value = 1f;
						enabled = false;
					}
				}
			}

			onValueChanged.Invoke(smoothStep ? SmoothedValue : value);
		}
	}
}
