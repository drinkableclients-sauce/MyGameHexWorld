using Godot;
using Godot.Collections;

public partial class LocalMapGenerator : Node3D
{
	private BasalKey _currentDna;
	private MeshInstance3D _terrainMeshNode;
	private MeshInstance3D _waterMeshNode;
	private MultiMeshInstance3D _treesMultiMeshNode;
	private Node3D _propsContainer;

	public override void _Ready()
	{
		EnsureVisualNodes();

		if (GameManager.Instance != null && GameManager.Instance.ActiveCell != null)
		{
			_currentDna = GameManager.Instance.ActiveCell;
			int tileId = _currentDna.Id;

			GD.Print($"[LocalMapGenerator] Analizando Tile #{tileId} | Bioma: {_currentDna.Biome}");

			// 1. SISTEMA ATK: Verificar si ya existe un Acta de Tile guardada en disco
			if (TilePersistenceManager.Instance != null && TilePersistenceManager.Instance.TileDataExists(tileId))
			{
				GD.Print($"[LocalMapGenerator] Cargando ATK existente para el Tile #{tileId} (Lectura instantánea).");
				Dictionary savedAtk = TilePersistenceManager.Instance.LoadTileATK(tileId);
				BuildMapFromATK(savedAtk);
			}
			else
			{
				GD.Print($"[LocalMapGenerator] Primera visita detectada. Generando génesis local y creando ATK para el Tile #{tileId}...");
				BuildMapFromDNA(_currentDna);

				// Guardar el snapshot ATK por primera y única vez
				if (TilePersistenceManager.Instance != null)
				{
					Dictionary newAtk = CreateAtkDictionary(_currentDna);
					TilePersistenceManager.Instance.SaveTileATK(tileId, newAtk);
				}
			}
		}
		else
		{
			GD.PrintErr("[LocalMapGenerator] ADVERTENCIA: No hay celda activa en el GameManager. Generando entorno de prueba por defecto.");
			BuildDefaultMap();
		}
	}

	private void EnsureVisualNodes()
	{
		if (_terrainMeshNode == null)
		{
			_terrainMeshNode = new MeshInstance3D { Name = "LocalTerrain" };
			AddChild(_terrainMeshNode);
		}
		if (_waterMeshNode == null)
		{
			_waterMeshNode = new MeshInstance3D { Name = "LocalWater" };
			AddChild(_waterMeshNode);
		}
		if (_treesMultiMeshNode == null)
		{
			_treesMultiMeshNode = new MultiMeshInstance3D { Name = "ForestMultiMesh" };
			AddChild(_treesMultiMeshNode);
		}
		if (_propsContainer == null)
		{
			_propsContainer = new Node3D { Name = "PropsContainer" };
			AddChild(_propsContainer);
		}
		else
		{
			foreach (Node child in _propsContainer.GetChildren()) child.QueueFree();
		}
	}

	/// <summary>
	/// Genera el diorama por primera vez a partir del ADN puro (BasalKey) y la consulta de vecinos (NFK).
	/// </summary>
	private void BuildMapFromDNA(BasalKey dna)
	{
		var localRng = new RandomNumberGenerator();
		localRng.Seed = (ulong)dna.RoughnessSeed;

		float targetElevation = dna.BaseElevation;
		bool isWaterBody = dna.WaterRatio > 0.4f || dna.Biome.Contains("Océano") || dna.Biome.Contains("Lago");

		// Determinar si es Pentágono (5 aristas) o Hexágono (6 aristas)
		int vertexCount = (dna.TileVertices != null && dna.TileVertices.Count == 5) ? 5 : 6;
		float mapRadius = 220.0f; // Escala amplia y táctica
		float terrainHeightOffset = targetElevation * 10.0f;
		float blockDepth = 18.0f; // Grosor físico sólido del bloque tipo diorama

		// 1. Construir el prisma poligonal sólido
		_terrainMeshNode.Mesh = CreateSolidPolygonPrismMesh(vertexCount, mapRadius, blockDepth, dna);
		_terrainMeshNode.MaterialOverride = GetBiomeMaterial(dna.Biome, targetElevation);
		_terrainMeshNode.Position = new Vector3(0, terrainHeightOffset, 0);

		// 2. Gestionar agua local si aplica
		if (isWaterBody)
		{
			var waterPlane = new PlaneMesh { Size = new Vector2(mapRadius * 2.2f, mapRadius * 2.2f) };
			_waterMeshNode.Mesh = waterPlane;
			_waterMeshNode.MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.15f, 0.42f, 0.78f, 0.85f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			};
			_waterMeshNode.Position = new Vector3(0, terrainHeightOffset - 1.2f, 0);
			_waterMeshNode.Visible = true;
		}
		else
		{
			_waterMeshNode.Visible = false;
		}

