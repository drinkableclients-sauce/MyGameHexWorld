using Godot;
using System;
using System.Collections.Generic;

public enum TacticalToolMode
{
	Inspeccionar,
	FundarAsentamiento,
	OrdenarExtraccion,
	PintarCultivo,
	LimpiarZona
}

public partial class LocalSettlementController : Node3D
{
	public event Action OnInventoryOrStateChanged;
	public event Action<string> OnStatusMessage;

	public TacticalToolMode CurrentTool { get; private set; } = TacticalToolMode.Inspeccionar;

	private LocalMapGenerator _mapGenerator;
	private LocalResourceSpawner _resourceSpawner;
	private LocalTileSelector _selector;
	private BasalKey _activeCell;

	private HashSet<int> _harvestOrderedTriangles = new HashSet<int>();
	private HashSet<int> _farmZoneTriangles = new HashSet<int>();

	private MeshInstance3D _zonesMeshInstance;
	private MeshInstance3D _townHallInstance;
	private Timer _productionTimer;

	public override void _Ready()
	{
		_mapGenerator = GetParent() as LocalMapGenerator ?? GetNodeOrNull<LocalMapGenerator>("../LocalMap");
		_resourceSpawner = GetParent()?.GetNodeOrNull<LocalResourceSpawner>("ResourceSpawner")
			?? GetTree().Root.FindChild("ResourceSpawner", true, false) as LocalResourceSpawner;
		_selector = GetParent()?.GetNodeOrNull<LocalTileSelector>("TileSelector")
			?? GetTree().Root.FindChild("TileSelector", true, false) as LocalTileSelector;

		_activeCell = GameManager.Instance != null ? GameManager.Instance.ActiveCell : null;
		if (_activeCell == null)
		{
			_activeCell = new BasalKey();
			_activeCell.Initialize(25, 1337, Vector3.Right);
		}

		// Malla visual para zonas pintadas (Cultivos y Órdenes de Extracción)
		var zoneMat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
			VertexColorUseAsAlbedo = true,
			Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
			CullMode = BaseMaterial3D.CullModeEnum.Disabled
		};

		_zonesMeshInstance = new MeshInstance3D
		{
			Name = "PaintedZonesOverlay",
			MaterialOverride = zoneMat,
			CastShadow = GeometryInstance3D.ShadowCastingSetting.Off
		};
		AddChild(_zonesMeshInstance);

		if (_selector != null)
		{
			_selector.OnTriangleClicked += HandleTriangleClicked;
			_selector.OnTrianglePaintDragged += HandleTriangleDragged;
		}

		// Si el sector aún no tiene asentamiento, iniciar directamente en modo Fundar Asentamiento
		CurrentTool = _activeCell.HasSettlement ? TacticalToolMode.Inspeccionar : TacticalToolMode.FundarAsentamiento;

