using Godot;

public partial class LocalMapGenerator : Node3D
{
	private HexCell _cellData;
	private ActiveTileKey _activeTileKey;
	private RandomNumberGenerator _rng;
	private DirectionalLight3D _sunLight;
	private FastNoiseLite _terrainNoise;

	[Export] public float LocalDayNightSpeed = 0.005f; 
	[Export] public float MeshScale = 1.0f; 
	[Export] public float WaterLevel = 0.5f; 

	private float _terrainRadius = 80.0f; 
	private Vector2[] _localPolygonVertices;

	public override void _Ready()
	{
		_cellData = GameManager.Instance.ActiveCell;
		_activeTileKey = GameManager.Instance.CurrentActiveTile;

		if (_cellData == null || _activeTileKey == null) 
		{
			GD.PrintErr("[LocalMapGenerator] Error crítico: No hay celda o ATK activa.");
			return;
		}

		_rng = new RandomNumberGenerator();
		_rng.Seed = (ulong)(GameManager.Instance.GlobalSeed + _cellData.Id);

		_terrainNoise = new FastNoiseLite();
		_terrainNoise.Seed = GameManager.Instance.GlobalSeed + _cellData.Id;
		_terrainNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		_terrainNoise.Frequency = 0.008f; 

		BuildScene();
		ConfigureCameraAndLimits();
	}

	public override void _Process(double delta)
	{
		if (_sunLight != null) _sunLight.RotateX(LocalDayNightSpeed * (float)delta);
	}

	private void BuildScene()
	{
		GD.Print($"--- CARGANDO SECTOR TÁCTICO: TILE #{_cellData.Id} [{_cellData.Biome}] ---");

		float planetRadius = 100.0f;
		var planetNode = GetTree().Root.FindChild("Planet", true, false) as Planet;
		if (planetNode != null) planetRadius = planetNode.Radius;

		// 1. Usamos nuestro CellGeometryProvider unificado para extraer la geometría limpia
		var geometryProvider = new CellGeometryProvider(_cellData, planetRadius, 120.0f * MeshScale);
		_localPolygonVertices = geometryProvider.GetLocalPolygon();
		_terrainRadius = geometryProvider.GetMaxRadius();

		// 2. Terreno Poligonal
		var terrainMesh = PolygonalMeshBuilder.BuildTerrainMesh(_cellData, planetRadius, MeshScale, _terrainNoise, out Vector3 centerPos, out _terrainRadius);
		var terrainInstance = new MeshInstance3D { Mesh = terrainMesh };

		var mat = new StandardMaterial3D();
		mat.AlbedoTexture = GenerateTopographicTexture();
		mat.Uv1Scale = new Vector3(4.0f, 4.0f, 4.0f);
		terrainInstance.MaterialOverride = mat;
		AddChild(terrainInstance);

		// 3. AGUA FIEL A LA BASAL KEY (BK) DE LA CELDA
		// Evaluamos la elevación y si el bioma o sus conexiones indican agua real
		bool hasWater = _cellData.Elevation <= 0.45f || _cellData.HasSeaConnection || _cellData.Biome.Contains("Océano") || _cellData.Biome.Contains("Costa") || _cellData.Biome.Contains("Lago");
		
		// Si la celda es tierra alta pero tiene un lago interno registrado, permitimos agua parcial
		float targetWaterLevel = hasWater ? WaterLevel : -200.0f; 

		var waterMesh = PolygonalMeshBuilder.BuildWaterMesh(_cellData, planetRadius, MeshScale, targetWaterLevel);
		var waterMat = new StandardMaterial3D 
		{ 
			AlbedoColor = new Color(0.12f, 0.45f, 0.85f, 0.75f), 
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			Roughness = 0.05f,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		var waterInstance = new MeshInstance3D { Mesh = waterMesh, MaterialOverride = waterMat };
		AddChild(waterInstance);

		PopulateResources();
		SpawnThreats();

		_sunLight = new DirectionalLight3D { RotationDegrees = new Vector3(-45, 45, 0), ShadowEnabled = true };
		AddChild(_sunLight);
	}

	private ImageTexture GenerateTopographicTexture()
	{
		int size = 512;
		var img = Image.CreateEmpty(size, size, false, Image.Format.Rgb8);
		
		Color baseColor = new Color(0.85f, 0.80f, 0.68f); 
		Color lineColor = new Color(0.3f, 0.22f, 0.15f); 
		Color thickLineColor = new Color(0.15f, 0.1f, 0.05f); 

		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float nx = (x / (float)size) * 10.0f;
				float nz = (y / (float)size) * 10.0f;
				float noiseVal = (_terrainNoise.GetNoise2D(nx * 15.0f, nz * 15.0f) + 1.0f) * 0.5f;
				float heightSample = noiseVal * 40.0f; 
				
				float contourInterval = 4.0f; 
				float mod = Mathf.Abs(heightSample % contourInterval);
				
				Color pixelColor = baseColor;
				if (mod < 0.12f) 
				{
					if (Mathf.Abs((heightSample % (contourInterval * 5.0f))) < 0.2f)
						pixelColor = thickLineColor; 
					else
						pixelColor = lineColor;
				}
				
				img.SetPixel(x, y, pixelColor);
			}
		}
		return ImageTexture.CreateFromImage(img);
	}

	private void PopulateResources()
{
	int targetTrees = Mathf.RoundToInt(150 * _cellData.ArableArea);

	for (int i = 0; i < targetTrees; i++)
	{
		// Generamos un punto aleatorio dentro del radio del terreno
		float rx = _rng.RandfRange(-0.9f, 0.9f) * _terrainRadius;
		float rz = _rng.RandfRange(-0.9f, 0.9f) * _terrainRadius;
		Vector2 candidatePos = new Vector2(rx, rz);

		// Validamos estrictamente que el punto esté DENTRO del polígono del hexágono/pentágono
		if (_localPolygonVertices != null && _localPolygonVertices.Length >= 3)
		{
			candidatePos = PolygonBoundaryConstraint.ConstrainPosition(candidatePos, _localPolygonVertices);
			// Si el punto fue forzado a moverse mucho hacia el borde, podemos omitirlo para que no se amontone
			if (candidatePos.DistanceTo(new Vector2(rx, rz)) > 2.0f) continue;
		}

		Vector3 pos = new Vector3(candidatePos.X, 0, candidatePos.Y);
		float groundY = PolygonalMeshBuilder.GetHeight(pos, _cellData, _terrainNoise);

		// RESTRICCIÓN DE AGUA: Si la altura del suelo está bajo el nivel del agua, no nace el árbol
		bool hasWater = _cellData.Elevation <= 0.45f || _cellData.HasSeaConnection || _cellData.Biome.Contains("Océano") || _cellData.Biome.Contains("Costa") || _cellData.Biome.Contains("Lago");
		float currentWaterThreshold = hasWater ? WaterLevel : -999.0f;

		if (groundY < currentWaterThreshold + 0.5f) continue; // Evita que nazcan en el agua
		float treeHeight = _rng.RandfRange(2.5f, 4.5f);
		var tree = new CsgBox3D { Size = new Vector3(1.2f, treeHeight, 1.2f), Position = new Vector3(pos.X, groundY + (treeHeight / 2.0f) - 0.4f, pos.Z) };
		tree.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.35f, 0.12f) };
		AddChild(tree);
	}
}

