#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEngine;

namespace Grass.Core
{

	public static class GrassCreator
	{
		// Constant For Creating Low Discrepancy Sequence
		private const float g = 1.32471795572f;
		private static Collider[] cullColliders = new Collider[32];

		// Fast random hash function for consistent random values based on position
		private static float GetRandomHash(Vector3 worldPosition)
		{
			uint hash = (uint)Mathf.Abs(worldPosition.x * 73856093f) ^ (uint)Mathf.Abs(worldPosition.z * 19349663f) ^ (uint)Mathf.Abs(worldPosition.y * 97531.0f);
			return (hash % 10000) * 0.0001f;
		}

		// Apply random normal nudging to create variation in grass orientation
		private static Vector3 ApplyNormalNudge(Vector3 originalNormal, float maxNudgeAmount, Vector3 worldPosition)
		{
			float nudgeX = (GetRandomHash(worldPosition + Vector3.right) - 0.5f) * 2f * maxNudgeAmount;
			float nudgeZ = (GetRandomHash(worldPosition + Vector3.forward) - 0.5f) * 2f * maxNudgeAmount;

			Quaternion rotationX = Quaternion.AngleAxis(nudgeX * Mathf.Rad2Deg, Vector3.right);
			Quaternion rotationZ = Quaternion.AngleAxis(nudgeZ * Mathf.Rad2Deg, Vector3.forward);

			Vector3 nudgedNormal = rotationZ * rotationX * originalNormal;
			return nudgedNormal.normalized;
		}

		// Unified material assignment logic
		private static void AssignGrassMaterial(ref GrassData grassData, GrassMaterialSystem materialSystem)
		{
			if (materialSystem == null || !materialSystem.IsValid())
			{
				grassData.materialIndex = 0;
				return;
			}

			// Check if this should be nudged based on probability
			float nudgeChance = GetRandomHash(grassData.position);
			if (nudgeChance < materialSystem.grassNormalNudgeProbability)
			{
				// Apply random normal nudging with default amount
				// Users can override nudge amount in their individual materials if needed
				float nudgeAmount = 0.1f;
				grassData.normal = ApplyNormalNudge(grassData.normal, nudgeAmount, grassData.position);
			}

			// Assign material index
			grassData.materialIndex = materialSystem.SelectMaterialIndex(grassData.position);
		}

