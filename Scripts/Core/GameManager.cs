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

		if (GenesisSeed == 847291)
		{
			GenesisSeed = GenesisSeedEngine.RollDiceSeed();
		}

		// Asegurar que la configuración por defecto tenga sus modificadores aplicados
		CurrentWorldConfig.ApplyDifficultyModifiers();
		
		GD.Print($"[GameManager] Inicializado. Semilla: {GenesisSeed} | Dificultad: {CurrentWorldConfig.Difficulty} | Continentes: {CurrentWorldConfig.LandmassType}");
	}
}
