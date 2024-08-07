using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class Fractal_Jobs_Optimized_Parallel : MonoBehaviour {
	private struct FractalPart {
		public float3 direction, worldPosition;
		public quaternion rotation, worldRotation;
		public float spinAngle;
	}

	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	private struct UpdateFractalLevelJob : IJobFor {
		public float spinAngleDelta;
		public float scale;
		
		[ReadOnly]
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;

		[WriteOnly]
		public NativeArray<float3x4> matrices;

		public void Execute(int i) {
			FractalPart parent = parents[i / 5];
			FractalPart part = parts[i];
			part.spinAngle += spinAngleDelta;
			part.worldRotation = math.mul(parent.worldRotation, math.mul(part.rotation, quaternion.RotateY(part.spinAngle)));
			part.worldPosition = parent.worldPosition + math.mul(parent.worldRotation, (1.5f * scale * part.direction));
			parts[i] = part;
			float3x3 r = math.float3x3(part.worldRotation) * scale;
			matrices[i] = math.float3x4(r.c0, r.c1, r.c2, part.worldPosition);
		}
	}
	
	private static readonly int matricesId = Shader.PropertyToID("_Matrices");
	private static float3[] directions = {
		Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back,
		math.up(), math.right(), math.left(), math.forward(), math.back()
	};
	private static quaternion[] rotations = {
		quaternion.identity,
		quaternion.RotateZ(-0.5f * math.PI), quaternion.RotateZ(0.5f * math.PI),
		quaternion.RotateX(0.5f * math.PI), quaternion.RotateX(-0.5f * math.PI)
	};
	private static MaterialPropertyBlock propertyBlock;
	
	[Range(1, 8)]
	public int depth = 4;
	public Mesh mesh;
	public Material material;
	
	private NativeArray<FractalPart>[] parts;
	private NativeArray<float3x4>[] matrices;
	private ComputeBuffer[] matricesBuffers;

	void OnValidate () {
		if (parts != null && enabled) {
			OnDisable();
			OnEnable();
		}
	}
	
	void OnEnable () {
		parts = new NativeArray<FractalPart>[depth];
		matrices = new NativeArray<float3x4>[depth];
		matricesBuffers = new ComputeBuffer[depth];
		int stride = 12 * 4;
		for (int i = 0, length = 1; i < parts.Length; i++, length *= 5) {
			parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
			matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
			matricesBuffers[i] = new ComputeBuffer(length, stride);
		}
		parts[0][0] = CreatePart(0);
		for (int li = 1; li < parts.Length; li++) {
			NativeArray<FractalPart> levelParts = parts[li];
			for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
				for (int ci = 0; ci < 5; ci++) {
					levelParts[fpi + ci] = CreatePart(ci);
				}
			}
		}
		propertyBlock ??= new MaterialPropertyBlock();
	}
	
	void OnDisable () {
		for (int i = 0; i < matricesBuffers.Length; i++) {
			parts[i].Dispose();
			matrices[i].Dispose();
			matricesBuffers[i].Release();
		}
		parts = null;
		matrices = null;
		matricesBuffers = null;
	}
	
	void Update () {
		float spinAngleDelta = 0.125f * math.PI * Time.deltaTime;
		
		FractalPart rootPart = parts[0][0];
		rootPart.spinAngle += spinAngleDelta;
		rootPart.worldRotation = math.mul(transform.rotation, math.mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle)));
		rootPart.worldPosition = transform.position;
		parts[0][0] = rootPart;
		float objectScale = transform.lossyScale.x;
		float3x3 r = math.float3x3(rootPart.worldRotation) * objectScale;
		matrices[0][0] = math.float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);
		float scale = objectScale;
		JobHandle jobHandle = default;
		for (int li = 1; li < parts.Length; li++) {
			scale *= 0.5f;
			jobHandle = new UpdateFractalLevelJob() {
				spinAngleDelta = spinAngleDelta,
				scale = scale,
				parents = parts[li - 1],
				parts = parts[li],
				matrices = matrices[li]
			}.ScheduleParallel(parts[li].Length, 5, jobHandle);
		}
		jobHandle.Complete();
		Bounds bounds = new Bounds(rootPart.worldPosition, 3f * objectScale * Vector3.one);
		for (int i = 0; i < matricesBuffers.Length; i++) {
			ComputeBuffer buffer = matricesBuffers[i];
			buffer.SetData(matrices[i]);
			propertyBlock.SetBuffer(matricesId, buffer);
			Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
		}
	}

	FractalPart CreatePart(int childIndex) => new FractalPart() {
		direction = directions[childIndex],
		rotation = rotations[childIndex]
	};
}
