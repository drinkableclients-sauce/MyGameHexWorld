using Godot;

public partial class SeedGenerator : Control
{
	[Export] private LineEdit _worldNameInput;
	[Export] private LineEdit _seedInput;
	[Export] private OptionButton _landmassOption;
	[Export] private OptionButton _difficultyOption;
	[Export] private Button _startWorldButton;
	[Export] private Button _backButton;

	public override void _Ready()
	{
		// Enlace por [Export] o respaldo automático por FindChild (soporta tanto LandmassOption como LandmssOption)
		if (_worldNameInput == null) _worldNameInput = FindChild("WorldNameInput", true, false) as LineEdit;
		if (_seedInput == null) _seedInput = FindChild("SeedInput", true, false) as LineEdit;
		if (_landmassOption == null) _landmassOption = (FindChild("LandmassOption", true, false) ?? FindChild("LandmssOption", true, false)) as OptionButton;
		if (_difficultyOption == null) _difficultyOption = FindChild("DifficultyOption", true, false) as OptionButton;
		if (_startWorldButton == null) _startWorldButton = FindChild("StartWorldBtn", true, false) as Button;
		if (_backButton == null) _backButton = FindChild("BackBtn", true, false) as Button;

		if (_startWorldButton != null)
			_startWorldButton.Pressed += OnStartWorldPressed;
		
		if (_backButton != null)
			_backButton.Pressed += () => GetTree().ChangeSceneToFile("res://Scenes/UI/MainMenu.tscn");
	}

	private void OnStartWorldPressed()
	{
		if (GameManager.Instance == null) return;

		// 1. Leer el nombre del mundo
		string worldName = (_worldNameInput != null && !string.IsNullOrWhiteSpace(_worldNameInput.Text)) 
			? _worldNameInput.Text.Trim() 
			: "Mundo Sin Nombre";

		// 2. Leer semilla
		string rawSeedText = _seedInput != null ? _seedInput.Text.Trim() : "";
		int finalSeed = GenesisSeedEngine.ParseCustomSeed(rawSeedText);

		// 3. Leer tipo de terreno por el índice seleccionado
		WorldLandmassType landmass = WorldLandmassType.Continental;
		int landmassIndex = -1;
		if (_landmassOption != null)
		{
			landmassIndex = _landmassOption.Selected;
			switch (landmassIndex)
			{
				case 0: landmass = WorldLandmassType.Pangea; break;
				case 1: landmass = WorldLandmassType.Archipiélago; break;
				case 2: landmass = WorldLandmassType.Continental; break;
				default: landmass = WorldLandmassType.Continental; break;
			}
		}
		else
		{
			GD.PrintErr("[SeedGenerator] ADVERTENCIA: No se encontró el OptionButton de Terreno.");
		}

		// 4. Leer dificultad por el índice seleccionado
		WorldDifficulty difficulty = WorldDifficulty.Normal;
		int diffIndex = -1;
		if (_difficultyOption != null)
		{
			diffIndex = _difficultyOption.Selected;
			switch (diffIndex)
			{
				case 0: difficulty = WorldDifficulty.Fácil; break;
				case 1: difficulty = WorldDifficulty.Normal; break;
				case 2: difficulty = WorldDifficulty.Difícil; break;
				case 3: difficulty = WorldDifficulty.Hardcore; break;
				default: difficulty = WorldDifficulty.Normal; break;
			}
		}
		else
		{
			GD.PrintErr("[SeedGenerator] ADVERTENCIA: No se encontró el OptionButton de Dificultad.");
		}

		// 5. Inyectar en el GameManager global
		GameManager.Instance.GenesisSeed = finalSeed;
		GameManager.Instance.CurrentWorldConfig.WorldName = worldName;
		GameManager.Instance.CurrentWorldConfig.LandmassType = landmass;
		GameManager.Instance.CurrentWorldConfig.Difficulty = difficulty;
		GameManager.Instance.CurrentWorldConfig.ApplyDifficultyModifiers();

		GD.Print($"[SeedGenerator] ¡Mundo configurado con éxito!\n" +
				 $" - Nombre: {worldName}\n" +
				 $" - Semilla: {finalSeed}\n" +
				 $" - Terreno: {landmass} (Índice UI: {landmassIndex})\n" +
				 $" - Dificultad: {difficulty} (Índice UI: {diffIndex})");

		// 6. Transicionar a la escena del Planeta Global
		GetTree().ChangeSceneToFile("res://Scenes/Global/Planet.tscn");
		
		// En lugar de asignar las propiedades una por una sueltas, llamas a la función centralizada:
GameManager.Instance.SetupNewWorld(finalSeed, worldName, landmass, difficulty);
	}
}
