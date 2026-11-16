using Godot;
using System.Collections.Generic;

public partial class PlanetGridManager : Node3D
{
	[Export] public float PlanetRadius = 32.0f;
	[Export] public Node3D PlanetGeneratorNode;
	[Export] public Camera3D GameCamera;
	[Export] public Label TooltipLabel;

	private class TileData
	{
		public int Id;
		public Vector3 CenterDir;
		public List<Vector3> BorderVertices = new List<Vector3>();
		public MeshInstance3D MeshInstance;
		public StandardMaterial3D Material;

		// Información táctica escaneada del terreno real
		public int BuildablePercent;
		public string DominantBiome;
	}

	private Dictionary<int, TileData> _tiles = new Dictionary<int, TileData>();
	private int _hoveredTileId = -1;
	private float _tacticalRadius;

	public override void _Ready()
	{
		if (GameCamera == null) GameCamera = GetViewport().GetCamera3D();

		if (PlanetGeneratorNode != null)
		{
			var radiusProp = PlanetGeneratorNode.Get("PlanetRadius");
			if (radiusProp.VariantType != Variant.Type.Nil)
				PlanetRadius = radiusProp.AsSingle();
		}

		_tacticalRadius = PlanetRadius + 1.8f;
		BuildExactGoldbergGrid();
	}

	private void BuildExactGoldbergGrid()
	{
		foreach (var t in _tiles.Values) t.MeshInstance.QueueFree();
		_tiles.Clear();

		float phi = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;

		List<Vector3> verts = new List<Vector3>
		{
			new Vector3(-1,  phi,  0).Normalized(), new Vector3( 1,  phi,  0).Normalized(),
			new Vector3(-1, -phi,  0).Normalized(), new Vector3( 1, -phi,  0).Normalized(),
			new Vector3( 0, -1,  phi).Normalized(), new Vector3( 0,  1,  phi).Normalized(),
			new Vector3( 0, -1, -phi).Normalized(), new Vector3( 0,  1, -phi).Normalized(),
			new Vector3( phi,  0, -1).Normalized(), new Vector3( phi,  0,  1).Normalized(),
			new Vector3(-phi,  0, -1).Normalized(), new Vector3(-phi,  0,  1).Normalized()
		};

		int[,] baseFaces = new int[,]
		{
			{0,11,5}, {0,5,1}, {0,1,7}, {0,7,10}, {0,10,11},
			{1,5,9}, {5,11,4}, {11,10,2}, {10,7,6}, {7,1,8},
			{3,9,4}, {3,4,2}, {3,2,6}, {3,6,8}, {3,8,9},
			{4,9,5}, {2,4,11}, {6,2,10}, {8,6,7}, {9,8,1}
		};

		Dictionary<long, int> midPointCache = new Dictionary<long, int>();
		List<int[]> subFaces = new List<int[]>();

		int GetMidPoint(int p1, int p2)
		{
			long key = ((long)Mathf.Min(p1, p2) << 32) + Mathf.Max(p1, p2);
			if (midPointCache.TryGetValue(key, out int idx)) return idx;

			Vector3 mid = ((verts[p1] + verts[p2]) * 0.5f).Normalized();
			idx = verts.Count;
			verts.Add(mid);
			midPointCache[key] = idx;
			return idx;
		}

		for (int i = 0; i < 20; i++)
		{
			int v1 = baseFaces[i, 0], v2 = baseFaces[i, 1], v3 = baseFaces[i, 2];
			int a = GetMidPoint(v1, v2);
			int b = GetMidPoint(v2, v3);
			int c = GetMidPoint(v3, v1);

			subFaces.Add(new[] { v1, a, c });
			subFaces.Add(new[] { v2, b, a });
			subFaces.Add(new[] { v3, c, b });
			subFaces.Add(new[] { a, b, c });
		}

		for (int vIdx = 0; vIdx < verts.Count; vIdx++)
		{
			Vector3 centerDir = verts[vIdx];
			List<Vector3> faceCentroids = new List<Vector3>();

			foreach (var face in subFaces)
			{
				if (face[0] == vIdx || face[1] == vIdx || face[2] == vIdx)
				{
					Vector3 centroid = (verts[face[0]] + verts[face[1]] + verts[face[2]]).Normalized();
					faceCentroids.Add(centroid);
				}
			}

			Vector3 tangent = centerDir.Cross(Vector3.Up);
			if (tangent.LengthSquared() < 0.001f) tangent = centerDir.Cross(Vector3.Right);
			tangent = tangent.Normalized();
			Vector3 bitangent = centerDir.Cross(tangent).Normalized();

			faceCentroids.Sort((a, b) =>
			{
				float angA = Mathf.Atan2((a - centerDir).Dot(bitangent), (a - centerDir).Dot(tangent));
				float angB = Mathf.Atan2((b - centerDir).Dot(bitangent), (b - centerDir).Dot(tangent));
				return angA.CompareTo(angB);
			});

			var tile = new TileData
			{
				Id = vIdx,
				CenterDir = centerDir,
				BorderVertices = faceCentroids
			};

			tile.Material = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
				NoDepthTest = false,
				AlbedoColor = new Color(0.35f, 0.75f, 1.0f, 0.25f),
				EmissionEnabled = true,
				Emission = new Color(0.25f, 0.65f, 1.0f),
				EmissionEnergyMultiplier = 1.2f
			};

			tile.MeshInstance = new MeshInstance3D
			{
				Mesh = BuildBorderMesh(tile),
				MaterialOverride = tile.Material
			};

			AddChild(tile.MeshInstance);
			_tiles[vIdx] = tile;
		}

