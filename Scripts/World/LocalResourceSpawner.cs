using Godot;
using System.Collections.Generic;

public partial class LocalResourceSpawner : Node3D
{
	[Export(PropertyHint.Range, "0.0,1.0")] public float ForestDensity { get; set; } = 0.22f;
	[Export(PropertyHint.Range, "0.0,1.0")] public float MineralDensity { get; set; } = 0.20f;
	[Export(PropertyHint.Range, "0.0,1.0")] public float CoastalResourceDensity { get; set; } = 0.16f;

	private LocalMapGenerator _mapGenerator;
	private Dictionary<LocalResourceType, MultiMeshInstance3D> _resourceMeshes = new Dictionary<LocalResourceType, MultiMeshInstance3D>();

	public override void _Ready()
	{
		CallDeferred(nameof(PopulateResources));
	}

	public void PopulateResources()
	{
		_mapGenerator = GetParent() as LocalMapGenerator ?? GetNodeOrNull<LocalMapGenerator>("../LocalMap");
		if (_mapGenerator == null || _mapGenerator.Triangles == null || _mapGenerator.Triangles.Count == 0)
		{
			GD.PushWarning("[LocalResourceSpawner] No se encontraron triángulos generados para poblar.");
			return;
		}

		int seed = GameManager.Instance != null ? GameManager.Instance.GenesisSeed : 1337;
		int cellId = (GameManager.Instance != null && GameManager.Instance.ActiveCell != null)
			? GameManager.Instance.ActiveCell.Id
			: 25;

		float abundance = (GameManager.Instance != null && GameManager.Instance.CurrentWorldConfig != null)
			? GameManager.Instance.CurrentWorldConfig.ResourceAbundanceMultiplier
			: 1.0f;

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(seed ^ (cellId * 48271));

		var groveNoise = new FastNoiseLite
		{
			Seed = seed + cellId + 505,
			NoiseType = FastNoiseLite.NoiseTypeEnum.SimplexSmooth,
			Frequency = 0.0028f
		};

		var treeTransforms = new List<Transform3D>();
		var stoneTransforms = new List<Transform3D>();
		var clayTransforms = new List<Transform3D>();
		var coalTransforms = new List<Transform3D>();
		var copperTransforms = new List<Transform3D>();
		var ironTransforms = new List<Transform3D>();

		float effForest = Mathf.Clamp(ForestDensity * abundance, 0.04f, 0.55f);
		float effMineral = Mathf.Clamp(MineralDensity * abundance, 0.05f, 0.52f);
		float effCoastal = Mathf.Clamp(CoastalResourceDensity * abundance, 0.04f, 0.45f);

		foreach (var tri in _mapGenerator.Triangles)
		{
			if (tri.TerrainType == LocalTerrainType.Agua ||
				tri.TerrainType == LocalTerrainType.RocaAcantilado ||
				tri.TerrainType == LocalTerrainType.Nieve ||
				tri.Slope > 0.32f ||
				tri.ZoneState != LocalZoneState.Libre)
			{
				continue;
			}

			// 1. RECURSOS COSTEROS (Piedra y Arcilla de ribera en WaterDistance == 1)
			if (tri.WaterDistance == 1 && tri.TerrainType == LocalTerrainType.Arena)
			{
				if (rng.Randf() < effCoastal)
				{
					bool isClay = rng.Randf() < 0.42f;
					Transform3D xform = CreateSurfaceTransform(tri, rng, rng.RandfRange(0.90f, 1.35f), alignToSlope: true);
					tri.ZoneState = LocalZoneState.RocaRecurso;

					if (isClay)
					{
						tri.ResourceType = LocalResourceType.Arcilla;
						tri.ResourceAmount = rng.RandiRange(70, 140);
						tri.MultiMeshInstanceIndex = clayTransforms.Count;
						clayTransforms.Add(xform);
						tri.ResourceHint = $"Depósito de Arcilla ({tri.ResourceAmount} u.)";
					}
					else
					{
						tri.ResourceType = LocalResourceType.Piedra;
						tri.ResourceAmount = rng.RandiRange(80, 160);
						tri.MultiMeshInstanceIndex = stoneTransforms.Count;
						stoneTransforms.Add(xform);
						tri.ResourceHint = $"Cantera de Piedra ({tri.ResourceAmount} u.)";
					}
					continue;
				}
			}

			// 2. VETAS MINERALES MEDIEVALES (Carbón, Cobre y Hierro según altitud y pendiente)
			if (tri.AverageHeight >= 4.0f && tri.AverageHeight <= 32.0f && tri.Slope >= 0.025f)
			{
				if (rng.Randf() < effMineral)
				{
					Transform3D xform = CreateSurfaceTransform(tri, rng, rng.RandfRange(0.95f, 1.40f), alignToSlope: true);
					tri.ZoneState = LocalZoneState.RocaRecurso;

					// Distribución realista por altitud:
					// - 4m a 15m: Cobre y Carbón
					// - 13m a 32m: Hierro y Carbón profundo
					if (tri.AverageHeight >= 13.5f && rng.Randf() < 0.62f)
					{
						tri.ResourceType = LocalResourceType.Hierro;
						tri.ResourceAmount = rng.RandiRange(80, 160);
						tri.MultiMeshInstanceIndex = ironTransforms.Count;
						ironTransforms.Add(xform);
						tri.ResourceHint = $"Veta de Hierro ({tri.ResourceAmount} u.)";
					}
					else if (rng.Randf() < 0.52f)
					{
						tri.ResourceType = LocalResourceType.Cobre;
						tri.ResourceAmount = rng.RandiRange(90, 170);
						tri.MultiMeshInstanceIndex = copperTransforms.Count;
						copperTransforms.Add(xform);
						tri.ResourceHint = $"Veta de Cobre ({tri.ResourceAmount} u.)";
					}
					else
					{
						tri.ResourceType = LocalResourceType.Carbon;
						tri.ResourceAmount = rng.RandiRange(100, 190);
						tri.MultiMeshInstanceIndex = coalTransforms.Count;
						coalTransforms.Add(xform);
						tri.ResourceHint = $"Veta de Carbón ({tri.ResourceAmount} u.)";
					}
					continue;
				}
			}

			// 3. BOSQUES (Madera) en Selva, Pradera o Tundra con claros naturales
			if ((tri.TerrainType == LocalTerrainType.Selva ||
				 tri.TerrainType == LocalTerrainType.Pradera ||
				 tri.TerrainType == LocalTerrainType.Tundra) &&
				tri.AverageHeight > 1.5f && tri.Slope < 0.24f)
			{
				float groveFactor = (groveNoise.GetNoise2D(tri.Center.X, tri.Center.Z) + 1.0f) * 0.5f;
				float biomeMultiplier = tri.TerrainType == LocalTerrainType.Selva ? 1.35f : 0.90f;

				if (groveFactor > 0.52f && rng.Randf() < effForest * biomeMultiplier * ((groveFactor - 0.52f) * 2.5f))
				{
					Transform3D xform = CreateSurfaceTransform(tri, rng, rng.RandfRange(0.95f, 1.45f), alignToSlope: false);
					tri.ZoneState = LocalZoneState.Bosque;
					tri.ResourceType = LocalResourceType.Madera;
					tri.ResourceAmount = rng.RandiRange(60, 120);
					tri.MultiMeshInstanceIndex = treeTransforms.Count;
					treeTransforms.Add(xform);
					tri.ResourceHint = $"Bosque / Madera ({tri.ResourceAmount} u.)";
				}
			}
		}

		_resourceMeshes.Clear();
		CreateResourceMultiMesh(LocalResourceType.Madera, "ForestTreesMultiMesh", CreateFacetedTreeMesh(), treeTransforms);
		CreateResourceMultiMesh(LocalResourceType.Piedra, "CoastalStonesMultiMesh", CreateFacetedRockMesh(new Color(0.62f, 0.60f, 0.56f)), stoneTransforms);
		CreateResourceMultiMesh(LocalResourceType.Arcilla, "RiverClayMultiMesh", CreateFacetedRockMesh(new Color(0.72f, 0.45f, 0.30f)), clayTransforms);
		CreateResourceMultiMesh(LocalResourceType.Carbon, "CoalVeinsMultiMesh", CreateFacetedOreMesh(new Color(0.14f, 0.15f, 0.18f), new Color(0.28f, 0.28f, 0.30f)), coalTransforms);
		CreateResourceMultiMesh(LocalResourceType.Cobre, "CopperVeinsMultiMesh", CreateFacetedOreMesh(new Color(0.86f, 0.44f, 0.20f), new Color(0.42f, 0.36f, 0.33f)), copperTransforms);
		CreateResourceMultiMesh(LocalResourceType.Hierro, "IronVeinsMultiMesh", CreateFacetedOreMesh(new Color(0.56f, 0.64f, 0.74f), new Color(0.32f, 0.33f, 0.36f)), ironTransforms);

		GD.Print($"[LocalResourceSpawner] Economía Medieval Poblada -> Madera: {treeTransforms.Count}, Piedra: {stoneTransforms.Count}, Arcilla: {clayTransforms.Count}, Carbón: {coalTransforms.Count}, Cobre: {copperTransforms.Count}, Hierro: {ironTransforms.Count}");
	}

