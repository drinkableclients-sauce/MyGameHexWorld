using Godot;

[Tool]
public partial class Planet : Node3D
{
	public MeshInstance3D TerrainNode;
	public MeshInstance3D WaterNode;
	public MeshInstance3D CloudsNode;
	public MeshInstance3D AtmosphereNode;
	public MeshInstance3D StarsNode;
	private DirectionalLight3D _sunLight;
	private WorldEnvironment _worldEnvironment;

	[ExportSubgroup("Terrain & Biomes")]
	[Export] public FastNoiseLite Noise { get => _noise; set { BindNoise(ref _noise, value); } }
	private FastNoiseLite _noise;

	[Export] public float Radius { get => _radius; set { _radius = Mathf.Max(1.0f, value); RebuildPlanet(); } }
	private float _radius = 8.0f;
	[Export] public int Detail { get => _detail; set { _detail = Mathf.Max(4, value); RebuildPlanet(); } }
	private int _detail = 32;
	[Export] public float Height { get => _height; set { _height = Mathf.Max(0.0f, value); RebuildPlanet(); } }
	private float _height = 1.5f;
	[Export(PropertyHint.Range, "0.5, 5.0")] public float TerrainExponent { get => _terrainExponent; set { _terrainExponent = Mathf.Max(0.1f, value); RebuildPlanet(); } }
	private float _terrainExponent = 1.775f;
	
	[Export] public float MinHeightLimit = -0.2f;
	[Export] public float MaxHeightLimit = 0.8f;

	[ExportSubgroup("Environment")]
	[Export(PropertyHint.Range, "0,1")] public float WaterLevel { get => _waterLevel; set { _waterLevel = value; RebuildPlanet(); } }
	private float _waterLevel = 0.211f;

	[Export] public bool EnableClouds { get => _enableClouds; set { _enableClouds = value; RebuildPlanet(); } }
	private bool _enableClouds = true;
	[Export] public bool EnableAtmosphere { get => _enableAtmosphere; set { _enableAtmosphere = value; RebuildPlanet(); } }
	private bool _enableAtmosphere = true;
	[Export] public float DayNightSpeed = 0.05f;

	public FastNoiseLite CloudNoise;
	public FastNoiseLite MoistureNoise;

	public override void _Ready()
	{
		SetupEnvironment();
		RebuildPlanet();
	}

	public override void _Process(double delta)
	{
		if (_sunLight != null) _sunLight.RotateY(DayNightSpeed * (float)delta);
		else _sunLight = GetNodeOrNull<DirectionalLight3D>("DirectionalLight3D");

		if (CloudsNode != null && EnableClouds) CloudsNode.RotateY(DayNightSpeed * 0.3f * (float)delta);
	}

	private void BindNoise(ref FastNoiseLite field, FastNoiseLite value)
	{
		if (field != null)
		{
			var call = new Callable(this, nameof(RebuildPlanet));
			if (field.IsConnected(FastNoiseLite.SignalName.Changed, call)) field.Disconnect(FastNoiseLite.SignalName.Changed, call);
		}
		field = value;
		if (field != null)
		{
			var call = new Callable(this, nameof(RebuildPlanet));
			if (!field.IsConnected(FastNoiseLite.SignalName.Changed, call)) field.Connect(FastNoiseLite.SignalName.Changed, call);
		}
		RebuildPlanet();
	}

	private void SetupEnvironment()
	{
		if (_worldEnvironment == null)
		{
			_worldEnvironment = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
			if (_worldEnvironment == null)
			{
				_worldEnvironment = new WorldEnvironment { Name = "WorldEnvironment" };
				var env = new Godot.Environment();
				env.BackgroundMode = Godot.Environment.BGMode.Color;
				env.BackgroundColor = Colors.Black;
				env.GlowEnabled = true;
				env.GlowIntensity = 0.8f;
				_worldEnvironment.Environment = env;
				AddChild(_worldEnvironment);
			}
		}
	}

	public void RebuildPlanet()
	{
		if (TerrainNode == null) { TerrainNode = GetNodeOrNull<MeshInstance3D>("Terrain") ?? new MeshInstance3D { Name = "Terrain" }; if (TerrainNode.GetParent() == null) AddChild(TerrainNode); }
		if (WaterNode == null) { WaterNode = GetNodeOrNull<MeshInstance3D>("Water") ?? new MeshInstance3D { Name = "Water" }; if (WaterNode.GetParent() == null) AddChild(WaterNode); }
		if (CloudsNode == null) { CloudsNode = GetNodeOrNull<MeshInstance3D>("Clouds") ?? new MeshInstance3D { Name = "Clouds" }; if (CloudsNode.GetParent() == null) AddChild(CloudsNode); }
		if (AtmosphereNode == null) { AtmosphereNode = GetNodeOrNull<MeshInstance3D>("Atmosphere") ?? new MeshInstance3D { Name = "Atmosphere" }; if (AtmosphereNode.GetParent() == null) AddChild(AtmosphereNode); }
		if (StarsNode == null) { StarsNode = GetNodeOrNull<MeshInstance3D>("Stars") ?? new MeshInstance3D { Name = "Stars" }; if (StarsNode.GetParent() == null) AddChild(StarsNode); }

		if (_noise == null) _noise = new FastNoiseLite { Seed = 1337, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.05f };
		if (CloudNoise == null) CloudNoise = new FastNoiseLite { Seed = 8888, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, Frequency = 0.04f };
		if (MoistureNoise == null) MoistureNoise = new FastNoiseLite { Seed = 5555, NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex, Frequency = 0.04f };

		// Llamadas al nuevo generador de clima y biomas
		WeatherGenerator.UpdateTerrainMesh(this);
		WeatherGenerator.UpdateWaterMesh(this);
		WeatherGenerator.UpdateCloudsMesh(this);
		WeatherGenerator.UpdateAtmosphereMesh(this);
		WeatherGenerator.UpdateStarsMesh(this);
	}
}
