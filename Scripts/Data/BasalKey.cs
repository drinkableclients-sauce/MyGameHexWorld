using Godot;
using System.Collections.Generic;

public class BasalKey
{
	// 1. Identidad y Posición Inmutable
	public int Id { get; set; }
	public Vector3 Normal { get; set; }
	public bool IsPentagon { get; set; }
	
	// 2. Topografía y ADN Geomorfológico
	public float BaseElevation { get; set; }
	public float WaterRatio { get; set; }
	public ulong RoughnessSeed { get; set; }
	
	// 3. Capacidad Económica y Táctica
	public float ArableArea { get; set; }      // Porcentaje de suelo fértil
	public float BuildableArea { get; set; }  // Porcentaje de superficie firme y plana para construir
	public int ThreatLevel { get; set; }
	public string Biome { get; set; }

	// 4. Geometría y Fronteras (Aristas físicas del tile)
	public List<Vector3[]> TileEdges { get; set; } = new List<Vector3[]>();

	/// <summary>
	/// Ejecuta el "Génesis" o nacimiento determinista de la celda utilizando la semilla global
	/// y los parámetros configurados en el menú (WorldSettings).
	/// </summary>
	public void GenerateGenesis(long globalSeed, int cellId, Vector3 normal, bool isPentagon, float seaLevelThreshold, float roughnessMultiplier)
	{
		Id = cellId;
		Normal = normal.Normalized();
		IsPentagon = isPentagon;

		// Creamos un RNG determinista exclusivo para esta celda combinando la semilla global y su ID
		ulong cellSeed = (ulong)globalSeed + ((ulong)cellId * 73856093UL);
		var rng = new RandomNumberGenerator();
		rng.Seed = cellSeed;

		// Semilla de relieve única para el diorama local
		RoughnessSeed = rng.Randi();

		// 1. Generación de Elevación Base (con una leve modulación espacial basada en su normal)
		float spatialNoise = (Mathf.Sin(Normal.X * 4.0f) + Mathf.Cos(Normal.Y * 4.0f) + 1.0f) * 0.5f;
		BaseElevation = (rng.RandfRange(0.15f, 0.85f) * 0.4f + spatialNoise * 0.6f);

		// 2. Determinación de Agua (WaterRatio) enlazada estrictamente al umbral del menú
		if (BaseElevation <= seaLevelThreshold)
		{
			WaterRatio = rng.RandfRange(0.5f, 1.0f); // Océano o costa profunda
			Biome = WaterRatio > 0.8f ? "Océano" : "Costa";
		}
		else
		{
			WaterRatio = rng.RandfRange(0.0f, 0.2f); // Tierra firme o lago menor interior
			Biome = WaterRatio > 0.08f ? "Lago Interior" : "Pradera Fértil";
		}

		// 3. Cálculo de Áreas de Zonificación (Cultivable y Edificable)
		// Las zonas muy empinadas o con mucha agua reducen el área edificable
		float slopeFactor = Mathf.Abs(BaseElevation - 0.5f);
		BuildableArea = Mathf.Clamp(1.0f - (slopeFactor * 1.8f) - (WaterRatio * 0.4f), 0.05f, 0.95f);
		ArableArea = Mathf.Clamp(rng.RandfRange(0.25f, 0.9f) * (1.0f - slopeFactor * 0.5f), 0.1f, 0.9f);

		// 4. Nivel de amenaza procedural inicial
		ThreatLevel = rng.RandiRange(0, 5);
	}
}
