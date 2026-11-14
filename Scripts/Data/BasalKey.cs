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

	public void Initialize(int id, int genesisSeed, Vector3 sphericalPosition)
	{
		Id = id;
		SphericalPosition = sphericalPosition;
		
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)(genesisSeed + (id * 73856093) ^ (int)(sphericalPosition.X * 1000));
		
		var worldConfig = GameManager.Instance != null ? GameManager.Instance.CurrentWorldConfig : new WorldConfig();

		float landmassBias = 0.0f;
		if (worldConfig.LandmassType == WorldLandmassType.Pangea) landmassBias = 0.40f;
		else if (worldConfig.LandmassType == WorldLandmassType.Archipiélago) landmassBias = -0.35f;

		RoughnessSeed = (int)rng.RandiRange(1000, 999999);
		
		float rawElevation = rng.RandfRange(-0.6f, 1.0f);
		BaseElevation = (rawElevation + landmassBias) * worldConfig.MountainFrequency;
		BaseElevation = Mathf.Clamp(BaseElevation, -0.9f, 1.2f);

		WaterRatio = BaseElevation < 0.0f ? rng.RandfRange(0.5f, 0.95f) : rng.RandfRange(0.0f, 0.25f);
		if (worldConfig.LandmassType == WorldLandmassType.Archipiélago) WaterRatio *= 1.3f;

		BuildableArea = Mathf.Clamp(1.0f - Mathf.Abs(BaseElevation), 0.1f, 1.0f);
		ThreatLevel = (int)((rng.Randf() * 4.0f) * (worldConfig.ResourceAbundanceMultiplier < 1.0f ? 1.5f : 1.0f));

		float latitude = Mathf.Abs(sphericalPosition.Normalized().Y);
		if (BaseElevation < -0.3f) Biome = latitude > 0.75f ? "Océano Polar" : "Océano Profundo";
		else if (BaseElevation < 0.0f) Biome = latitude > 0.75f ? "Costa Helada" : "Costa / Ribera";
		else if (latitude > 0.8f) Biome = "Casquete Polar / Tundra";
		else if (latitude > 0.5f) Biome = BaseElevation > 0.5f ? "Montaña Alpina" : "Bosque Frío / Taiga";
		else
		{
			if (WaterRatio > 0.5f) Biome = "Lago Interior / Humedal";
			else if (BuildableArea > 0.68f) Biome = "Pradera Fértil";
			else Biome = "Desierto / Estepa";
		}
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

	public void FoundSettlement(string name)
	{
		HasSettlement = true;
		SettlementName = name;
		TotalPopulation = 10;
		SurplusPopulation = 0;
		
		LocalInventory["Comida"] = 100;
		LocalInventory["Madera"] = 150;
		LocalInventory["Minerales"] = 50;

		GD.Print($"[ATK #{Id}] ¡Asentamiento '{SettlementName}' fundado con éxito!");
	}
}