		// Temporizador de producción y recolección de colonos (cada 2.5 segundos)
		_productionTimer = new Timer
		{
			WaitTime = 2.5f,
			Autostart = true,
			OneShot = false
		};
		_productionTimer.Timeout += ProcessColonyWorkTick;
		AddChild(_productionTimer);
	}

	public void SetToolMode(TacticalToolMode mode)
	{
		if (mode == TacticalToolMode.FundarAsentamiento && _activeCell.HasSettlement)
		{
			OnStatusMessage?.Invoke("Este sector ya cuenta con un asentamiento fundado.");
			return;
		}

		CurrentTool = mode;
		OnInventoryOrStateChanged?.Invoke();
	}

	private void HandleTriangleClicked(LocalTriangleData tri)
	{
		if (tri == null) return;

		if (CurrentTool == TacticalToolMode.FundarAsentamiento)
		{
			TryFoundSettlementOnTriangle(tri);
		}
	}

	private void HandleTriangleDragged(LocalTriangleData tri)
	{
		if (tri == null) return;

		if (CurrentTool == TacticalToolMode.OrdenarExtraccion)
		{
			// Solo se pueden marcar triángulos que tengan árboles, piedra, arcilla o vetas minerales
			if (tri.ResourceType != LocalResourceType.Ninguno && tri.ResourceAmount > 0)
			{
				if (_harvestOrderedTriangles.Add(tri.Index))
				{
					RebuildZonesOverlayMesh();
					OnStatusMessage?.Invoke($"Orden de extracción marcada: {tri.ResourceType} (Total órdenes: {_harvestOrderedTriangles.Count})");
				}
			}
		}
		else if (CurrentTool == TacticalToolMode.PintarCultivo)
		{
			// Solo se puede cultivar en tierra firme libre y fértil (Pradera o Selva) con baja pendiente
			if (tri.IsBuildable &&
				(tri.TerrainType == LocalTerrainType.Pradera || tri.TerrainType == LocalTerrainType.Selva))
			{
				tri.ZoneState = LocalZoneState.Cultivo;
				tri.ResourceHint = "Parcela de Cultivo (Produce Comida)";
				_farmZoneTriangles.Add(tri.Index);
				RebuildZonesOverlayMesh();
				OnStatusMessage?.Invoke($"Parcela agrícola pintada ({_farmZoneTriangles.Count} celdas cultivadas).");
			}
		}
		else if (CurrentTool == TacticalToolMode.LimpiarZona)
		{
			bool changed = false;
			if (_harvestOrderedTriangles.Remove(tri.Index)) changed = true;

			if (_farmZoneTriangles.Remove(tri.Index))
			{
				tri.ZoneState = LocalZoneState.Libre;
				tri.ResourceHint = "Terreno despejado (Edificable)";
				changed = true;
			}

			if (changed)
			{
				RebuildZonesOverlayMesh();
				OnStatusMessage?.Invoke("Orden / zona eliminada de la celda.");
			}
		}
	}

	private void TryFoundSettlementOnTriangle(LocalTriangleData tri)
	{
		if (_activeCell.HasSettlement) return;

		if (!tri.IsBuildable)
		{
			OnStatusMessage?.Invoke("Terreno no apto para fundar la colonia. Elige una pradera o zona plana.");
			return;
		}

		_activeCell.FoundSettlement($"Colonia #{_activeCell.Id}");
		tri.ZoneState = LocalZoneState.Edificado;
		tri.ResourceHint = $"Ayuntamiento de {_activeCell.SettlementName}";

		SpawnTownHallVisual(tri);
		CurrentTool = TacticalToolMode.Inspeccionar;

		OnInventoryOrStateChanged?.Invoke();
		OnStatusMessage?.Invoke($"¡{_activeCell.SettlementName} fundada! Usa las herramientas inferiores para talar, minar o cultivar.");
	}

	private void SpawnTownHallVisual(LocalTriangleData tri)
	{
		if (_townHallInstance != null) _townHallInstance.QueueFree();

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Color stoneBase = new Color(0.55f, 0.54f, 0.52f);
		Color woodWalls = new Color(0.76f, 0.66f, 0.48f);
		Color roofRed = new Color(0.68f, 0.22f, 0.18f);

		// Cimiento de piedra
		AddBox(st, Vector3.Zero, new Vector3(11.0f, 2.5f, 11.0f), stoneBase);
		// Cuerpo principal del Ayuntamiento
		AddBox(st, new Vector3(0, 2.5f, 0), new Vector3(9.2f, 5.5f, 9.2f), woodWalls);
		// Tejado piramidal medieval
		AddPyramidRoof(st, new Vector3(0, 8.0f, 0), 10.6f, 4.8f, roofRed);
		// Torre vigía lateral
		AddBox(st, new Vector3(3.5f, 2.5f, -3.5f), new Vector3(3.4f, 9.5f, 3.4f), stoneBase.Lightened(0.08f));
		AddPyramidRoof(st, new Vector3(3.5f, 12.0f, -3.5f), 4.2f, 3.2f, roofRed.Darkened(0.1f));

		var mat = new StandardMaterial3D
		{
			ShadingMode = BaseMaterial3D.ShadingModeEnum.PerVertex,
			VertexColorUseAsAlbedo = true,
			Roughness = 0.8f
		};

		_townHallInstance = new MeshInstance3D
		{
			Name = "SettlementTownHall",
			Mesh = st.Commit(),
			MaterialOverride = mat,
			Position = tri.Center - Vector3.Up * 0.2f
		};
		AddChild(_townHallInstance);
	}

	private void ProcessColonyWorkTick()
	{
		if (_activeCell == null || !_activeCell.HasSettlement || _mapGenerator == null)
			return;

		bool anyChange = false;

		// 1. Cosecha de parcelas de cultivo (1 unidad de Comida por cada 3 parcelas cultivadas por ciclo)
		if (_farmZoneTriangles.Count > 0)
		{
			int foodYield = Mathf.Max(1, _farmZoneTriangles.Count / 3);
			_activeCell.LocalInventory["Comida"] += foodYield;
			anyChange = true;
		}

		// 2. Extracción de recursos marcados (hasta 4 tareas simultáneas según población)
		if (_harvestOrderedTriangles.Count > 0 && _resourceSpawner != null)
		{
			int maxWorkers = Mathf.Clamp(_activeCell.TotalPopulation / 3, 2, 8);
			var completed = new List<int>();
			int activeWorkers = 0;

			foreach (int triIdx in _harvestOrderedTriangles)
			{
				if (activeWorkers >= maxWorkers) break;
				if (triIdx < 0 || triIdx >= _mapGenerator.Triangles.Count) continue;

				var tri = _mapGenerator.Triangles[triIdx];
				if (tri.ResourceType == LocalResourceType.Ninguno || tri.ResourceAmount <= 0)
				{
					completed.Add(triIdx);
					continue;
				}

				string invKey = tri.ResourceType.ToString(); // "Madera", "Piedra", "Carbon", "Cobre", "Hierro", "Arcilla"
				int gathered = _resourceSpawner.ExtractResourceFromTriangle(tri, 12);

				if (gathered > 0 && _activeCell.LocalInventory.ContainsKey(invKey))
				{
					_activeCell.LocalInventory[invKey] += gathered;
					anyChange = true;
				}

				if (tri.ResourceAmount <= 0)
				{
					completed.Add(triIdx);
				}

				activeWorkers++;
			}

			if (completed.Count > 0)
			{
				foreach (int id in completed) _harvestOrderedTriangles.Remove(id);
				RebuildZonesOverlayMesh();
			}
		}

		if (anyChange)
		{
			OnInventoryOrStateChanged?.Invoke();
		}
	}

	private void RebuildZonesOverlayMesh()
	{
		if (_mapGenerator == null) return;

		var st = new SurfaceTool();
		st.Begin(Mesh.PrimitiveType.Triangles);

		Color farmColor = new Color(0.46f, 0.30f, 0.14f, 0.72f);     // Tierra labrada marrón
		Color harvestColor = new Color(0.95f, 0.68f, 0.15f, 0.55f);  // Marca dorada de tala/minería

		foreach (int idx in _farmZoneTriangles)
		{
			if (idx < 0 || idx >= _mapGenerator.Triangles.Count) continue;
			var t = _mapGenerator.Triangles[idx];
			Vector3 lift = t.Normal * 0.22f + Vector3.Up * 0.12f;
			st.SetColor(farmColor); st.AddVertex(t.V1 + lift);
			st.SetColor(farmColor); st.AddVertex(t.V2 + lift);
			st.SetColor(farmColor); st.AddVertex(t.V3 + lift);
		}

		foreach (int idx in _harvestOrderedTriangles)
		{
			if (idx < 0 || idx >= _mapGenerator.Triangles.Count) continue;
			var t = _mapGenerator.Triangles[idx];
			Vector3 lift = t.Normal * 0.26f + Vector3.Up * 0.15f;
			st.SetColor(harvestColor); st.AddVertex(t.V1 + lift);
			st.SetColor(harvestColor); st.AddVertex(t.V2 + lift);
			st.SetColor(harvestColor); st.AddVertex(t.V3 + lift);
		}

		_zonesMeshInstance.Mesh = st.Commit();
	}

	// --- UTILIDADES GEOMÉTRICAS LOW-POLY ---
	private void AddBox(SurfaceTool st, Vector3 baseCenter, Vector3 size, Color color)
	{
		float hx = size.X * 0.5f;
		float hz = size.Z * 0.5f;
		float y0 = baseCenter.Y;
		float y1 = baseCenter.Y + size.Y;
		Vector3 c = new Vector3(baseCenter.X, 0, baseCenter.Z);

		Vector3 p000 = c + new Vector3(-hx, y0, -hz);
		Vector3 p100 = c + new Vector3(hx, y0, -hz);
		Vector3 p101 = c + new Vector3(hx, y0, hz);
		Vector3 p001 = c + new Vector3(-hx, y0, hz);

		Vector3 p010 = c + new Vector3(-hx, y1, -hz);
		Vector3 p110 = c + new Vector3(hx, y1, -hz);
		Vector3 p111 = c + new Vector3(hx, y1, hz);
		Vector3 p011 = c + new Vector3(-hx, y1, hz);

		AddQuad(st, p001, p101, p111, p011, color);
		AddQuad(st, p100, p000, p010, p110, color.Darkened(0.12f));
		AddQuad(st, p000, p001, p011, p010, color.Darkened(0.06f));
		AddQuad(st, p101, p100, p110, p111, color.Darkened(0.06f));
		AddQuad(st, p011, p111, p110, p010, color.Lightened(0.08f));
	}

	private void AddPyramidRoof(SurfaceTool st, Vector3 baseCenter, float width, float height, Color color)
	{
		float h = width * 0.5f;
		Vector3 b0 = baseCenter + new Vector3(-h, 0, -h);
		Vector3 b1 = baseCenter + new Vector3(h, 0, -h);
		Vector3 b2 = baseCenter + new Vector3(h, 0, h);
		Vector3 b3 = baseCenter + new Vector3(-h, 0, h);
		Vector3 tip = baseCenter + new Vector3(0, height, 0);

		AddTri(st, b3, b2, tip, color);
		AddTri(st, b2, b1, tip, color.Darkened(0.08f));
		AddTri(st, b1, b0, tip, color.Darkened(0.15f));
		AddTri(st, b0, b3, tip, color.Darkened(0.08f));
	}

	private void AddQuad(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
	{
		AddTri(st, a, b, c, col);
		AddTri(st, a, c, d, col);
	}

	private void AddTri(SurfaceTool st, Vector3 a, Vector3 b, Vector3 c, Color col)
	{
		Vector3 n = (b - a).Cross(c - a).Normalized();
		st.SetNormal(n); st.SetColor(col); st.AddVertex(a);
		st.SetNormal(n); st.SetColor(col); st.AddVertex(b);
		st.SetNormal(n); st.SetColor(col); st.AddVertex(c);
	}
}
