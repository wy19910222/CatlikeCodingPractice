using TMPro;
using UnityEngine;

public class FrameRateCounter : MonoBehaviour {
	public enum DisplayMode { FPS, MS }
	
	public TextMeshProUGUI display;

	public DisplayMode displayMode = DisplayMode.FPS;

	[Range(0.1f, 2f)]
	public float sampleDuration = 1f;
	
	private int frames;
	private float duration;
	private float bestDuration = float.MaxValue;
	private float worstDuration;
	
	private void Update () {
		float frameDuration = Time.unscaledDeltaTime;
		frames += 1;
		duration += frameDuration;
		if (frameDuration < bestDuration) {
			bestDuration = frameDuration;
		}
		if (frameDuration > worstDuration) {
			worstDuration = frameDuration;
		}
		
		if (duration >= sampleDuration) {
			// if (displayMode == DisplayMode.FPS) {
			// 	display.SetText($"FPS\n{1F / bestDuration:0}\n{frames / duration:0}\n{1F / worstDuration:0}");
			// } else {
			// 	display.SetText($"FPS\n{1000F * bestDuration:1}\n{1000F * duration / frames:1}\n{1000F * worstDuration:1}");
			// }
			if (displayMode == DisplayMode.FPS) {
				display.SetText("FPS\n{0:0}\n{1:0}\n{2:0}", 1F / Mathf.Max(bestDuration, 0.0001F), frames / duration, 1F / worstDuration);
			} else {
				display.SetText("FPS\n{0:1}\n{1:1}\n{2:1}", 1000F * bestDuration, 1000F * duration / frames, 1000F * worstDuration);
			}
			frames = 0;
			duration = 0f;
			bestDuration = float.MaxValue;
			worstDuration = 0f;
		}
	}
}
