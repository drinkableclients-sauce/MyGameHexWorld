using Godot;
using System.Collections.Generic;

public enum LocalTerrainType
{
	Agua,
	Arena,
	Pradera,
	Selva,
	Desierto,
	Tundra,
	RocaAcantilado,
	Nieve
}

public enum LocalZoneState
{
	Libre,
	Cultivo,
	Bosque,
	RocaRecurso,
	Edificado
}

public class LocalTriangleData
{
	public int Index;
	public Vector3 V1, V2, V3;
	public Vector3 Center;
	public Vector3 Normal;
	public float AverageHeight;
	public float Slope; // 0 = plano, 1 = vertical
	public int WaterDistance; // 0 = agua, 1..N = pasos de triángulo desde el agua
	public LocalTerrainType TerrainType;
	public LocalZoneState ZoneState = LocalZoneState.Libre;
	public string ResourceHint = "Ninguno";

	public bool IsBuildable =>
		TerrainType != LocalTerrainType.Agua &&
		TerrainType != LocalTerrainType.RocaAcantilado &&
		Slope < 0.35f &&
		ZoneState == LocalZoneState.Libre;
}

public partial class LocalMapGenerator : Node3D
{
	[Export] public float MapRadius { get; set; } = 1400.0f;
	[Export] public float TerrainDepth { get; set; } = 220.0f;
	[Export] public int Subdivisions { get; set; } = 96;
	[Export] public int BeachWidthTriangles { get; set; } = 2; // Ancho de playa en triángulos desde el agua

	// Lista maestra con todos los triángulos del diorama para gameplay (recursos, cultivos, selección)
	public List<LocalTriangleData> Triangles { get; private set; } = new List<LocalTriangleData>();

	private MeshInstance3D _terrainMeshInstance;
	private MeshInstance3D _wireframeMeshInstance;
	private MeshInstance3D _waterMeshInstance;

	private FastNoiseLite _macroDetailNoise;
	private FastNoiseLite _macroClimateNoise;
	private FastNoiseLite _microLocalNoise;
	private List<Vector3> _tectonicCenters = new List<Vector3>();
	private WorldLandmassType _landmassType = WorldLandmassType.Continental;

	public override void _Ready()
	{
		BasalKey activeCell = GameManager.Instance != null ? GameManager.Instance.ActiveCell : null;

		if (activeCell == null)
		{
			activeCell = new BasalKey();
			activeCell.Initialize(25, 1337, Vector3.Right);
			activeCell.Biome = "Selva / Trópico";
			activeCell.BaseElevation = 0.31f;
		}

		int sides = (activeCell.CornerDirections != null && activeCell.CornerDirections.Length >= 5)
			? activeCell.CornerDirections.Length
			: (activeCell.Id < 12 ? 5 : 6);

		InitializeMacroGeophysics();
		BuildSyncedTriangulatedDiorama(sides, MapRadius, TerrainDepth, activeCell);

		var cameraRig = GetNodeOrNull<LocalTacticalCamera>("CameraRig");
		if (cameraRig != null)
		{
			cameraRig.MaxBoundaryRadius = MapRadius * 0.88f;
		}
	}

	private void InitializeMacroGeophysics()
	{
		int genesisSeed = GameManager.Instance != null ? GameManager.Instance.GenesisSeed : 1337;
		var worldConfig = GameManager.Instance != null ? GameManager.Instance.CurrentWorldConfig : new WorldConfig();
		_landmassType = worldConfig.LandmassType;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)genesisSeed;

		// Solo 2 octavas para crestas montañosas limpias sin ruido granular entre triángulos vecinos
		_macroDetailNoise = new FastNoiseLite
		{
			Seed = genesisSeed + 99,
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			FractalType = FastNoiseLite.FractalTypeEnum.Ridged,
			FractalOctaves = 2,
			Frequency = 1.4f
		};

		_macroClimateNoise = new FastNoiseLite
		{
			Seed = genesisSeed + 777,
			NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex,
			FractalOctaves = 2,
			Frequency = 0.5f
		};