	/// <summary>
	/// Extrae recursos de un triángulo y oculta su instancia visual en el MultiMesh si se agota.
	/// </summary>
	public int ExtractResourceFromTriangle(LocalTriangleData tri, int requestedAmount)
	{
		if (tri == null || tri.ResourceType == LocalResourceType.Ninguno || tri.ResourceAmount <= 0)
			return 0;

		int extracted = Mathf.Min(requestedAmount, tri.ResourceAmount);
		tri.ResourceAmount -= extracted;

		if (tri.ResourceAmount <= 0)
		{
			// Ocultar instancia en el MultiMesh escalándola a 0
			if (tri.MultiMeshInstanceIndex >= 0 && _resourceMeshes.TryGetValue(tri.ResourceType, out var mmInstance))
			{
				mmInstance.Multimesh.SetInstanceTransform(tri.MultiMeshInstanceIndex, new Transform3D(Basis.FromScale(Vector3.Zero), tri.Center));
			}

			tri.ResourceType = LocalResourceType.Ninguno;
			tri.ResourceAmount = 0;
			tri.ZoneState = LocalZoneState.Libre;
			tri.ResourceHint = "Terreno despejado (Edificable)";
		}
		else
		{
			tri.ResourceHint = $"{tri.ResourceType} ({tri.ResourceAmount} u.)";
		}

		return extracted;
	}

