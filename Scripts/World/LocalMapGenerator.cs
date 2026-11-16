using Godot;

public partial class LocalMapGenerator : Node3D
{
	[Export] public float MapRadius { get; set; } = 680.0f; // 1360m de diámetro total
	[Export] public float TerrainDepth { get; set; } = 150.0f; // Zócalo sólido del diorama

	private MeshInstance3D _terrainMeshInstance;

	public override void _Ready()
	{
		BasalKey activeCell = GameManager.Instance != null ? GameManager.Instance.ActiveCell : null;

		if (activeCell == null)
		{
			GD.PrintErr("[LocalMapGenerator] No hay una celda activa en el GameManager. Usando datos por defecto.");
			activeCell = new BasalKey();
			activeCell.Initialize(0, 12345, Vector3.Zero);
		}

		GD.Print($"[LocalMapGenerator] Analizando Tile #{activeCell.Id} | Bioma: {activeCell.Biome}");

		// 1. Determinar si es pentágono (5 lados) o hexágono (6 lados) según su ID o vecindad
		int sides = (activeCell.Id < 12) ? 5 : 6;

		// 2. Generar la malla del diorama de 1360m con micro-triángulos
		Mesh dioramaMesh = CreateSolidPolygonPrismMesh(sides, MapRadius, TerrainDepth, activeCell);

		// 3. Crear el MeshInstance3D y añadirlo a la escena para que sea visible
		_terrainMeshInstance = new MeshInstance3D
		{
			Name = "LocalTerrainMesh",
			Mesh = dioramaMesh,
			MaterialOverride = GetBiomeMaterial(activeCell.Biome)
		};
		AddChild(_terrainMeshInstance);

		// 4. Ajustar automáticamente la cámara para encuadrar los 1360 metros del diorama
		SetupCamera();

		GD.Print($"[LocalMapGenerator] Diorama táctico de {MapRadius * 2.0f}m generado con éxito para el Tile #{activeCell.Id}.");
	}

	private Mesh CreateSolidPolygonPrismMesh(int sides, float radius, float depth, BasalKey dna)
	{
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		var noise = new FastNoiseLite();
		noise.Seed = dna.RoughnessSeed;
		noise.Frequency = 0.0025f; // Frecuencia para detalles de micro-triángulos

		int subdivisions = 16; // Densidad de la retícula de micro-triángulos

		// Construir tapa superior con relieve procedural por FastNoiseLite
		for (int ring = 0; ring < subdivisions; ring++)
		{
			float r1 = (float)ring / subdivisions;
			float r2 = (float)(ring + 1) / subdivisions;

			int radialSegments = sides * (ring + 1);

			for (int i = 0; i < radialSegments; i++)
			{
				float angle1 = i * Mathf.Tau / radialSegments;
				float angle2 = ((i + 1) % radialSegments) * Mathf.Tau / radialSegments;

				Vector3 p1 = new Vector3(Mathf.Cos(angle1) * r1 * radius, 0, Mathf.Sin(angle1) * r1 * radius);
				Vector3 p2 = new Vector3(Mathf.Cos(angle2) * r1 * radius, 0, Mathf.Sin(angle2) * r1 * radius);
				Vector3 p3 = new Vector3(Mathf.Cos(angle1) * r2 * radius, 0, Mathf.Sin(angle1) * r2 * radius);
				Vector3 p4 = new Vector3(Mathf.Cos(angle2) * r2 * radius, 0, Mathf.Sin(angle2) * r2 * radius);

				// Elevar vértices según elevación y ruido
				p1.Y = noise.GetNoise2D(p1.X, p1.Z) * 60.0f * dna.BaseElevation;
				p2.Y = noise.GetNoise2D(p2.X, p2.Z) * 60.0f * dna.BaseElevation;
				p3.Y = noise.GetNoise2D(p3.X, p3.Z) * 60.0f * dna.BaseElevation;
				p4.Y = noise.GetNoise2D(p4.X, p4.Z) * 60.0f * dna.BaseElevation;

				// Triángulo A
				st.SetNormal(Vector3.Up);
				st.AddVertex(p1);
				st.AddVertex(p3);
				st.AddVertex(p2);

				// Triángulo B
				st.SetNormal(Vector3.Up);
				st.AddVertex(p2);
				st.AddVertex(p3);
				st.AddVertex(p4);
			}
		}

		// Zócalo lateral sólido del diorama
		Vector3[] topPerimeter = new Vector3[sides];
		Vector3[] bottomPerimeter = new Vector3[sides];
		for (int i = 0; i < sides; i++)
		{
			float angle = i * Mathf.Tau / sides;
			float x = Mathf.Cos(angle) * radius;
			float z = Mathf.Sin(angle) * radius;
			float yTop = noise.GetNoise2D(x, z) * 60.0f * dna.BaseElevation;

			topPerimeter[i] = new Vector3(x, yTop, z);
			bottomPerimeter[i] = new Vector3(x, -depth, z);
		}

		for (int i = 0; i < sides; i++)
		{
			int next = (i + 1) % sides;
			Vector3 p1 = topPerimeter[i];
			Vector3 p2 = topPerimeter[next];
			Vector3 b1 = bottomPerimeter[i];
			Vector3 b2 = bottomPerimeter[next];

			Vector3 sideNormal = (p2 - p1).Cross(b1 - p1).Normalized();

			st.SetNormal(sideNormal);
			st.AddVertex(p1); st.AddVertex(b1); st.AddVertex(p2);
			st.SetNormal(sideNormal);
			st.AddVertex(p2); st.AddVertex(b1); st.AddVertex(b2);
		}

		return st.Commit();
	}

	private StandardMaterial3D GetBiomeMaterial(string biome)
	{
		var mat = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };

		if (biome.Contains("Océano") || biome.Contains("Oceano"))
			mat.AlbedoColor = new Color(0.15f, 0.35f, 0.55f);
		else if (biome.Contains("Polar") || biome.Contains("Tundra") || biome.Contains("Helada"))
			mat.AlbedoColor = new Color(0.90f, 0.92f, 0.96f);
		else if (biome.Contains("Montaña") || biome.Contains("Montana"))
			mat.AlbedoColor = new Color(0.50f, 0.50f, 0.55f);
		else if (biome.Contains("Desierto"))
			mat.AlbedoColor = new Color(0.85f, 0.72f, 0.45f);
		else if (biome.Contains("Bosque"))
			mat.AlbedoColor = new Color(0.20f, 0.48f, 0.30f);
		else
			mat.AlbedoColor = new Color(0.30f, 0.65f, 0.38f); // Pradera Fértil

		return mat;
	}

	private void SetupCamera()
	{
		Camera3D camera = GetNodeOrNull<Camera3D>("Camera3D");
		if (camera != null)
		{
			// Posicionar la cámara a una altura adecuada para ver los 1360m de diámetro
			camera.Position = new Vector3(0, 900.0f, 900.0f);
			camera.LookAt(Vector3.Zero, Vector3.Up);
		}
	}
}
