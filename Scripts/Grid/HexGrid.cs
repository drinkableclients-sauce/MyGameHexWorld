using Godot;
using System.Collections.Generic;

public class HexCell
{
	public int Id;
	public Vector3 Position; 
	public Vector3 Normal;    
	
	// --- EL ACTA DE NACIMIENTO (BASAL KEY INMUTABLE) ---
	public BasalKey BirthKey;

	// Propiedades de conveniencia enlazadas directamente al ADN de la BasalKey
	public float Elevation => BirthKey != null ? BirthKey.BaseElevation : 0f;
	public float Temperature => 1.0f - Mathf.Abs(Normal.Y); 
	public string Biome => BirthKey != null ? BirthKey.Biome : "Desconocido";
	public bool IsBuildable => BirthKey != null && BirthKey.BuildableArea > 0.3f; 
	public float ArableArea => BirthKey != null ? BirthKey.ArableArea : 0f; 
	public int ThreatLevel => BirthKey != null ? BirthKey.ThreatLevel : 0; 
	
	// Datos de congruencia de agua heredados de la BasalKey
	public bool HasSeaConnection => BirthKey != null && BirthKey.WaterRatio > 0.4f;
	public bool NorthWater { get; set; }
	public bool SouthWater { get; set; }
	public bool EastWater { get; set; }
	public bool WestWater { get; set; }
	
	public List<Vector3[]> TileEdges = new List<Vector3[]>(); 
}

[Tool]
public partial class HexGrid : Node3D
{
	[Export] public Planet PlanetNode; 
	[Export(PropertyHint.Range, "1, 5")] public int SubdivisionLevel = 3; 
	
	[ExportSubgroup("Visuals & Debug")]
	[Export] public bool ShowGridTiles = true; 
	
	// NUEVO: Variables para controlar qué tan alto flotan las mallas sobre el suelo
	[Export] public float GridHoverOffset = 0.15f; 
	[Export] public float CursorHoverOffset = 0.25f; 
	
	[Export] public bool ForceGenerate { get => false; set { if (value) GenerateGrid(); } }

	public List<HexCell> Cells = new List<HexCell>();
	
	private MeshInstance3D _gridTilesNode;
	private StaticBody3D _planetCollider;
	private MeshInstance3D _cursorNode;

	public override void _Ready()
	{
		GenerateGrid();
	}

	public void GenerateGrid()
	{
		if (PlanetNode == null || PlanetNode.Noise == null || PlanetNode.MoistureNoise == null) return;
		
		Cells.Clear();
		GenerateGeodesicData(SubdivisionLevel, out List<Vector3> verts, out List<int> indices);

		for (int i = 0; i < verts.Count; i++)
		{
			Cells.Add(CalculateCellData(i, verts[i]));
		}

		BuildPerfectTiles(verts, indices);

		if (!Engine.IsEditorHint()) 
		{
			CreatePlanetCollider();
			CreateSelectionCursor();
		}

		if (ShowGridTiles) DrawGridTiles();
		else if (_gridTilesNode != null) _gridTilesNode.QueueFree();
	}

	private void BuildPerfectTiles(List<Vector3> verts, List<int> indices)
	{
		var edges = new Dictionary<long, List<Vector3>>();
		
		for (int i = 0; i < indices.Count; i += 3)
		{
			int v1 = indices[i], v2 = indices[i+1], v3 = indices[i+2];
			Vector3 triCenter = (verts[v1] + verts[v2] + verts[v3]).Normalized();
			
			AddEdge(edges, v1, v2, triCenter);
			AddEdge(edges, v2, v3, triCenter);
			AddEdge(edges, v3, v1, triCenter);
		}

		foreach (var kvp in edges)
		{
			if (kvp.Value.Count == 2)
			{
				int vA = (int)(kvp.Key >> 32);
				int vB = (int)(kvp.Key & 0xFFFFFFFF);
				ProcessTileEdge(Cells[vA], kvp.Value[0], kvp.Value[1]);
				ProcessTileEdge(Cells[vB], kvp.Value[0], kvp.Value[1]);
			}
		}
	}

	private void AddEdge(Dictionary<long, List<Vector3>> edges, int v1, int v2, Vector3 center)
	{
		long key = v1 < v2 ? ((long)v1 << 32) | (uint)v2 : ((long)v2 << 32) | (uint)v1;
		if (!edges.ContainsKey(key)) edges[key] = new List<Vector3>();
		edges[key].Add(center);
	}

	private void ProcessTileEdge(HexCell cell, Vector3 c1, Vector3 c2)
	{
		Vector3 normal = cell.Normal;
		if ((c1 - normal).Cross(c2 - normal).Dot(normal) < 0)
		{
			Vector3 temp = c1; c1 = c2; c2 = temp;
		}
		cell.TileEdges.Add(new Vector3[] { c1, c2 });
	}

