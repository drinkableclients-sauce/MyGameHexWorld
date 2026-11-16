using Godot;

public partial class GameManager : Node
{
	public static GameManager Instance { get; private set; }

	[Export] 
	public int GenesisSeed { get; set; } = 847291;

	// Configuración macro del mundo elegida en el menú de inicio
	public WorldConfig CurrentWorldConfig { get; set; } = new WorldConfig();

	public BasalKey ActiveCell { get; set; } = null;

	public override void _Ready()
	{
		if (Instance != null && Instance != this)
		{
			QueueFree();
			return;
		}
		
		Instance = this;
		GD.Print("[GameManager] Singleton global cargado en memoria.");
	}

	/// <summary>
	/// Método único y centralizado para inicializar el mundo con los datos del SeedGenerator.
	/// </summary>
	public void SetupNewWorld(int seed, string worldName, WorldLandmassType landmass, WorldDifficulty difficulty)
	{
		GenesisSeed = seed;
		CurrentWorldConfig.WorldName = worldName;
		CurrentWorldConfig.LandmassType = landmass;
		CurrentWorldConfig.Difficulty = difficulty;
		
		// Aplicar los modificadores correspondientes a la dificultad elegida
		CurrentWorldConfig.ApplyDifficultyModifiers();
		
		GD.Print($"[GameManager] ¡Nuevo Mundo Creado!\n" +
				 $" - Nombre: {worldName}\n" +
				 $" - Semilla: {GenesisSeed}\n" +
				 $" - Dificultad: {difficulty}\n" +
				 $" - Continentes: {landmass}");
	}
}
