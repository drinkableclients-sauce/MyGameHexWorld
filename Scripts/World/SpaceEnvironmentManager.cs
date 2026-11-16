using Godot;

public partial class SpaceEnvironmentManager : Node3D
{
	[Export] public DirectionalLight3D SunLight;
	[Export] public Node3D PlanetNode;

	[Export(PropertyHint.Range, "0.0, 5.0")] public float SunIntensity = 2.5f;
	[Export(PropertyHint.Range, "0.0, 3.0")] public float AtmosphereIntensity = 1.8f;
	[Export(PropertyHint.Range, "0.0, 1.0")] public float OrbitSpeed = 0.03f;

	private MeshInstance3D _atmosphereMesh;
	private Node3D _starsContainer;
	private float _currentAngle = 0.0f;
	private ShaderMaterial _pixelRectMaterial;

	public override void _Ready()
{
	SetupSpaceBackground();
	SetupStarField();
	SetupRealisticAtmosphere();

	// Buscar el ColorRect con el shader de pixel art en la escena para controlarlo desde la toolbar
	var colorRect = GetNodeOrNull<ColorRect>("CanvasLayer/ColorRect");
	if (colorRect != null && colorRect.Material is ShaderMaterial mat)
	{
		_pixelRectMaterial = mat;
	}

	BuildToolbarUI();
}

	public override void _Process(double delta)
	{
		// Rotación del Sol para el ciclo de día y noche
		if (SunLight != null)
		{
			_currentAngle += (float)(delta * OrbitSpeed);
			float x = Mathf.Sin(_currentAngle) * 60.0f;
			float z = Mathf.Cos(_currentAngle) * 60.0f;
			SunLight.GlobalPosition = new Vector3(x, 15.0f, z);
			SunLight.LookAt(Vector3.Zero, Vector3.Up);
			SunLight.LightEnergy = SunIntensity;
		}

		// Dinámica de la atmósfera
		if (_atmosphereMesh != null && _atmosphereMesh.MaterialOverride is StandardMaterial3D mat)
		{
			mat.Rim = 0.5f * AtmosphereIntensity;
			mat.EmissionEnergyMultiplier = 0.8f * AtmosphereIntensity;
		}
	}

	private void SetupSpaceBackground()
	{
		var envNode = GetNodeOrNull<WorldEnvironment>("WorldEnvironment");
		if (envNode == null)
		{
			envNode = new WorldEnvironment();
			var env = new Environment();
			env.BackgroundMode = Environment.BGMode.Color;
			env.BackgroundColor = new Color(0.002f, 0.002f, 0.005f); // Negro espacio profundo absoluto
			env.GlowEnabled = true;
			env.GlowIntensity = 1.2f;
			envNode.Environment = env;
			AddChild(envNode);
		}
	}

	private void SetupStarField()
	{
		// Generar un campo de estrellas tridimensional masivo alrededor de la escena
		_starsContainer = new Node3D { Name = "StarField" };
		var rng = new RandomNumberGenerator();
		rng.Seed = 999;

		int starCount = 400;
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Points);

		for (int i = 0; i < starCount; i++)
		{
			// Posicionar estrellas en una esfera gigante lejana (Radio ~250m)
			Vector3 dir = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)).Normalized();
			float distance = rng.RandfRange(200.0f, 300.0f);
			Vector3 starPos = dir * distance;

			st.AddVertex(starPos);
		}

		var starMesh = st.Commit();
		var starInstance = new MeshInstance3D { Mesh = starMesh };

		var starMat = new StandardMaterial3D();
		starMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		starMat.AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
		starMat.PointSize = 3.0f; // Tamaño visible de las estrellas en píxeles
		starMat.EmissionEnabled = true;
		starMat.Emission = new Color(0.9f, 0.95f, 1.0f);
		starMat.EmissionEnergyMultiplier = 3.0f; // Hace que brillen intensamente contra el fondo negro

		starInstance.MaterialOverride = starMat;
		_starsContainer.AddChild(starInstance);
		AddChild(_starsContainer);
	}

	private void SetupRealisticAtmosphere()
	{
		// Crear la capa atmosférica envolvente con efecto de horizonte luminoso
		_atmosphereMesh = new MeshInstance3D();
		var sphereMesh = new SphereMesh();
		
		sphereMesh.Radius = 33.2f; // Ligeramente mayor al radio del planeta (32.0f)
		sphereMesh.Height = 66.4f;
		_atmosphereMesh.Mesh = sphereMesh;

		var atmosMat = new StandardMaterial3D();
		atmosMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.PerPixel;
		atmosMat.AlbedoColor = new Color(0.15f, 0.45f, 0.9f, 0.05f);
		atmosMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		atmosMat.BlendMode = BaseMaterial3D.BlendModeEnum.Add;
		
		// Efecto Fresnel para que la atmósfera brille intensamente solo en los bordes (horizonte)
		atmosMat.RimEnabled = true;
		atmosMat.Rim = 1.0f;
		atmosMat.RimTint = 0.2f;
		
		atmosMat.EmissionEnabled = true;
		atmosMat.Emission = new Color(0.2f, 0.5f, 1.0f);
		atmosMat.EmissionEnergyMultiplier = 1.5f;

		_atmosphereMesh.MaterialOverride = atmosMat;
		AddChild(_atmosphereMesh);
	}

	private void BuildToolbarUI()
	{
		var canvasLayer = new CanvasLayer();
		var panel = new PanelContainer();
		panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		panel.OffsetLeft = -250;
		panel.OffsetTop = 20;
		panel.OffsetRight = -20;
		panel.OffsetBottom = 260; // Expandido para los nuevos controles

		var vbox = new VBoxContainer();

		// Control de Sol
		vbox.AddChild(new Label { Text = "Intensidad Sol:" });
		var sliderSun = new HSlider { MinValue = 0, MaxValue = 5, Step = 0.1, Value = SunIntensity };
		sliderSun.ValueChanged += (val) => SunIntensity = (float)val;
		vbox.AddChild(sliderSun);

		// Control de Atmósfera
		vbox.AddChild(new Label { Text = "Intensidad Atmosfera:" });
		var sliderAtmos = new HSlider { MinValue = 0, MaxValue = 3, Step = 0.05, Value = AtmosphereIntensity };
		sliderAtmos.ValueChanged += (val) => AtmosphereIntensity = (float)val;
		vbox.AddChild(sliderAtmos);

		// --- NUEVOS CONTROLES DE PIXEL ART ---
		vbox.AddChild(new Label { Text = "Tamaño de Píxel:" });
		var sliderPixel = new HSlider { MinValue = 1, MaxValue = 10, Step = 1, Value = 3 };
		sliderPixel.ValueChanged += (val) => {
			if (_pixelRectMaterial != null) _pixelRectMaterial.SetShaderParameter("pixel_size", (float)val);
		};
		vbox.AddChild(sliderPixel);

		vbox.AddChild(new Label { Text = "Vitalidad Colores (Saturación):" });
		var sliderSat = new HSlider { MinValue = 0.5, MaxValue = 2.0, Step = 0.05, Value = 1.25 };
		sliderSat.ValueChanged += (val) => {
			if (_pixelRectMaterial != null) _pixelRectMaterial.SetShaderParameter("saturation", (float)val);
		};
		vbox.AddChild(sliderSat);

		panel.AddChild(vbox);
		canvasLayer.AddChild(panel);
		AddChild(canvasLayer);
	}
}
