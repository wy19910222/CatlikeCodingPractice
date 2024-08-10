using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace NoiseVariants {
	public abstract class Visualization : MonoBehaviour {
		private static readonly int positionsId = Shader.PropertyToID("_Positions");
		private static readonly int normalsId = Shader.PropertyToID("_Normals");
		private static readonly int configId = Shader.PropertyToID("_Config");
	
		[SerializeField]
		private Mesh instanceMesh;
		[SerializeField]
		private Material material;
		[SerializeField]
		private Shapes.Shape shape;
		[SerializeField, Range(0.1f, 10f)]
		private float instanceScale = 2f;
		[SerializeField, Range(1, 512)]
		private int resolution = 64;
		[SerializeField, Range(-0.5f, 0.5f)]
		private float displacement = 0.1f;
	
		private NativeArray<float3x4> positions;
		private NativeArray<float3x4> normals;
		private ComputeBuffer positionsBuffer;
		private ComputeBuffer normalsBuffer;
		private MaterialPropertyBlock propertyBlock;
		
		private bool isDirty;
		private Bounds bounds;
		
		protected abstract void EnableVisualization(int dataLength, MaterialPropertyBlock propertyBlock);

		protected abstract void DisableVisualization();

		protected abstract void UpdateVisualization (NativeArray<float3x4> positions, int resolution, JobHandle handle);

		void OnValidate () {
			if (positionsBuffer != null && enabled) {
				OnDisable();
				OnEnable();
			}
		}
		
		void OnEnable () {
			isDirty = true;
			
			int length = resolution * resolution;
			length = length / 4 + (length & 1);
			positions = new NativeArray<float3x4>(length, Allocator.Persistent);
			normals = new NativeArray<float3x4>(length, Allocator.Persistent);
			positionsBuffer = new ComputeBuffer(length * 4, 3 * 4);
			normalsBuffer = new ComputeBuffer(length * 4, 3 * 4);
	
			propertyBlock ??= new MaterialPropertyBlock();
			EnableVisualization(length, propertyBlock);
			propertyBlock.SetBuffer(positionsId, positionsBuffer);
			propertyBlock.SetBuffer(normalsId, normalsBuffer);
			propertyBlock.SetVector(configId, new Vector4(resolution, instanceScale / resolution, displacement));
		}
		
		void OnDisable () {
			positions.Dispose();
			normals.Dispose();
			positionsBuffer.Release();
			normalsBuffer.Release();
			positionsBuffer = null;
			normalsBuffer = null;
			DisableVisualization();
		}
		
		void Update () {
			Transform trans = transform;
			if (isDirty || trans.hasChanged) {
				isDirty = false;
				trans.hasChanged = false;
	
				UpdateVisualization(
						positions, resolution,
						Shapes.shapeJobs[(int)shape](
								positions, normals, resolution, transform.localToWorldMatrix, default
						)
				);
	
				positionsBuffer.SetData(positions.Reinterpret<float3>(3 * 4 * 4));
				normalsBuffer.SetData(normals.Reinterpret<float3>(3 * 4 * 4));
				
				bounds = new Bounds(
						trans.position,
						math.float3(2f * math.cmax(math.abs(trans.lossyScale)) + displacement)
				);
			}
			Graphics.DrawMeshInstancedProcedural(instanceMesh, 0, material, bounds, resolution * resolution, propertyBlock);
		}
	}
}