		public static bool TryGeneratePoints(GrassHolder grassHolder, GameObject target, int totalGrassAmount,
			LayerMask cullMask, float normalLimit, GrassMaterialSystem materialSystem = null)
		{
			if (target.TryGetComponent(out MeshFilter sourceMesh) &&
			    target.TryGetComponent(out MeshRenderer meshRenderer))
			{
				// Pass root surface's shader variables to grass instances
				var meshMaterial = meshRenderer.sharedMaterial;
				grassHolder._rootMeshMaterial = meshMaterial;

				// Get Data from Mesh
				var triangles = sourceMesh.sharedMesh.triangles;
				var vertices = sourceMesh.sharedMesh.vertices;
				var normals = sourceMesh.sharedMesh.normals;
				var lightmapUVs = new List<Vector2>(vertices.Length);
				sourceMesh.sharedMesh.GetUVs(1, lightmapUVs);

				// cache
				var localToWorldMatrix = target.transform.localToWorldMatrix;
				var localToWorldMatrixInverseTranspose = target.transform.localToWorldMatrix.inverse.transpose;
				var objPosition = target.transform.position;

				// Transform to world for right calculations
				for (var i = 0; i < normals.Length; i++)
				{
					vertices[i] = localToWorldMatrix * vertices[i];
					normals[i] = localToWorldMatrixInverseTranspose * normals[i];
					normals[i] /= normals[i].magnitude;
				}

				var surfaceAreas = CalculateSurfaceArea(triangles, vertices, out var areas);

				// Generation Algorithm
				var grassData = new GrassData();
				Vector3 v1, v2, root;
				for (var i = 0; i < areas.Length; i++)
				{
					grassData.normal = normals[triangles[i * 3]];

					if (grassData.normal.y > 1 + normalLimit || grassData.normal.y < 1 - normalLimit)
						continue;

					var vi1 = triangles[i * 3];
					var vi2 = triangles[i * 3 + 1];
					var vi3 = triangles[i * 3 + 2];

					root = vertices[vi1];
					v1 = vertices[vi2] - root;
					v2 = vertices[vi3] - root;

					// Generating Points
					float r1, r2;
					var countGrassOnTriangle = (int)(totalGrassAmount * areas[i] / surfaceAreas);
					for (int j = 0; j < countGrassOnTriangle; j++)
					{
						r1 = (j / g) % 1;
						r2 = (j / g / g) % 1;
						if (r1 + r2 > 1)
						{
							r1 = 1 - r1;
							r2 = 1 - r2;
						}

						grassData.position = objPosition + root + r1 * v1 + r2 * v2;

						// Apply material system (handles normal nudging and material assignment)
						AssignGrassMaterial(ref grassData, materialSystem);

						if (lightmapUVs.Count != 0)
						{
							var lmRoot = lightmapUVs[vi1];
							var lmV1 = lightmapUVs[vi2] - lmRoot;
							var lmV2 = lightmapUVs[vi3] - lmRoot;
							var scaleOffset = meshRenderer.lightmapScaleOffset;

							grassData.lightmapUV = lmRoot + r1 * lmV1 + r2 * lmV2;
							grassData.lightmapUV.x = grassData.lightmapUV.x * scaleOffset.x + scaleOffset.z;
							grassData.lightmapUV.y = grassData.lightmapUV.y * scaleOffset.y + scaleOffset.w;
						}

						// Check for culling using the layer mask
						if (Physics.OverlapBoxNonAlloc(grassData.position, Vector3.one * 0.2f, cullColliders, Quaternion.identity, cullMask) > 0) {
							continue;
						}

						grassHolder.grassData.Add(grassData);
					}
				}

				grassHolder.lightmapIndex = meshRenderer.lightmapIndex;

				// Reset cleared flag since grass was generated
				grassHolder.SetGrassClearedFlag(false);
				
				grassHolder.FastSetup();
				return true;
			}

		// Handle Terrain objects
		if (target.TryGetComponent(out Terrain terrain))
			{
				var meshMaterial = terrain.materialTemplate;
				grassHolder._rootMeshMaterial = meshMaterial;

				GrassData grassData = new();

				// Computing v1, v2 and offset
				var v1 = new Vector3(1, 0, 0) * terrain.terrainData.size.x;
				var v2 = new Vector3(0, 0, 1) * terrain.terrainData.size.z;
				var root = terrain.GetPosition();

				var grassCounter = 0;

				var i = 0;
				while (grassCounter < totalGrassAmount)
				{
					i++;
					var r1 = (i / g) % 1;
					var r2 = (i / g / g) % 1;
					
					grassData.normal = terrain.terrainData.GetInterpolatedNormal(r1, r2);
					
					grassData.position = root + r1 * v1 + r2 * v2;
					grassData.position.y = terrain.SampleHeight(grassData.position) + terrain.GetPosition().y;
					
					grassData.position -= grassData.normal / 5;

					// Apply material system (handles normal nudging and material assignment)
					AssignGrassMaterial(ref grassData, materialSystem);

					var scaleOffset = terrain.lightmapScaleOffset;
					grassData.lightmapUV = new Vector2(
						r1 * scaleOffset.x + scaleOffset.z,
						r2 * scaleOffset.y + scaleOffset.w);

					// FIX: Normal should point UP enough (grass shouldn't grow on vertical surfaces)
					if (grassData.normal.y >= 1 - normalLimit)
					{
						// Check for culling using the layer mask (same as mesh generation)
						if (Physics.OverlapBoxNonAlloc(grassData.position, Vector3.one * 0.2f, cullColliders, Quaternion.identity, cullMask) > 0)
						{
							continue; // Skip this position, but don't increment counter
						}

						grassCounter++;
						grassHolder.grassData.Add(grassData);
					}
				}

				grassHolder.lightmapIndex = terrain.lightmapIndex;

				// Reset cleared flag since grass was generated
				grassHolder.SetGrassClearedFlag(false);
				
				grassHolder.FastSetup();
				return true;
			}

			return false;
		}

		private static float CalculateSurfaceArea(int[] tris, Vector3[] verts, out float[] sizes)
		{
			var res = 0f;
			var triangleCount = tris.Length / 3;
			sizes = new float[triangleCount];
			for (var i = 0; i < triangleCount; i++)
			{
				res += sizes[i] = .5f * Vector3.Cross(
					verts[tris[i * 3 + 1]] - verts[tris[i * 3]],
					verts[tris[i * 3 + 2]] - verts[tris[i * 3]]).magnitude;
			}

			return res;
		}
	}
}
#endif