		// 3. Población ecológica estricta (Bosques y Amenazas según bioma)
		PopulateEcology(vertexCount, mapRadius, terrainHeightOffset, dna, localRng);
	}

	/// <summary>
	/// Reconstruye el mapa instantáneamente leyendo el archivo ATK guardado (Write-Once, Read-Forever).
	/// </summary>
	private void BuildMapFromATK(Dictionary atkData)
	{
		string biome = atkData.ContainsKey("Biome") ? atkData["Biome"].As<string>() : "Pradera Fértil";
		float elevation = atkData.ContainsKey("BaseElevation") ? (float)atkData["BaseElevation"] : 0.0f;
		int vertexCount = atkData.ContainsKey("VertexCount") ? (int)atkData["VertexCount"] : 6;
		float mapRadius = 220.0f;
		float terrainHeightOffset = elevation * 10.0f;
		float blockDepth = 18.0f;

		var dummyDna = new BasalKey { RoughnessSeed = 1337, BaseElevation = elevation, Biome = biome };

		_terrainMeshNode.Mesh = CreateSolidPolygonPrismMesh(vertexCount, mapRadius, blockDepth, dummyDna);
		_terrainMeshNode.MaterialOverride = GetBiomeMaterial(biome, elevation);
		_terrainMeshNode.Position = new Vector3(0, terrainHeightOffset, 0);

		bool isWater = biome.Contains("Océano") || biome.Contains("Lago");
		if (isWater)
		{
			var waterPlane = new PlaneMesh { Size = new Vector2(mapRadius * 2.2f, mapRadius * 2.2f) };
			_waterMeshNode.Mesh = waterPlane;
			_waterMeshNode.MaterialOverride = new StandardMaterial3D
			{
				ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
				AlbedoColor = new Color(0.15f, 0.42f, 0.78f, 0.85f),
				Transparency = BaseMaterial3D.TransparencyEnum.Alpha
			};
			_waterMeshNode.Position = new Vector3(0, terrainHeightOffset - 1.2f, 0);
			_waterMeshNode.Visible = true;
		}
		else
		{
			_waterMeshNode.Visible = false;
		}

		GD.Print("[LocalMapGenerator] Diorama reconstruido fluidamente desde el ATK en disco.");
	}

	private Mesh CreateSolidPolygonPrismMesh(int sides, float radius, float depth, BasalKey dna)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		var noise = new FastNoiseLite();
		noise.Seed = dna.RoughnessSeed;
		noise.Frequency = 0.008f;

		Vector3[] topVertices = new Vector3[sides];
		Vector3[] bottomVertices = new Vector3[sides];
		Vector3 centerTop = new Vector3(0, noise.GetNoise2D(0, 0) * 4.0f, 0);
		Vector3 centerBottom = new Vector3(0, -depth, 0);

		for (int i = 0; i < sides; i++)
		{
			float angle = i * Mathf.Tau / sides;
			float x = Mathf.Cos(angle) * radius;
			float z = Mathf.Sin(angle) * radius;
			float yTop = noise.GetNoise2D(x, z) * 5.0f;

			topVertices[i] = new Vector3(x, yTop, z);
			bottomVertices[i] = new Vector3(x, -depth, z);
		}

		// Tapa superior del diorama
		for (int i = 0; i < sides; i++)
		{
			int next = (i + 1) % sides;
			st.SetNormal(Vector3.Up);
			st.AddVertex(centerTop);
			st.AddVertex(topVertices[i]);
			st.AddVertex(topVertices[next]);
		}

		// Paredes laterales sólidas del zócalo
		for (int i = 0; i < sides; i++)
		{
			int next = (i + 1) % sides;
			Vector3 p1 = topVertices[i];
			Vector3 p2 = topVertices[next];
			Vector3 b1 = bottomVertices[i];
			Vector3 b2 = bottomVertices[next];

			Vector3 sideNormal = (p2 - p1).Cross(b1 - p1).Normalized();

			st.SetNormal(sideNormal);
			st.AddVertex(p1);
			st.AddVertex(b1);
			st.AddVertex(p2);

			st.SetNormal(sideNormal);
			st.AddVertex(p2);
			st.AddVertex(b1);
			st.AddVertex(b2);
		}

		return st.Commit();
	}

	private StandardMaterial3D GetBiomeMaterial(string biome, float elevation)
	{
		var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

		if (biome.Contains("Océano") || biome.Contains("Costa"))
			mat.AlbedoColor = new Color(0.75f, 0.68f, 0.48f); // Arena de costa
		else if (biome.Contains("Polar") || biome.Contains("Tundra"))
			mat.AlbedoColor = new Color(0.88f, 0.88f, 0.92f); // Nieve / hielo
		else if (elevation > 0.7f)
			mat.AlbedoColor = new Color(0.52f, 0.52f, 0.58f); // Roca alpina
		else if (biome.Contains("Desierto"))
			mat.AlbedoColor = new Color(0.85f, 0.75f, 0.45f); // Desértico
		else
			mat.AlbedoColor = new Color(0.25f, 0.60f, 0.30f); // Verde pradera medieval

		return mat;
	}

	private void PopulateEcology(int sides, float radius, float baseHeight, BasalKey dna, RandomNumberGenerator rng)
	{
		if (dna.Biome.Contains("Desierto") || dna.Biome.Contains("Polar") || dna.Biome.Contains("Océano"))
			return; // Respeto estricto biológico: sin bosques en los polos o desiertos

		int treeCount = 45;
		var multiMesh = new MultiMesh
		{
			TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
			InstanceCount = treeCount,
			Mesh = new SphereMesh { Radius = 2.5f, Height = 5.0f }
		};

		for (int i = 0; i < treeCount; i++)
		{
			float dist = rng.RandfRange(20.0f, radius * 0.8f);
			float angle = rng.RandfRange(0, Mathf.Tau);
			float x = Mathf.Cos(angle) * dist;
			float z = Mathf.Sin(angle) * dist;

			Transform3D t = new Transform3D();
			float scaleVar = rng.RandfRange(0.6f, 1.4f);
			t = t.ScaledLocal(new Vector3(scaleVar, scaleVar, scaleVar));
			t.Origin = new Vector3(x, baseHeight + 2.0f, z);

			multiMesh.SetInstanceTransform(i, t);
		}
		_treesMultiMeshNode.Multimesh = multiMesh;
	}

	private Dictionary CreateAtkDictionary(BasalKey dna)
	{
		var atk = new Dictionary();
		atk["Id"] = dna.Id;
		atk["Biome"] = dna.Biome;
		atk["BaseElevation"] = dna.BaseElevation;
		atk["VertexCount"] = (dna.TileVertices != null && dna.TileVertices.Count == 5) ? 5 : 6;
		atk["IsExplored"] = true;
		return atk;
	}

	private void BuildDefaultMap()
	{
		_terrainMeshNode.Mesh = CreateSolidPolygonPrismMesh(6, 220.0f, 18.0f, new BasalKey { RoughnessSeed = 123 });
		_terrainMeshNode.MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, AlbedoColor = new Color(0.25f, 0.60f, 0.30f) };
		_waterMeshNode.Visible = false;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			GetTree().ChangeSceneToFile("res://Scenes/Global/Planet.tscn");
		}
	}
}
