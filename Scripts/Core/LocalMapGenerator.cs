using Godot;

public partial class LocalMapGenerator : Node3D
{
	private HexCell _cellData;
	private ActiveTileKey _activeTileKey;
	private RandomNumberGenerator _rng;
	private DirectionalLight3D _sunLight;
	private FastNoiseLite _terrainNoise;

	[Export] public float LocalDayNightSpeed = 0.005f; 
	[Export] public float MeshScale = 350.0f; // ¡Ahora este valor hará que el hexágono crezca a lo grande!
	[Export] public float WaterLevel = 0.5f; 

	private float _terrainRadius = 50.0f; // Radio real calculado por el generador de mallas

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
		_terrainNoise.Frequency = 0.01f; 

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

		// 1. Terreno Poligonal (capturando el radio real exacto)
		var terrainMesh = PolygonalMeshBuilder.BuildTerrainMesh(_cellData, MeshScale, _terrainNoise, out Vector3 centerPos, out _terrainRadius);
		var terrainInstance = new MeshInstance3D { Mesh = terrainMesh };

		var mat = new StandardMaterial3D();
		mat.AlbedoTexture = GenerateTopographicTexture();
		mat.Uv1Scale = new Vector3(4.0f, 4.0f, 4.0f);
		terrainInstance.MaterialOverride = mat;
		AddChild(terrainInstance);

		// 2. Agua condicional
		bool isWaterCell = _cellData.Elevation <= 0.40f || _cellData.HasSeaConnection || _cellData.Biome.Contains("Océano") || _cellData.Biome.Contains("Costa");
		float targetWaterLevel = isWaterCell ? WaterLevel : -50.0f; 

		var waterMesh = PolygonalMeshBuilder.BuildWaterMesh(_cellData, MeshScale, targetWaterLevel);
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
				
				float contourInterval = 3.0f; 
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
		int targetTrees = Mathf.RoundToInt(250 * _cellData.ArableArea);

		for (int i = 0; i < targetTrees; i++)
		{
			// Usamos _terrainRadius real para que los árboles nazcan dentro del polígono ensanchado
			float rx = _rng.RandfRange(-0.8f, 0.8f) * _terrainRadius;
			float rz = _rng.RandfRange(-0.8f, 0.8f) * _terrainRadius;
			Vector3 pos = new Vector3(rx, 0, rz);
			
			float groundY = PolygonalMeshBuilder.GetHeight(pos, _cellData, _terrainNoise); 
			if (groundY < WaterLevel + 0.3f) continue; 

			float treeHeight = _rng.RandfRange(3.0f, 6.0f);
			var tree = new CsgBox3D { Size = new Vector3(1.5f, treeHeight, 1.5f), Position = new Vector3(pos.X, groundY + (treeHeight / 2.0f) - 0.4f, pos.Z) };
			tree.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.1f, 0.35f, 0.12f) };
			AddChild(tree);
		}
	}

	private void SpawnThreats()
	{
		for (int i = 0; i < _cellData.ThreatLevel; i++)
		{
			float rx = _rng.RandfRange(-0.8f, 0.8f) * _terrainRadius;
			float rz = _rng.RandfRange(-0.8f, 0.8f) * _terrainRadius;
			Vector3 pos = new Vector3(rx, 0, rz);
			
			float groundY = PolygonalMeshBuilder.GetHeight(pos, _cellData, _terrainNoise); 
			if (groundY < WaterLevel + 0.3f) continue; 

			float enemySize = 2.0f;
			var enemy = new CsgBox3D { Size = new Vector3(enemySize, enemySize, enemySize), Position = new Vector3(pos.X, groundY + (enemySize / 2.0f), pos.Z) };
			enemy.MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0.65f, 0.12f, 0.12f) };
			AddChild(enemy);
		}
	}

	private void ConfigureCameraAndLimits()
	{
		var camera = GetTree().Root.FindChild("TacticalCamera", true, false) as TacticalCamera;
		if (camera != null)
		{
			// La cámara usará exactamente el radio real del terreno ensanchado
			camera.MapLimit = _terrainRadius * 0.9f;
			camera.GlobalPosition = new Vector3(0, camera.GlobalPosition.Y, 0);
			GD.Print($"[LocalMapGenerator] Hexágono expandido. Radio real: {_terrainRadius}, Límite cámara: {camera.MapLimit}");
		}
	}
}
