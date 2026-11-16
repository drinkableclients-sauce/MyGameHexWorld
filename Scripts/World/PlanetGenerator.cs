using Godot;
using System.Collections.Generic;

[Tool]
public partial class PlanetGenerator : Node3D
{
	private float _planetRadius = 32.0f;
	[Export]
	public float PlanetRadius
	{
		get => _planetRadius;
		set { _planetRadius = value; if (IsInsideTree()) GenerateRealisticPlanet(); }
	}

	private int _planetDetail = 64;
	[Export]
	public int PlanetDetail
	{
		get => _planetDetail;
		set { _planetDetail = Mathf.Max(16, value); if (IsInsideTree()) GenerateRealisticPlanet(); }
	}

	private float _elevationHeight = 1.0f;
	[Export]
	public float ElevationHeight
	{
		get => _elevationHeight;
		set { _elevationHeight = value; if (IsInsideTree()) GenerateRealisticPlanet(); }
	}

	private float _seaLevelOffset = 0.15f;
	[Export(PropertyHint.Range, "0.0,1.0")]
	public float SeaLevelOffset
	{
		get => _seaLevelOffset;
		set { _seaLevelOffset = value; if (IsInsideTree()) GenerateRealisticPlanet(); }
	}

	private MeshInstance3D _planetMeshInstance;
	private MeshInstance3D _waterMeshInstance;

	// Datos geofísicos compartidos para consulta de celdas
	private FastNoiseLite _detailNoise;
	private FastNoiseLite _climateNoise;
	private List<Vector3> _tectonicCenters = new List<Vector3>();
	private WorldLandmassType _currentLandmassType = WorldLandmassType.Continental;

	public float AbsoluteSeaLevel => _planetRadius + (_elevationHeight * _seaLevelOffset);

	public override void _Ready()
	{
		GenerateRealisticPlanet();
	}

	private void InitializeGeophysics()
	{
		int genesisSeed = GameManager.Instance != null ? GameManager.Instance.GenesisSeed : 1337;
		var worldConfig = GameManager.Instance != null ? GameManager.Instance.CurrentWorldConfig : new WorldConfig();
		_currentLandmassType = worldConfig.LandmassType;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)genesisSeed;

		_detailNoise = new FastNoiseLite();
		_detailNoise.Seed = genesisSeed + 99;
		_detailNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_detailNoise.FractalType = FastNoiseLite.FractalTypeEnum.Ridged;
		_detailNoise.Frequency = 1.8f;

		_climateNoise = new FastNoiseLite();
		_climateNoise.Seed = genesisSeed + 777;
		_climateNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_climateNoise.Frequency = 0.5f;

		_tectonicCenters.Clear();

