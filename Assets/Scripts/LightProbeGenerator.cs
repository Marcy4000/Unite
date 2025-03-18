using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(LightProbeGroup))]
[AddComponentMenu("Light Probe Helper/Light Probe Generator")]
public class LightProbeGenerator : MonoBehaviour
{
	[System.Serializable]
	public class LightProbeArea
	{
		public Bounds ProbeVolume = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f));
		public Vector3 Subdivisions = new Vector3(5, 5, 5);
		public Quaternion Rotation = Quaternion.identity;
		public int RandomCount = 0;
	}

	public enum LightProbePlacementType
	{
		Grid,
		Random
	}

#if UNITY_EDITOR
	public LightProbeArea LightProbeVolumes;
	public LightProbePlacementType PlacementAlgorithm;

    private void Reset()
    {
		ClearProbes();
    }

    public void ClearProbes()
	{
		if (!TryGetComponent<LightProbeGroup>(out var lprobe))
		{
			Debug.LogError("LightProbeGenerator: Must have LightProbeGroup attached!");
			return;
		}

		lprobe.probePositions = null;
	}

	public void GenProbes()
	{
		ClearProbes();

		if (!TryGetComponent(out LightProbeGroup lprobe))
		{
			gameObject.AddComponent<LightProbeGroup>();
		}

		transform.SetPositionAndRotation(LightProbeVolumes.ProbeVolume.center, LightProbeVolumes.Rotation);

		List<Vector3> probePositions = new List<Vector3>();

		if (PlacementAlgorithm == LightProbePlacementType.Grid)
		{
			probePositions.AddRange(GetProbesForVolume_Grid(LightProbeVolumes.ProbeVolume, LightProbeVolumes.Subdivisions));
		}
		else
		{
			probePositions.AddRange(GetProbesForVolume_Random(LightProbeVolumes.ProbeVolume, LightProbeVolumes.RandomCount));
		}

		lprobe.probePositions = probePositions.ToArray();
	}

	List<Vector3> GetProbesForVolume_Grid(Bounds ProbeVolume, Vector3 Subdivisions)
	{
		List<Vector3> probePositions = new List<Vector3>();

		Vector3 step = new Vector3(ProbeVolume.extents.x * 2 / Subdivisions.x, 
			ProbeVolume.extents.y * 2 / Subdivisions.y, 
			ProbeVolume.extents.z * 2 / Subdivisions.z);

		for (int x = 0; x <= Subdivisions.x; x++)
		{
			for (int y = 0; y <= Subdivisions.y; y++)
			{
				for (int z = 0; z <= Subdivisions.z; z++)
				{
					Vector3 probePos = ProbeVolume.center - ProbeVolume.extents + new Vector3(step.x * x, step.y * y, step.z * z);
					probePositions.Add(probePos - transform.position);
				}
			}
		}

		return probePositions;
	}

	List<Vector3> GetProbesForVolume_Random(Bounds ProbeVolume, int Count)
	{
		List<Vector3> probePositions = new List<Vector3>();

		for (int c = 0; c <= Count; c++)
		{
			Vector3 probePos = ProbeVolume.center + new Vector3(Random.Range(-0.5f, 0.5f) * ProbeVolume.extents.x, 
				Random.Range(-0.5f, 0.5f) * ProbeVolume.extents.y, 
				Random.Range(-0.5f, 0.5f) * ProbeVolume.extents.z);

			probePositions.Add(probePos - transform.position);
		}

		return probePositions;
	}

	void OnDrawGizmosSelected()
	{
		if (LightProbeVolumes != null)
		{
			Gizmos.color = Color.red;
			Matrix4x4 matrix = Matrix4x4.TRS(LightProbeVolumes.ProbeVolume.center, LightProbeVolumes.Rotation, Vector3.one);
			Gizmos.matrix = matrix;
			Gizmos.DrawWireCube(Vector3.zero, LightProbeVolumes.ProbeVolume.extents * 2);
		}
	}
#endif
}