		// Alinear un pentágono exactamente en el Polo Norte y otro en el Polo Sur
		Vector3 northPoleDir = _tiles[0].CenterDir;
		Quaternion alignToPoles = new Quaternion(northPoleDir, Vector3.Up);
		Transform = new Transform3D(new Basis(alignToPoles), Transform.Origin);

		// Escanear el terreno real bajo cada celda después de alinear los polos
		ScanAllTilesTerrain();
	}

	private void ScanAllTilesTerrain()
	{
		var generator = PlanetGeneratorNode as PlanetGenerator;
		float seaLevelOffset = generator != null ? generator.SeaLevelOffset : 0.15f;
		float elevationHeight = generator != null ? generator.ElevationHeight : 1.0f;
		float seaHeightThreshold = elevationHeight * seaLevelOffset;

		foreach (var tile in _tiles.Values)
		{
			Vector3 worldCenterDir = (Transform.Basis * tile.CenterDir).Normalized();
			List<Vector3> sampleDirs = new List<Vector3> { worldCenterDir };

			foreach (var v in tile.BorderVertices)
			{
				Vector3 worldVertex = (Transform.Basis * v).Normalized();
				sampleDirs.Add(worldCenterDir.Lerp(worldVertex, 0.45f).Normalized());
				sampleDirs.Add(worldCenterDir.Lerp(worldVertex, 0.85f).Normalized());
			}

			int buildableSamples = 0;
			int waterSamples = 0;
			int mountainSamples = 0;
			float avgAltitudeRatio = 0f;
			int landCount = 0;

			foreach (var dir in sampleDirs)
			{
				float elev = generator != null ? generator.SampleElevationAt(dir) : 0.2f;

				if (elev < seaHeightThreshold)
				{
					waterSamples++;
				}
				else
				{
					landCount++;
					float altRatio = elev / elevationHeight;
					avgAltitudeRatio += altRatio;

					// Edificable: tierra sobre el nivel del mar que no sea montaña escarpada (< 0.45) ni cumbre nevada
					if (altRatio <= 0.45f)
						buildableSamples++;
					else
						mountainSamples++;
				}
			}

			tile.BuildablePercent = Mathf.RoundToInt((buildableSamples / (float)sampleDirs.Count) * 100f);
			avgAltitudeRatio = landCount > 0 ? avgAltitudeRatio / landCount : 0f;

			float latitude = Mathf.Abs(worldCenterDir.Y);
			if (waterSamples > sampleDirs.Count * 0.70f)
				tile.DominantBiome = "Océano";
			else if (latitude > 0.82f)
				tile.DominantBiome = "Casquete Polar";
			else if (mountainSamples > landCount * 0.45f || avgAltitudeRatio > 0.50f)
				tile.DominantBiome = "Cordillera Montañosa";
			else if (latitude > 0.65f)
				tile.DominantBiome = "Tundra / Taiga";
			else if (latitude < 0.25f)
				tile.DominantBiome = "Selva / Trópico";
			else if (latitude < 0.48f && tile.BuildablePercent > 50)
				tile.DominantBiome = "Sabana / Desierto";
			else
				tile.DominantBiome = "Pradera Templada";
		}
	}

	private Mesh BuildBorderMesh(TileData tile)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Lines);

		int count = tile.BorderVertices.Count;
		for (int i = 0; i < count; i++)
		{
			Vector3 v1 = tile.BorderVertices[i].Lerp(tile.CenterDir, 0.015f).Normalized() * _tacticalRadius;
			Vector3 v2 = tile.BorderVertices[(i + 1) % count].Lerp(tile.CenterDir, 0.015f).Normalized() * _tacticalRadius;

			st.AddVertex(v1);
			st.AddVertex(v2);
		}

		return st.Commit();
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventMouseMotion motion && GameCamera != null)
		{
			DetectHoveredTile(motion.Position);
		}
		else if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (_hoveredTileId != -1 && _tiles.TryGetValue(_hoveredTileId, out var selectedTile))
			{
				if (GameManager.Instance != null)
				{
					var cellData = new BasalKey();
					Vector3 worldCenterDir = (Transform.Basis * selectedTile.CenterDir).Normalized();
					cellData.Initialize(selectedTile.Id, GameManager.Instance.GenesisSeed, worldCenterDir);
					cellData.Biome = selectedTile.DominantBiome;
					cellData.BaseElevation = Mathf.Clamp(selectedTile.BuildablePercent / 100.0f, 0.15f, 1.0f);

					// Guardar las direcciones 3D exactas de las 5 o 6 esquinas de este sector en el planeta
					Vector3[] worldCorners = new Vector3[selectedTile.BorderVertices.Count];
					for (int i = 0; i < selectedTile.BorderVertices.Count; i++)
					{
						worldCorners[i] = (Transform.Basis * selectedTile.BorderVertices[i]).Normalized();
					}
					cellData.CornerDirections = worldCorners;

					if (PlanetGeneratorNode is PlanetGenerator pg)
					{
						cellData.SeaLevelOffset = pg.SeaLevelOffset;
						cellData.ElevationHeight = pg.ElevationHeight;
					}

					GameManager.Instance.ActiveCell = cellData;
				}

				GD.Print($"[PlanetGridManager] Entrando al Sector #{_hoveredTileId} ({selectedTile.DominantBiome} - {selectedTile.BuildablePercent}% Edificable)...");
				GetTree().ChangeSceneToFile("res://Scenes/Local/LocalTile.tscn");
			}
		}
	}

	private void DetectHoveredTile(Vector2 mousePos)
	{
		Vector3 rayOrigin = GameCamera.ProjectRayOrigin(mousePos);
		Vector3 rayDir = GameCamera.ProjectRayNormal(mousePos);
		Vector3 sphereCenter = GlobalPosition;

		Vector3 oc = rayOrigin - sphereCenter;
		float b = oc.Dot(rayDir);
		float c = oc.Dot(oc) - _tacticalRadius * _tacticalRadius;
		float discriminant = b * b - c;

		int newHoveredId = -1;

		if (discriminant >= 0)
		{
			float t = -b - Mathf.Sqrt(discriminant);
			if (t >= 0)
			{
				Vector3 hitWorld = rayOrigin + rayDir * t;
				Vector3 localHitDir = ToLocal(hitWorld).Normalized();

				float maxDot = -1.0f;
				foreach (var kvp in _tiles)
				{
					float dot = kvp.Value.CenterDir.Dot(localHitDir);
					if (dot > maxDot)
					{
						maxDot = dot;
						newHoveredId = kvp.Key;
					}
				}
			}
		}

		if (_hoveredTileId != newHoveredId)
		{
			if (_hoveredTileId != -1 && _tiles.TryGetValue(_hoveredTileId, out var oldTile))
			{
				oldTile.Material.AlbedoColor = new Color(0.35f, 0.75f, 1.0f, 0.25f);
				oldTile.Material.Emission = new Color(0.25f, 0.65f, 1.0f);
				oldTile.Material.EmissionEnergyMultiplier = 1.2f;
			}

			_hoveredTileId = newHoveredId;

			if (_hoveredTileId != -1 && _tiles.TryGetValue(_hoveredTileId, out var newTile))
			{
				newTile.Material.AlbedoColor = new Color(1.0f, 0.55f, 0.0f, 1.0f);
				newTile.Material.Emission = new Color(1.0f, 0.45f, 0.0f);
				newTile.Material.EmissionEnergyMultiplier = 4.5f;
			}

			UpdateTooltip();
		}
	}

	private void UpdateTooltip()
	{
		if (TooltipLabel == null) return;

		if (_hoveredTileId != -1 && _tiles.TryGetValue(_hoveredTileId, out var tile))
		{
			string tipo = tile.BorderVertices.Count == 5 ? "Pentágono" : "Hexágono";
			string edificableTexto = tile.BuildablePercent == 0
				? "0% Edificable (Solo Marítimo)"
				: $"{tile.BuildablePercent}% Edificable";

			TooltipLabel.Text = $"SECTOR #{_hoveredTileId} ({tipo})\n" +
								$"Bioma: {tile.DominantBiome}\n" +
								$"Terreno: {edificableTexto}\n" +
								$"[Clic izquierdo para explorar]";
		}
		else
		{
			TooltipLabel.Text = "";
		}
	}
}