		// Ondulación local muy amplia (cientos de metros por colina)
		_microLocalNoise = new FastNoiseLite
		{
			Seed = genesisSeed + 314,
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			FractalType = FastNoiseLite.FractalTypeEnum.Fbm,
			FractalOctaves = 2,
			Frequency = 0.00035f
		};

		_tectonicCenters.Clear();
		if (_landmassType == WorldLandmassType.Pangea)
		{
			_tectonicCenters.Add(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-0.5f, 0.5f), rng.RandfRange(-1, 1)).Normalized());
		}
		else if (_landmassType == WorldLandmassType.Continental)
		{
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
		else
		{
			for (int c = 0; c < 8; c++)
			{
				_tectonicCenters.Add(new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized());
			}
		}
	}

	private float GetDifficultyMountainScale()
	{
		if (GameManager.Instance == null || GameManager.Instance.CurrentWorldConfig == null)
			return 1.0f;

		string diffName = GameManager.Instance.CurrentWorldConfig.Difficulty.ToString().ToLower();

		// Ajuste porcentual sutil solo sobre la altura de las montañas, sin hundir ni exagerar las llanuras
		if (diffName.Contains("easy") || diffName.Contains("facil") || diffName.Contains("fácil") || diffName.Contains("peace"))
			return 0.84f; // ~16% más suave
		if (diffName.Contains("hard") || diffName.Contains("dificil") || diffName.Contains("difícil") || diffName.Contains("extreme") || diffName.Contains("extremo"))
			return 1.18f; // ~18% más montañoso

		return 1.0f; // Normal
	}

	private float SamplePlanetElevation(Vector3 normDir, float elevationHeight)
	{
		float structuralFactor = 0.0f;

		if (_landmassType == WorldLandmassType.Pangea)
		{
			float dist = normDir.DistanceTo(_tectonicCenters[0]);
			structuralFactor = Mathf.Pow(Mathf.Clamp(1.0f - (dist / 1.8f), 0.0f, 1.0f), 0.7f);
		}
		else if (_landmassType == WorldLandmassType.Continental)
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
		else
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

		// Multiplicadores suaves (2.5f y 5.0f) para cordilleras fluidas a 160-256 subdivisiones
		float noiseVal = _macroDetailNoise.GetNoise3D(normDir.X * 2.5f, normDir.Y * 2.5f, normDir.Z * 2.5f);
		float finalContinentScore = structuralFactor + (noiseVal * 0.16f);

		if (finalContinentScore > 0.35f)
		{
			float landHeight = (finalContinentScore - 0.35f) / 0.65f;
			float mountainRidge = 1.0f - Mathf.Abs(_macroDetailNoise.GetNoise3D(normDir.X * 5.0f, normDir.Y * 5.0f, normDir.Z * 5.0f));
			return (landHeight * 0.60f + Mathf.Pow(mountainRidge, 2.0f) * 0.40f) * elevationHeight;
		}

		return -0.15f * elevationHeight;
	}

	private Color GetColorForTerrainType(LocalTerrainType type)
	{
		return type switch
		{
			LocalTerrainType.Agua => new Color(0.11f, 0.28f, 0.50f),
			LocalTerrainType.Arena => new Color(0.84f, 0.77f, 0.54f),
			LocalTerrainType.Selva => new Color(0.18f, 0.43f, 0.22f),
			LocalTerrainType.Desierto => new Color(0.83f, 0.71f, 0.46f),
			LocalTerrainType.Tundra => new Color(0.45f, 0.55f, 0.45f),
			LocalTerrainType.RocaAcantilado => new Color(0.47f, 0.48f, 0.52f),
			LocalTerrainType.Nieve => new Color(0.92f, 0.95f, 0.98f),
			_ => new Color(0.28f, 0.58f, 0.34f) // Pradera
		};
	}

	private LocalTerrainType ClassifyContinentalBiome(Vector3 sphereDir, float macroElev, float elevationHeight)
	{
		float latitude = Mathf.Abs(sphereDir.Y);
		float moisture = (_macroClimateNoise.GetNoise3D(sphereDir.X * 35.0f, sphereDir.Y * 35.0f, sphereDir.Z * 35.0f) + 1.0f) * 0.5f;
		float altRatio = macroElev / Mathf.Max(0.1f, elevationHeight);

		if (altRatio > 0.68f || latitude > 0.82f)
			return LocalTerrainType.Nieve;
		if (altRatio > 0.48f)
			return LocalTerrainType.RocaAcantilado;

		bool isSubtropicalDesert = (latitude > 0.28f && latitude < 0.48f && moisture < 0.40f);
		bool isTropicalRainforest = (latitude <= 0.25f && moisture > 0.55f);

		if (isSubtropicalDesert)
			return LocalTerrainType.Desierto;
		if (isTropicalRainforest)
			return LocalTerrainType.Selva;
		if (latitude > 0.65f)
			return LocalTerrainType.Tundra;

		return LocalTerrainType.Pradera;
	}

	private void BuildSyncedTriangulatedDiorama(int sides, float radius, float depth, BasalKey dna)
	{
		Triangles.Clear();

		int N = Mathf.Clamp(Subdivisions, 8, 512);
		float seaThreshold = dna.ElevationHeight * dna.SeaLevelOffset;
		float mountainScale = GetDifficultyMountainScale();

		Vector3 centerSphereDir = Vector3.Right;
		if (dna.CornerDirections != null && dna.CornerDirections.Length > 0)
		{
			Vector3 sum = Vector3.Zero;
			foreach (var c in dna.CornerDirections) sum += c;
			centerSphereDir = sum.Normalized();
		}
		else if (dna.OriginCoordinates.LengthSquared() > 0.01f)
		{
			centerSphereDir = dna.OriginCoordinates.Normalized();
		}

		Vector3[] sphereCorners = new Vector3[sides];
		if (dna.CornerDirections != null && dna.CornerDirections.Length == sides)
		{
			sphereCorners = dna.CornerDirections;
		}
		else
		{
			Vector3 tangent = centerSphereDir.Cross(Vector3.Up);
			if (tangent.LengthSquared() < 0.001f) tangent = centerSphereDir.Cross(Vector3.Right);
			tangent = tangent.Normalized();
			Vector3 bitangent = centerSphereDir.Cross(tangent).Normalized();

			for (int i = 0; i < sides; i++)
			{
				float ang = i * Mathf.Tau / sides;
				sphereCorners[i] = (centerSphereDir + (tangent * Mathf.Cos(ang) + bitangent * Mathf.Sin(ang)) * 0.42f).Normalized();
			}
		}

		Vector3 upRef = Vector3.Up;
		Vector3 eastAxis = upRef.Cross(centerSphereDir);
		if (eastAxis.LengthSquared() < 0.001f) eastAxis = Vector3.Right;
		eastAxis = eastAxis.Normalized();
		Vector3 northAxis = centerSphereDir.Cross(eastAxis).Normalized();

		Vector2[] localCorners2D = new Vector2[sides];
		for (int i = 0; i < sides; i++)
		{
			Vector3 rel = sphereCorners[i] - centerSphereDir;
			float px = rel.Dot(eastAxis);
			float pz = -rel.Dot(northAxis);
			localCorners2D[i] = new Vector2(px, pz).Normalized() * radius;
		}

		// Mapa espacial por vértice compartido para detectar vecinos y propagar proximidad al agua en 1 pase
		var vertexToTriangles = new Dictionary<Vector2I, List<int>>();
		var baseBiomes = new List<LocalTerrainType>();

		Vector2I QuantizeXZ(Vector3 p) => new Vector2I(Mathf.RoundToInt(p.X * 10.0f), Mathf.RoundToInt(p.Z * 10.0f));

		void RegisterTriangle(Vector3 a, Vector3 b, Vector3 c, Vector3 avgSphereDir, float avgMacroElev)
		{
			Vector3 faceNormal = (b - a).Cross(c - a).Normalized();
			if (faceNormal.Y < 0) faceNormal = -faceNormal;

			float crossY = (b.X - a.X) * (c.Z - a.Z) - (b.Z - a.Z) * (c.X - a.X);
			Vector3 v1 = a;
			Vector3 v2 = crossY > 0 ? b : c;
			Vector3 v3 = crossY > 0 ? c : b;

			float avgY = (v1.Y + v2.Y + v3.Y) / 3.0f;
			float slope = 1.0f - Mathf.Clamp(faceNormal.Y, 0.0f, 1.0f);

			int idx = Triangles.Count;
			bool isWater = avgY <= 0.0f;

			var triData = new LocalTriangleData
			{
				Index = idx,
				V1 = v1,
				V2 = v2,
				V3 = v3,
				Center = (v1 + v2 + v3) / 3.0f,
				Normal = faceNormal,
				AverageHeight = avgY,
				Slope = slope,
				WaterDistance = isWater ? 0 : int.MaxValue
			};

			Triangles.Add(triData);
			baseBiomes.Add(ClassifyContinentalBiome(avgSphereDir, avgMacroElev, dna.ElevationHeight));

			// Registrar los 3 vértices para vecindad topológica instantánea
			foreach (var v in new[] { v1, v2, v3 })
			{
				Vector2I key = QuantizeXZ(v);
				if (!vertexToTriangles.TryGetValue(key, out var list))
				{
					list = new List<int>(6);
					vertexToTriangles[key] = list;
				}
				list.Add(idx);
			}
		}

		// --- PASO 1: Calcular geometría y alturas de todos los sectores ---
		var sectorGrids = new Vector3[sides][,];

		for (int s = 0; s < sides; s++)
		{
			int nextS = (s + 1) % sides;
			Vector2 pCenter2D = Vector2.Zero;
			Vector2 pCorner1_2D = localCorners2D[s];
			Vector2 pCorner2_2D = localCorners2D[nextS];

			Vector3 sCenter = centerSphereDir;
			Vector3 sCorner1 = sphereCorners[s];
			Vector3 sCorner2 = sphereCorners[nextS];

			Vector3[,] gridPos = new Vector3[N + 1, N + 1];
			Vector3[,] gridSphere = new Vector3[N + 1, N + 1];
			float[,] gridMacroElev = new float[N + 1, N + 1];

			for (int i = 0; i <= N; i++)
			{
				for (int j = 0; j <= N - i; j++)
				{
					float u = (float)i / N;
					float v = (float)j / N;
					float w = 1.0f - u - v;

					Vector2 pos2D = pCenter2D * w + pCorner1_2D * u + pCorner2_2D * v;
					Vector3 sphereDir = (sCenter * w + sCorner1 * u + sCorner2 * v).Normalized();

					float macroElev = SamplePlanetElevation(sphereDir, dna.ElevationHeight);
					float relativeToSea = macroElev - seaThreshold;

					float finalY;
					if (relativeToSea <= 0.0f)
					{
						finalY = relativeToSea * 65.0f + _microLocalNoise.GetNoise2D(pos2D.X, pos2D.Y) * 2.5f;
					}
					else
					{
						float normalizedLand = Mathf.Clamp(relativeToSea / Mathf.Max(0.2f, dna.ElevationHeight * 0.75f), 0.0f, 1.5f);
						// Curva más pronunciada (1.75f) para aplanar valles bajos y concentrar altura solo en las cumbres
						float plainToMountain = Mathf.Pow(normalizedLand, 1.75f) * 85.0f * mountainScale;
						float gentleHills = (_microLocalNoise.GetNoise2D(pos2D.X, pos2D.Y) + 0.5f) * 6.0f * mountainScale;

						float shoreBlend = Mathf.Clamp(relativeToSea * 10.0f, 0.0f, 1.0f);
						finalY = (plainToMountain + gentleHills) * shoreBlend + (relativeToSea * 20.0f * (1.0f - shoreBlend));
					}

					gridPos[i, j] = new Vector3(pos2D.X, finalY, pos2D.Y);
					gridSphere[i, j] = sphereDir;
					gridMacroElev[i, j] = macroElev;
				}
			}

			sectorGrids[s] = gridPos;

			for (int i = 0; i < N; i++)
			{
				for (int j = 0; j < N - i; j++)
				{
					Vector3 avgDir1 = (gridSphere[i, j] + gridSphere[i + 1, j] + gridSphere[i, j + 1]).Normalized();
					float avgElev1 = (gridMacroElev[i, j] + gridMacroElev[i + 1, j] + gridMacroElev[i, j + 1]) / 3.0f;
					RegisterTriangle(gridPos[i, j], gridPos[i + 1, j], gridPos[i, j + 1], avgDir1, avgElev1);

					if (i + j < N - 1)
					{
						Vector3 avgDir2 = (gridSphere[i + 1, j] + gridSphere[i + 1, j + 1] + gridSphere[i, j + 1]).Normalized();
						float avgElev2 = (gridMacroElev[i + 1, j] + gridMacroElev[i + 1, j + 1] + gridMacroElev[i, j + 1]) / 3.0f;
						RegisterTriangle(gridPos[i + 1, j], gridPos[i + 1, j + 1], gridPos[i, j + 1], avgDir2, avgElev2);
					}
				}
			}
		}

		// --- PASO 2: Escaneo único de proximidad al agua (BFS) ---
		// Cada triángulo pregunta si tiene agua cerca y propaga la distancia hacia tierra adentro
		var queue = new Queue<int>();
		for (int i = 0; i < Triangles.Count; i++)
		{
			if (Triangles[i].WaterDistance == 0)
			{
				queue.Enqueue(i);
			}
		}

		while (queue.Count > 0)
		{
			int currentIdx = queue.Dequeue();
			var current = Triangles[currentIdx];

			// Solo necesitamos propagar hasta el ancho máximo de playa configurado
			if (current.WaterDistance >= BeachWidthTriangles) continue;

			foreach (var corner in new[] { current.V1, current.V2, current.V3 })
			{
				if (vertexToTriangles.TryGetValue(QuantizeXZ(corner), out var neighbors))
				{
					foreach (int neighborIdx in neighbors)
					{
						var neighbor = Triangles[neighborIdx];
						if (neighbor.WaterDistance > current.WaterDistance + 1)
						{
							neighbor.WaterDistance = current.WaterDistance + 1;
							queue.Enqueue(neighborIdx);
						}
					}
				}
			}
		}

		// --- PASO 3: Clasificar cada triángulo y construir la malla visual final ---
		var solidSt = new SurfaceTool();
		solidSt.Begin(Mesh.PrimitiveType.Triangles);

		var wireSt = new SurfaceTool();
		wireSt.Begin(Mesh.PrimitiveType.Lines);

		for (int i = 0; i < Triangles.Count; i++)
		{
			var tri = Triangles[i];

			if (tri.WaterDistance == 0)
			{
				tri.TerrainType = LocalTerrainType.Agua;
			}
			else if (tri.Slope > 0.36f)
			{
				// Acantilado rocoso por pendiente pronunciada
				tri.TerrainType = LocalTerrainType.RocaAcantilado;
			}
			else if (tri.WaterDistance <= BeachWidthTriangles && tri.AverageHeight < 12.0f)
			{
				// Tiene agua cerca -> se pinta de arena (playa real junto al agua, no en valles interiores)
				tri.TerrainType = LocalTerrainType.Arena;
			}
			else
			{
				tri.TerrainType = baseBiomes[i];
			}

			// Etiquetar pistas de recursos según altura y cercanía a montaña (para futura generación de rocas/vetas)
			if (tri.TerrainType != LocalTerrainType.Agua)
			{
				if (tri.AverageHeight >= 4.5f && tri.AverageHeight <= 14.0f && tri.Slope > 0.12f)
					tri.ResourceHint = "VetaMetales (Cobre/Hierro)";
				else if (tri.WaterDistance == 1 && tri.TerrainType == LocalTerrainType.Arena)
					tri.ResourceHint = "Costero (Arcilla/Sal/Piedra)";
				else if (tri.TerrainType == LocalTerrainType.Selva || tri.TerrainType == LocalTerrainType.Pradera)
					tri.ResourceHint = "Cultivable / Forestal";
			}

			Color triColor = GetColorForTerrainType(tri.TerrainType);

			solidSt.SetNormal(tri.Normal); solidSt.SetColor(triColor); solidSt.AddVertex(tri.V1);
			solidSt.SetNormal(tri.Normal); solidSt.SetColor(triColor); solidSt.AddVertex(tri.V2);
			solidSt.SetNormal(tri.Normal); solidSt.SetColor(triColor); solidSt.AddVertex(tri.V3);

			Vector3 lift = Vector3.Up * 0.25f;
			wireSt.AddVertex(tri.V1 + lift); wireSt.AddVertex(tri.V2 + lift);
			wireSt.AddVertex(tri.V2 + lift); wireSt.AddVertex(tri.V3 + lift);
			wireSt.AddVertex(tri.V3 + lift); wireSt.AddVertex(tri.V1 + lift);
		}

		// Zócalo geológico lateral
		Color wallTop = new Color(0.35f, 0.32f, 0.28f);
		Color wallBot = new Color(0.16f, 0.14f, 0.13f);

		for (int s = 0; s < sides; s++)
		{
			var gridPos = sectorGrids[s];
			for (int i = 0; i < N; i++)
			{
				Vector3 top1 = gridPos[i, N - i];
				Vector3 top2 = gridPos[i + 1, N - (i + 1)];
				Vector3 bot1 = new Vector3(top1.X, -depth, top1.Z);
				Vector3 bot2 = new Vector3(top2.X, -depth, top2.Z);

				Vector3 outward = new Vector3((top1.X + top2.X) * 0.5f, 0, (top1.Z + top2.Z) * 0.5f).Normalized();
				Vector3 sideNormal = (top2 - top1).Cross(bot1 - top1).Normalized();

				if (sideNormal.Dot(outward) < 0)
				{
					sideNormal = -sideNormal;
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top1);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top2);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot1);

					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top2);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot2);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot1);
				}
				else
				{
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top1);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot1);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top2);

					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallTop); solidSt.AddVertex(top2);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot1);
					solidSt.SetNormal(sideNormal); solidSt.SetColor(wallBot); solidSt.AddVertex(bot2);
				}
			}
		}

		var terrainMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
			VertexColorUseAsAlbedo = true,
			Roughness = 0.85f
		};

		_terrainMeshInstance = new MeshInstance3D
		{
			Name = "LocalTerrainMesh",
			Mesh = solidSt.Commit(),
			MaterialOverride = terrainMat
		};
		AddChild(_terrainMeshInstance);

		var wireMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.08f, 0.08f, 0.10f, 0.30f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha
		};

		_wireframeMeshInstance = new MeshInstance3D
		{
			Name = "EquilateralTriangleWireframe",
			Mesh = wireSt.Commit(),
			MaterialOverride = wireMat
		};
		AddChild(_wireframeMeshInstance);

		CreateWaterLayer(localCorners2D);
	}

	private void CreateWaterLayer(Vector2[] corners2D)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Vector3 center = Vector3.Zero;
		int sides = corners2D.Length;
		for (int i = 0; i < sides; i++)
		{
			Vector2 c1 = corners2D[i] * 0.998f;
			Vector2 c2 = corners2D[(i + 1) % sides] * 0.998f;

			Vector3 p1 = new Vector3(c1.X, 0.0f, c1.Y);
			Vector3 p2 = new Vector3(c2.X, 0.0f, c2.Y);

			st.SetNormal(Vector3.Up);
			st.AddVertex(center);
			st.AddVertex(p2);
			st.AddVertex(p1);
		}

		var waterMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			AlbedoColor = new Color(0.08f, 0.35f, 0.68f, 0.78f),
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		_waterMeshInstance = new MeshInstance3D
		{
			Name = "LocalWaterMesh",
			Mesh = st.Commit(),
			MaterialOverride = waterMat
		};
		AddChild(_waterMeshInstance);
	}
}