		if (_currentLandmassType == WorldLandmassType.Pangea)
		{
			_tectonicCenters.Add(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-0.5f, 0.5f), rng.RandfRange(-1, 1)).Normalized());
		}
		else if (_currentLandmassType == WorldLandmassType.Continental)
		{
			// Default: mínimo 2 y máximo 5 continentes separados
			int continentCount = rng.RandiRange(2, 5);
			for (int c = 0; c < continentCount; c++)
			{
				Vector3 candidate;
				int attempts = 0;
				do
				{
					candidate = new Vector3(
						rng.RandfRange(-1f, 1f),
						rng.RandfRange(-0.65f, 0.65f),
						rng.RandfRange(-1f, 1f)
					).Normalized();
					attempts++;
				}
				while (_tectonicCenters.Exists(existing => existing.Dot(candidate) > 0.25f) && attempts < 30);

				_tectonicCenters.Add(candidate);
			}
		}
		else // Archipiélago / Islas (sin depender del nombre del enum)
		{
			for (int c = 0; c < 8; c++)
			{
				_tectonicCenters.Add(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized());
			}
		}
	}

	public float SampleElevationAt(Vector3 normDir)
	{
		if (_detailNoise == null || _tectonicCenters.Count == 0)
			InitializeGeophysics();

		float structuralFactor = 0.0f;

		if (_currentLandmassType == WorldLandmassType.Pangea)
		{
			float dist = normDir.DistanceTo(_tectonicCenters[0]);
			structuralFactor = Mathf.Clamp(1.0f - (dist / 1.8f), 0.0f, 1.0f);
			structuralFactor = Mathf.Pow(structuralFactor, 0.7f);
		}
		else if (_currentLandmassType == WorldLandmassType.Continental)
		{
			float maxInfluence = 0.0f;
			foreach (var center in _tectonicCenters)
			{
				float dist = normDir.DistanceTo(center);
				float influence = Mathf.Clamp(1.0f - (dist / 0.95f), 0.0f, 1.0f);
				if (influence > maxInfluence) maxInfluence = influence;
			}
			structuralFactor = Mathf.Pow(maxInfluence, 0.85f);
		}
		else // Archipiélago / Islas
		{
			float maxInfluence = 0.0f;
			foreach (var center in _tectonicCenters)
			{
				float dist = normDir.DistanceTo(center);
				float influence = Mathf.Clamp(1.0f - (dist / 0.6f), 0.0f, 1.0f);
				if (influence > maxInfluence) maxInfluence = influence;
			}
			structuralFactor = Mathf.Pow(maxInfluence, 1.5f);
		}

		float noiseVal = _detailNoise.GetNoise3D(normDir.X * 40.0f, normDir.Y * 40.0f, normDir.Z * 40.0f);
		float finalContinentScore = structuralFactor + (noiseVal * 0.16f);

		if (finalContinentScore > 0.35f)
		{
			float landHeight = (finalContinentScore - 0.35f) / 0.65f;
			float mountainRidge = 1.0f - Mathf.Abs(_detailNoise.GetNoise3D(normDir.X * 80.0f, normDir.Y * 80.0f, normDir.Z * 80.0f));
			return (landHeight * 0.5f + Mathf.Pow(mountainRidge, 1.5f) * 0.5f) * _elevationHeight;
		}

		return -0.15f * _elevationHeight;
	}

	private void GenerateRealisticPlanet()
	{
		if (_planetMeshInstance != null) { _planetMeshInstance.QueueFree(); _planetMeshInstance = null; }
		if (_waterMeshInstance != null) { _waterMeshInstance.QueueFree(); _waterMeshInstance = null; }

		InitializeGeophysics();

		var sphereMesh = new SphereMesh();
		sphereMesh.Radius = _planetRadius;
		sphereMesh.Height = _planetRadius * 2.0f;
		sphereMesh.RadialSegments = _planetDetail * 2;
		sphereMesh.Rings = _planetDetail;

		var meshArrays = sphereMesh.GetMeshArrays();
		var vertices = (Vector3[])meshArrays[(int)Mesh.ArrayType.Vertex];
		var colors = new Color[vertices.Length];
		float absoluteSeaLevel = AbsoluteSeaLevel;

		for (int i = 0; i < vertices.Length; i++)
		{
			Vector3 normDir = vertices[i].Normalized();
			float finalElevation = SampleElevationAt(normDir);
			float currentRadius = _planetRadius + finalElevation;
			vertices[i] = normDir * currentRadius;

			float latitude = Mathf.Abs(normDir.Y);
			float moisture = (_climateNoise.GetNoise3D(normDir.X * 35.0f, normDir.Y * 35.0f, normDir.Z * 35.0f) + 1.0f) * 0.5f;
			float altitudeRatio = finalElevation / _elevationHeight;

			if (currentRadius < absoluteSeaLevel)
			{
				colors[i] = new Color(0.10f, 0.28f, 0.52f); // Océano
			}
			else if (altitudeRatio > 0.65f || latitude > 0.82f)
			{
				colors[i] = new Color(0.92f, 0.95f, 0.98f); // Cumbres nevadas / Polos
			}
			else if (altitudeRatio > 0.45f)
			{
				colors[i] = new Color(0.48f, 0.49f, 0.53f); // Montañas rocosas
			}
			else
			{
				bool isSubtropicalDesert = (latitude > 0.28f && latitude < 0.48f && moisture < 0.40f);
				bool isTropicalRainforest = (latitude <= 0.25f && moisture > 0.55f);

				if (isSubtropicalDesert)
					colors[i] = new Color(0.84f, 0.72f, 0.48f); // Desierto cálido
				else if (isTropicalRainforest)
					colors[i] = new Color(0.18f, 0.42f, 0.22f); // Selva tropical
				else if (latitude > 0.65f)
					colors[i] = new Color(0.45f, 0.55f, 0.45f); // Tundra / Taiga
				else if (moisture < 0.35f)
					colors[i] = new Color(0.65f, 0.62f, 0.38f); // Sabana / Estepa
				else
					colors[i] = new Color(0.28f, 0.58f, 0.34f); // Pradera templada
			}
		}

		meshArrays[(int)Mesh.ArrayType.Vertex] = vertices;
		meshArrays[(int)Mesh.ArrayType.Color] = colors;

		var finalArrayMesh = new ArrayMesh();
		finalArrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshArrays);

		var terrainMaterial = new StandardMaterial3D();
		terrainMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex;
		terrainMaterial.VertexColorUseAsAlbedo = true;
		terrainMaterial.Roughness = 0.7f;

		_planetMeshInstance = new MeshInstance3D();
		_planetMeshInstance.Mesh = finalArrayMesh;
		_planetMeshInstance.MaterialOverride = terrainMaterial;
		AddChild(_planetMeshInstance);

		var waterSphere = new SphereMesh();
		waterSphere.Radius = absoluteSeaLevel;
		waterSphere.Height = absoluteSeaLevel * 2.0f;
		waterSphere.RadialSegments = 32;
		waterSphere.Rings = 16;

		var waterMaterial = new StandardMaterial3D();
		waterMaterial.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		waterMaterial.AlbedoColor = new Color(0.08f, 0.25f, 0.52f, 0.85f);
		waterMaterial.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;

		_waterMeshInstance = new MeshInstance3D();
		_waterMeshInstance.Mesh = waterSphere;
		_waterMeshInstance.MaterialOverride = waterMaterial;
		AddChild(_waterMeshInstance);
	}
}
