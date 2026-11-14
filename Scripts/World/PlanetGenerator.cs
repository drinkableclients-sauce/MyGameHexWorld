using Godot;
using Godot.Collections;

public partial class PlanetGenerator : Node3D
{
	[Export] public float PlanetRadius { get; set; } = 10.0f;
	[Export] public int SubdivisionLevel { get; set; } = 2;

	public Dictionary<int, BasalKey> PlanetCells { get; private set; } = new Dictionary<int, BasalKey>();
	private Dictionary<int, Vector3> _cellPositions = new Dictionary<int, Vector3>();

	public override void _Ready()
	{
		GeneratePlanet();
	}

	private void GeneratePlanet()
	{
		int genesisSeed = GameManager.Instance != null ? GameManager.Instance.GenesisSeed : 12345;
		var worldConfig = GameManager.Instance != null ? GameManager.Instance.CurrentWorldConfig : new WorldConfig();

		GD.Print($"[PlanetGenerator] Generando planeta | Semilla: {genesisSeed} | Continentes: {worldConfig.LandmassType} | Dificultad: {worldConfig.Difficulty}");

		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)genesisSeed;

		int totalCellsCount = 42;
		Vector3 pangeaCenter = new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-0.5f, 0.5f), rng.RandfRange(-1, 1)).Normalized();

		PlanetCells.Clear();
		_cellPositions.Clear();

		for (int i = 0; i < totalCellsCount; i++)
		{
			float phi = Mathf.Acos(1.0f - (2.0f * i + 1.0f) / totalCellsCount);
			float theta = Mathf.Sqrt(totalCellsCount * Mathf.Pi) * phi;

			Vector3 cellPosition = new Vector3(
				PlanetRadius * Mathf.Sin(phi) * Mathf.Cos(theta),
				PlanetRadius * Mathf.Cos(phi),
				PlanetRadius * Mathf.Sin(phi) * Mathf.Sin(theta)
			);

			if (worldConfig.LandmassType == WorldLandmassType.Pangea)
			{
				Vector3 normalizedPos = cellPosition.Normalized();
				normalizedPos = normalizedPos.Slerp(pangeaCenter, 0.35f);
				cellPosition = normalizedPos * PlanetRadius;
			}
			else if (worldConfig.LandmassType == WorldLandmassType.Archipiélago)
			{
				cellPosition += new Vector3(rng.RandfRange(-1.5f, 1.5f), rng.RandfRange(-1.5f, 1.5f), rng.RandfRange(-1.5f, 1.5f));
				cellPosition = cellPosition.Normalized() * PlanetRadius;
			}

			// Guardar posición local de la celda
			_cellPositions[i] = cellPosition;

			// Crear y poblar el ADN de la celda (BasalKey)
			BasalKey cellKey = new BasalKey();
			cellKey.Initialize(i, genesisSeed, cellPosition);

			PlanetCells.Add(i, cellKey);
			// --- REPRESENTACIÓN VISUAL RÁPIDA EN LA ESFERA ---
			var cellVisual = new MeshInstance3D();
			cellVisual.Mesh = new SphereMesh { Radius = 0.8f, Height = 1.6f };
			cellVisual.Position = cellPosition;

			// Color temporal según si es agua o tierra firme para distinguirlas a simple vista
			var mat = new StandardMaterial3D();
			mat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
			mat.AlbedoColor = cellKey.Biome.Contains("Océano") ? new Color(0.2f, 0.4f, 0.8f) : new Color(0.3f, 0.7f, 0.3f);
			cellVisual.MaterialOverride = mat;

			AddChild(cellVisual);
			// ------------------------------------------------
		}

		GD.Print($"[PlanetGenerator] ¡Planeta esculpido con éxito! Total de celdas interactivas: {PlanetCells.Count}");
	}

	public override void _Input(InputEvent @event)
	{
		// Detectar clic izquierdo del mouse para seleccionar una celda del planeta
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			Camera3D camera = GetViewport().GetCamera3D();
			if (camera == null) return;

			Vector2 mousePos = mouseEvent.Position;
			Vector3 rayOrigin = camera.ProjectRayOrigin(mousePos);
			Vector3 rayDir = camera.ProjectRayNormal(mousePos);

			// Intersección con la esfera del planeta
			Vector3 sphereCenter = GlobalPosition;
			Vector3 m = rayOrigin - sphereCenter;
			float b = m.Dot(rayDir);
			float c = m.Dot(m) - (PlanetRadius * PlanetRadius);

			if (c > 0.0f && b > 0.0f) return;
			float discriminant = b * b - c;
			if (discriminant < 0.0f) return;

			float t = -b - Mathf.Sqrt(discriminant);
			if (t < 0.0f) t = 0.0f;

			Vector3 hitPoint = rayOrigin + rayDir * t;

			// Encontrar la celda más cercana al punto de impacto del rayo
			int closestCellId = -1;
			float minDistance = float.MaxValue;

			foreach (var kvp in _cellPositions)
			{
				float dist = kvp.Value.DistanceTo(hitPoint);
				if (dist < minDistance)
				{
					minDistance = dist;
					closestCellId = kvp.Key;
				}
			}

			// Si encontramos una celda válida y está a una distancia razonable del impacto
			if (closestCellId != -1 && minDistance < 5.0f)
			{
				BasalKey selectedCell = GetCellData(closestCellId);
				if (selectedCell != null)
				{
					GD.Print($"[PlanetGenerator] ¡Celda #{selectedCell.Id} seleccionada! Bioma: {selectedCell.Biome}. Saltando al diorama local...");
					
					// Asignar al GameManager global
					if (GameManager.Instance != null)
					{
						GameManager.Instance.ActiveCell = selectedCell;
					}

					// Transicionar a la escena del Diorama Local
					GetTree().ChangeSceneToFile("res://Scenes/Local/LocalTile.tscn");
				}
			}
		}
	}

	public BasalKey GetCellData(int cellId)
	{
		if (PlanetCells.ContainsKey(cellId))
		{
			return PlanetCells[cellId];
		}
		return null;
	}
}
