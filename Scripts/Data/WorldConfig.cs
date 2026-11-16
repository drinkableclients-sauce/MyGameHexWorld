using Godot;

public enum WorldLandmassType
{
	Pangea,           // Un supercontinente central masivo
	Archipiélago,     // Muchas islas y mares fragmentados
	Continental       // Varios continentes equilibrados
}

public enum WorldDifficulty
{
	Fácil,      // Más llanuras fértiles, menos amenazas, agua abundante
	Normal,     // Balance estándar
	Difícil,    // Más montañas escarpadas, desiertos extensos, recursos escasos y alta amenaza
	Hardcore    // Tundra extrema, hostilidad máxima
}

[GlobalClass]
public partial class WorldConfig : Resource
{
	[Export] public string WorldName { get; set; } = "Mundo de los Pioneros";
	[Export] public WorldLandmassType LandmassType { get; set; } = WorldLandmassType.Continental;
	[Export] public WorldDifficulty Difficulty { get; set; } = WorldDifficulty.Normal;
	[Export] public float ResourceAbundanceMultiplier { get; set; } = 1.0f;
	[Export] public float MountainFrequency { get; set; } = 1.0f;

	/// <summary>
	/// Ajusta los parámetros globales de configuración según la dificultad seleccionada.
	/// </summary>
	public void ApplyDifficultyModifiers()
	{
		switch (Difficulty)
		{
			case WorldDifficulty.Fácil:
				MountainFrequency = 0.7f;      // Menos picos montañosos abruptos
				ResourceAbundanceMultiplier = 1.5f; // Más recursos y zonas cultivables
				break;
			case WorldDifficulty.Normal:
				MountainFrequency = 1.0f;
				ResourceAbundanceMultiplier = 1.0f;
				break;
			case WorldDifficulty.Difícil:
				MountainFrequency = 1.4f;      // Más relieve montañoso y accidentado
				ResourceAbundanceMultiplier = 0.7f; // Recursos más escasos
				break;
			case WorldDifficulty.Hardcore:
				MountainFrequency = 1.8f;
				ResourceAbundanceMultiplier = 0.4f;
				break;
		}
	}
}
