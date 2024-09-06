using UnityEngine;
using UnityEngine.Events;

namespace CustomPlus {
	public class MaterialSelector : MonoBehaviour {
		[SerializeField] private Material[] materials;
		[SerializeField] private MeshRenderer meshRenderer;

		public void Select(int index) {
			if (meshRenderer && materials != null && index >= 0 && index < materials.Length) {
				meshRenderer.material = materials[index];
			}
		}
	}
}
