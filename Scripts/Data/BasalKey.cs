using Godot;
using Godot.Collections;

public partial class BasalKey : RefCounted
{
	// --- DATOS BASE DEL TILE ---
	public int Id { get; set; }
	public int RoughnessSeed { get; set; }
	public Vector3 SphericalPosition { get; set; }
	public float BaseElevation { get; set; }
	public float WaterRatio { get; set; }
	public float BuildableArea { get; set; }
	public int ThreatLevel { get; set; }
	public string Biome { get; set; } = "Desconocido";
	public Array<int> NeighborIds { get; set; } = new Array<int>();

	public Vector3 OriginCoordinates { get; set; }
	public Vector3[] CornerDirections { get; set; }
	public float SeaLevelOffset { get; set; } = 0.15f;
	public float ElevationHeight { get; set; } = 1.0f;

	// Propiedad que el LocalMapGenerator estaba buscando
	public Array<Vector3> TileVertices { get; set; } = new Array<Vector3>();

	// --- FASE 1: DEMOGRAFÍA, ASENTAMIENTO E INVENTARIO LOCAL ---
	public bool HasSettlement { get; set; } = false;
	public string SettlementName { get; set; } = "Tierras Vírgenes";
	
	public int TotalPopulation { get; set; } = 0;
	public int SurplusPopulation { get; set; } = 0;
	public float Happiness { get; set; } = 80.0f;

	public Dictionary<string, int> LocalInventory { get; private set; } = new Dictionary<string, int>()
	{
		{ "Comida", 0 },
		{ "Madera", 0 },
		{ "Minerales", 0 },
		{ "Hierro", 0 },
		{ "Cobre", 0 }
	};

	public int MaxStorageCapacity { get; set; } = 500;

	// --- FASE 2: EDIFICIOS Y TECNOLOGÍAS LOCALES ---
	public Array<string> ConstructedBuildings { get; private set; } = new Array<string>();
	public Array<string> UnlockedTechnologies { get; private set; } = new Array<string>();

	public void Initialize(int id, int genesisSeed, Vector3 originCoords)
	{
		Id = id;
		OriginCoordinates = originCoords;

		LocalInventory = new Dictionary<string, int>
		{
			{ "Comida", 0 },
			{ "Madera", 0 },
			{ "Piedra", 0 },
			{ "Carbon", 0 },
			{ "Cobre", 0 },
			{ "Hierro", 0 },
			{ "Bronce", 0 },
			{ "Acero", 0 }
		};
	}

	public void FoundSettlement(string name)
	{
		HasSettlement = true;
		SettlementName = string.IsNullOrWhiteSpace(name) ? $"Colonia #{Id}" : name;
		TotalPopulation = 12;

		// Suministros iniciales de expedición para levantar los primeros edificios
		LocalInventory["Comida"] = 120;
		LocalInventory["Madera"] = 100;
		LocalInventory["Piedra"] = 60;
		LocalInventory["Carbon"] = 25;
		LocalInventory["Cobre"] = 20;
		LocalInventory["Hierro"] = 15;
		LocalInventory["Bronce"] = 0;
		LocalInventory["Acero"] = 0;
	}

	public bool AddResource(string resourceName, int amount)
	{
		if (!LocalInventory.ContainsKey(resourceName)) return false;

		int currentTotal = 0;
		foreach (var kvp in LocalInventory) currentTotal += kvp.Value;

		if (currentTotal + amount > MaxStorageCapacity) return false;

		LocalInventory[resourceName] += amount;
		return true;
	}

	public bool ConsumeResource(string resourceName, int amount)
	{
		if (!LocalInventory.ContainsKey(resourceName)) return false;
		if (LocalInventory[resourceName] < amount) return false;

		LocalInventory[resourceName] -= amount;
		return true;
	}
}