	// ==========================================================
	// DIBUJO DE LA RED (Usando el nuevo GridHoverOffset)
	// ==========================================================
	private void DrawGridTiles()
	{
		if (_gridTilesNode != null) _gridTilesNode.QueueFree();
		_gridTilesNode = new MeshInstance3D { Name = "GridTiles" };
		
		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		foreach (var cell in Cells)
		{
			// APLICAMOS EL OFFSET CONTROLABLE PARA QUE NO SE HUNDA EN EL TERRENO
			Vector3 centerPos = cell.Position + (cell.Normal * GridHoverOffset); 
			
			foreach (var edge in cell.TileEdges)
			{
				Vector3 p1 = (cell.Normal + (edge[0] - cell.Normal) * 0.90f).Normalized() * centerPos.Length();
				Vector3 p2 = (cell.Normal + (edge[1] - cell.Normal) * 0.90f).Normalized() * centerPos.Length();

				st.SetNormal(cell.Normal);
				st.AddVertex(centerPos);
				st.AddVertex(p1);
				st.AddVertex(p2);
			}
		}

		_gridTilesNode.Mesh = st.Commit();
		_gridTilesNode.MaterialOverride = new StandardMaterial3D { 
		AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.02f), 
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha, 
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled // <--- ESTA ES LA MAGIA
	};
		AddChild(_gridTilesNode);
	}

	private void GenerateGeodesicData(int subdivisions, out List<Vector3> verts, out List<int> indices)
	{
		float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
		verts = new List<Vector3>() {
			new Vector3(-1,  t,  0).Normalized(), new Vector3( 1,  t,  0).Normalized(),
			new Vector3(-1, -t,  0).Normalized(), new Vector3( 1, -t,  0).Normalized(),
			new Vector3( 0, -1,  t).Normalized(), new Vector3( 0,  1,  t).Normalized(),
			new Vector3( 0, -1, -t).Normalized(), new Vector3( 0,  1, -t).Normalized(),
			new Vector3( t,  0, -1).Normalized(), new Vector3( t,  0,  1).Normalized(),
			new Vector3(-t,  0, -1).Normalized(), new Vector3(-t,  0,  1).Normalized()
		};

		indices = new List<int>() {
			0, 11, 5,  0, 5, 1,  0, 1, 7,  0, 7, 10,  0, 10, 11,
			1, 5, 9,   5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
			3, 9, 4,   3, 4, 2,  3, 2, 6,  3, 6, 8,   3, 8, 9,
			4, 9, 5,   2, 4, 11, 6, 2, 10, 8, 6, 7,   9, 8, 1
		};

		var cache = new Dictionary<long, int>();
		for (int i = 0; i < subdivisions; i++)
		{
			var newIndices = new List<int>();
			for (int j = 0; j < indices.Count; j += 3)
			{
				int v1 = indices[j], v2 = indices[j+1], v3 = indices[j+2];
				int a = GetMidpoint(v1, v2, verts, cache), b = GetMidpoint(v2, v3, verts, cache), c = GetMidpoint(v3, v1, verts, cache);
				newIndices.AddRange(new[] { v1, a, c }); newIndices.AddRange(new[] { v2, b, a });
				newIndices.AddRange(new[] { v3, c, b }); newIndices.AddRange(new[] { a, b, c });
			}
			indices = newIndices;
		}
	}

	private int GetMidpoint(int p1, int p2, List<Vector3> verts, Dictionary<long, int> cache)
	{
		long key = p1 < p2 ? ((long)p1 << 32) | (uint)p2 : ((long)p2 << 32) | (uint)p1;
		if (cache.TryGetValue(key, out int ret)) return ret;
		verts.Add((verts[p1] + verts[p2]).Normalized());
		cache.Add(key, verts.Count - 1);
		return verts.Count - 1;
	}

	private HexCell CalculateCellData(int id, Vector3 dir)
	{
		// 1. Obtenemos los parámetros globales del WorldSettings (o valores por defecto si no existe)
		long globalSeed = WorldSettings.Instance != null ? WorldSettings.Instance.GlobalSeed : 1337;
		float seaLevel = WorldSettings.Instance != null ? WorldSettings.Instance.SeaLevelThreshold : PlanetNode.WaterLevel;
		float roughness = WorldSettings.Instance != null ? WorldSettings.Instance.RoughnessMultiplier : 1.0f;

		// 2. Determinamos si es un pentágono (los primeros 12 vértices del icosaedro base)
		bool isPentagon = id < 12;

		// 3. INVOCAMOS EL GÉNESIS: Creamos y sellamos el Acta de Nacimiento (BasalKey) de la celda
		BasalKey basalKey = new BasalKey();
		basalKey.GenerateGenesis(globalSeed, id, dir, isPentagon, seaLevel, roughness);

		// 4. Calculamos la posición tridimensional final sobre la esfera usando la elevación de su nacimiento
		Vector3 finalPosition = dir * (PlanetNode.Radius + (basalKey.BaseElevation * PlanetNode.Height));

		// 5. Construimos la HexCell vinculándola con su BasalKey
		HexCell cell = new HexCell {
			Id = id, 
			Position = finalPosition, 
			Normal = dir,
			BirthKey = basalKey
		};

		// Sincronizamos las aristas para que la BasalKey y la Celda compartan la geometría
		basalKey.TileEdges = cell.TileEdges;

		return cell;
	}

	private void CreatePlanetCollider()
	{
		if (_planetCollider != null) _planetCollider.QueueFree();
		_planetCollider = new StaticBody3D();
		_planetCollider.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = PlanetNode.Radius + (PlanetNode.Height * 0.5f) } });
		AddChild(_planetCollider);
	}

	private void CreateSelectionCursor()
	{
		if (_cursorNode != null) _cursorNode.QueueFree();
		_cursorNode = new MeshInstance3D { Name = "HexCursor" };
		_cursorNode.MaterialOverride = new StandardMaterial3D {
		AlbedoColor = new Color(1.0f, 0.8f, 0.0f, 0.7f), 
		Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
		ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
		CullMode = BaseMaterial3D.CullModeEnum.Disabled // <--- MAGIA APLICADA AQUÍ TAMBIÉN
	};
		_cursorNode.Visible = false;
		AddChild(_cursorNode);
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!Engine.IsEditorHint() && @event is InputEventMouseButton mouseEvent && mouseEvent.Pressed && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			var camera = GetViewport().GetCamera3D();
			if (camera == null) return;
			var query = PhysicsRayQueryParameters3D.Create(camera.ProjectRayOrigin(mouseEvent.Position), camera.ProjectRayOrigin(mouseEvent.Position) + camera.ProjectRayNormal(mouseEvent.Position) * 1000.0f);
			var result = GetWorld3D().DirectSpaceState.IntersectRay(query);
			if (result.Count > 0) SelectCell((Vector3)result["position"]);
		}
	}

	// ==========================================================
	// DIBUJO DEL CURSOR DE SELECCIÓN (Usando CursorHoverOffset)
	// ==========================================================
	private void SelectCell(Vector3 hitPos)
	{
		HexCell closestCell = null;
		float minDistance = float.MaxValue;
		foreach (var cell in Cells) {
			float dist = hitPos.DistanceSquaredTo(cell.Position);
			if (dist < minDistance) { minDistance = dist; closestCell = cell; }
		}

		if (closestCell != null)
		{
			var st = new SurfaceTool();
			st.Begin(Mesh.PrimitiveType.Triangles);
			
			// APLICAMOS EL OFFSET PARA QUE EL CURSOR RESALTE POR ENCIMA DE TODO
			Vector3 centerPos = closestCell.Position + (closestCell.Normal * CursorHoverOffset); 
			
			foreach (var edge in closestCell.TileEdges)
			{
				Vector3 p1 = (closestCell.Normal + (edge[0] - closestCell.Normal) * 0.98f).Normalized() * centerPos.Length();
				Vector3 p2 = (closestCell.Normal + (edge[1] - closestCell.Normal) * 0.98f).Normalized() * centerPos.Length();
				st.SetNormal(closestCell.Normal);
				st.AddVertex(centerPos);
				st.AddVertex(p1);
				st.AddVertex(p2);
			}
			
			_cursorNode.Mesh = st.Commit();
			_cursorNode.Transform = Transform3D.Identity;
			_cursorNode.Visible = true;

			GD.Print("\n--- REPORTE DE TERRENO ---");
			GD.Print($"Celda ID: {closestCell.Id} | Geometría: {(closestCell.TileEdges.Count == 5 ? "Pentágono" : "Hexágono")}");
			GD.Print($"Bioma: {closestCell.Biome}");
			GD.Print($"Área Cultivable: {(closestCell.ArableArea * 100).ToString("0")}%");
			GD.Print($"Peligro (Amenaza): {closestCell.ThreatLevel}/10");
			// Guardamos la celda en el Singleton para que el mapa local la lea
			GameManager.Instance.ActiveCell = closestCell;
			GD.Print(">>> PRESIONA 'ENTER' PARA VIAJAR A ESTE SECTOR <<<");
		}
	}
	public override void _Input(InputEvent @event)
	{
		if (!Engine.IsEditorHint() && @event is InputEventKey keyEvent && keyEvent.Pressed)
		{
			// Si el jugador presiona Enter y hay una celda seleccionada en el GameManager
			if (keyEvent.Keycode == Key.Enter && GameManager.Instance.ActiveCell != null)
			{
				GameManager.Instance.EnterTile(GameManager.Instance.ActiveCell);
			}
		}
	}
}