	private Transform3D CreateSurfaceTransform(LocalTriangleData tri, RandomNumberGenerator rng, float scale, bool alignToSlope)
	{
		Basis basis = Basis.Identity;

		if (alignToSlope && tri.Normal.Y > 0.2f)
		{
			Vector3 up = tri.Normal.Normalized();
			Vector3 right = up.Cross(Vector3.Forward).Normalized();
			if (right.LengthSquared() < 0.001f) right = Vector3.Right;
			Vector3 forward = right.Cross(up).Normalized();
			basis = new Basis(right, up, forward);
		}

		basis = basis.Rotated(basis.Y, rng.RandfRange(0f, Mathf.Tau));
		basis = basis.Scaled(new Vector3(scale, scale * rng.RandfRange(0.85f, 1.25f), scale));

		Vector3 pos = tri.Center - Vector3.Up * 0.25f;
		return new Transform3D(basis, pos);
	}

	private void CreateResourceMultiMesh(LocalResourceType type, string nodeName, ArrayMesh mesh, List<Transform3D> transforms)
	{
		if (transforms.Count == 0) return;

		var multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			Mesh = mesh,
			InstanceCount = transforms.Count
		};

		for (int i = 0; i < transforms.Count; i++)
		{
			multiMesh.SetInstanceTransform(i, transforms[i]);
		}

		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
			VertexColorUseAsAlbedo = true,
			Roughness = 0.85f
		};

		var mmInstance = new MultiMeshInstance3D
		{
			Name = nodeName,
			Multimesh = multiMesh,
			MaterialOverride = mat
		};
		AddChild(mmInstance);
		_resourceMeshes[type] = mmInstance;
	}

	private ArrayMesh CreateFacetedTreeMesh()
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Color trunkColor = new Color(0.38f, 0.25f, 0.14f);
		Color foliageLow = new Color(0.14f, 0.42f, 0.18f);
		Color foliageHigh = new Color(0.24f, 0.58f, 0.26f);

		AddPrism(st, 0.0f, 4.5f, 1.2f, 0.9f, 5, trunkColor);
		AddPrism(st, 3.8f, 11.5f, 5.4f, 1.8f, 5, foliageLow);
		AddPrism(st, 9.5f, 16.5f, 3.8f, 0.1f, 5, foliageHigh);

		return st.Commit();
	}

	private ArrayMesh CreateFacetedOreMesh(Color oreAccent, Color baseRock)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		AddPrism(st, 0.0f, 4.2f, 5.2f, 3.0f, 6, baseRock);
		AddPrism(st, 1.0f, 7.2f, 2.5f, 0.35f, 4, oreAccent, new Vector3(1.3f, 0f, 0.9f));
		AddPrism(st, 0.6f, 6.0f, 2.2f, 0.25f, 4, oreAccent, new Vector3(-1.5f, 0f, -0.7f));
		AddPrism(st, 0.8f, 5.5f, 1.8f, 0.25f, 4, oreAccent, new Vector3(0.2f, 0f, -1.6f));

		return st.Commit();
	}

	private ArrayMesh CreateFacetedRockMesh(Color rockColor)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		AddPrism(st, 0.0f, 3.6f, 4.8f, 2.4f, 5, rockColor);
		AddPrism(st, 0.0f, 2.6f, 3.2f, 1.3f, 5, rockColor.Darkened(0.14f), new Vector3(2.4f, 0f, 1.3f));

		return st.Commit();
	}

	private void AddPrism(SurfaceTool st, float yBot, float yTop, float rBot, float rTop, int sides, Color color, Vector3 offset = default)
	{
		for (int i = 0; i < sides; i++)
		{
			float a1 = i * Mathf.Tau / sides;
			float a2 = ((i + 1) % sides) * Mathf.Tau / sides;

			Vector3 b1 = new Vector3(Mathf.Cos(a1) * rBot, yBot, Mathf.Sin(a1) * rBot) + offset;
			Vector3 b2 = new Vector3(Mathf.Cos(a2) * rBot, yBot, Mathf.Sin(a2) * rBot) + offset;
			Vector3 t1 = new Vector3(Mathf.Cos(a1) * rTop, yTop, Mathf.Sin(a1) * rTop) + offset;
			Vector3 t2 = new Vector3(Mathf.Cos(a2) * rTop, yTop, Mathf.Sin(a2) * rTop) + offset;

			AddFlatTri(st, b1, b2, t1, color);
			AddFlatTri(st, b2, t2, t1, color);

			Vector3 topCenter = new Vector3(0, yTop, 0) + offset;
			AddFlatTri(st, t1, t2, topCenter, color.Lightened(0.10f));
		}
	}

	private void AddFlatTri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color color)
	{
		Vector3 normal = (b - a).Cross(c - a).Normalized();
		st.SetNormal(normal); st.SetColor(color); st.AddVertex(a);
		st.SetNormal(normal); st.SetColor(color); st.AddVertex(b);
		st.SetNormal(normal); st.SetColor(color); st.AddVertex(c);
	}
}