private void SpawnThreats()
{
	for (int i = 0; i < _cellData.ThreatLevel; i++)
	{
		float rx = _rng.RandfRange(-0.9f, 0.9f) * _terrainRadius;
		float rz = _rng.RandfRange(-0.9f, 0.9f) * _terrainRadius;
		Vector2 candidatePos = new Vector2(rx, rz);

		if (_localPolygonVertices != null && _localPolygonVertices.Length >= 3)
		{
			candidatePos = PolygonBoundaryConstraint.ConstrainPosition(candidatePos, _localPolygonVertices);
			if (candidatePos.DistanceTo(new Vector2(rx, rz)) > 2.0f) continue;
		}

		Vector3 pos = new Vector3(candidatePos.X, 0, candidatePos.Y);
		float groundY = PolygonalMeshBuilder.GetHeight(pos, _cellData, _terrainNoise);

		bool hasWater = _cellData.Elevation <= 0.42f || _cellData.HasSeaConnection || _cellData.Biome.Contains("Océano") || _cellData.Biome.Contains("Costa") || _cellData.Biome.Contains("Lago");
		float currentWaterThreshold = hasWater ? WaterLevel : -999.0f;

		if (groundY < currentWaterThreshold + 0.8f) continue;

		float enemySize = 1.8f;
		var enemy = new CsgBox3D { Size = new Vector3(enemySize, enemySize, enemySize), Position = new Vector3(pos.X, groundY + (enemySize / 2.0f), pos.Z) };
		enemy.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.12f, 0.12f) };
		AddChild(enemy);
	}
}

	private void ConfigureCameraAndLimits()
	{
		var camera = GetTree().Root.FindChild("TacticalCamera", true, false) as TacticalCamera;
		if (camera != null && _localPolygonVertices != null)
		{
			// Inyectamos el polígono exacto del CellGeometryProvider directamente a la cámara
			camera.SetMapBoundaries(_localPolygonVertices);
			camera.GlobalPosition = new Vector3(0, camera.GlobalPosition.Y + 20.0f, 0);
			GD.Print($"[LocalMapGenerator] Polígono inyectado a la cámara con éxito. Vértices: {_localPolygonVertices.Length}");
		}
	}
	public override void _Input(InputEvent @event)
	{
		// Al presionar Escape salimos del tile táctico y volvemos al mundo (esfera)
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			ReturnToWorld();
		}
	}

	private void ReturnToWorld()
	{
		GD.Print("[LocalMapGenerator] Saliendo del sector táctico. Regresando al planeta...");
		GetTree().ChangeSceneToFile("res://Scenes/World/Planet.tscn");
	}
}
