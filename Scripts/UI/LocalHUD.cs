using Godot;

public partial class LocalHUD : Control
{
	private Label _infoLabel;
	private Label _inventoryLabel;
	private Button _foundSettlementButton;
	private Button _backToPlanetButton;

	public override void _Ready()
	{
		// Enlazar nodos de la UI por código o nombres estándar
		_infoLabel = GetNodeOrNull<Label>("PanelInfo/VBox/InfoLabel");
		_inventoryLabel = GetNodeOrNull<Label>("PanelInventory/VBox/InventoryLabel");
		_foundSettlementButton = GetNodeOrNull<Button>("ActionPanel/FoundSettlementBtn");
		_backToPlanetButton = GetNodeOrNull<Button>("TopBar/BackToPlanetBtn");

		if (_foundSettlementButton != null)
			_foundSettlementButton.Pressed += OnFoundSettlementPressed;

		if (_backToPlanetButton != null)
			_backToPlanetButton.Pressed += OnBackToPlanetPressed;

		UpdateHUDData();
	}

	public override void _Process(double delta)
	{
		// Refrescar datos en tiempo real por si cambian recursos o población
		UpdateHUDData();
	}

	private void UpdateHUDData()
	{
		if (GameManager.Instance == null || GameManager.Instance.ActiveCell == null) return;

		var activeTile = GameManager.Instance.ActiveCell;

		// 1. Actualizar panel de información del Tile
		string settlementStatus = activeTile.HasSettlement ? activeTile.SettlementName : "Territorio Virgen (Sin Asentar)";
		if (_infoLabel != null)
		{
			_infoLabel.Text = $"Tile #{activeTile.Id} | Bioma: {activeTile.Biome}\n" +
							  $"Estado: {settlementStatus}\n" +
							  $"Población: {activeTile.TotalPopulation} | Felicidad: {activeTile.Happiness}%\n" +
							  $"Amenaza: {activeTile.ThreatLevel}";
		}

		// 2. Actualizar inventario local estricto del ATK
		if (_inventoryLabel != null)
		{
			string invText = "--- Inventario Local (ATK) ---\n";
			int totalStored = 0;
			foreach (var kvp in activeTile.LocalInventory)
			{
				invText += $"{kvp.Key}: {kvp.Value}\n";
				totalStored += kvp.Value;
			}
			invText += $"Almacenamiento: {totalStored} / {activeTile.MaxStorageCapacity}";
			_inventoryLabel.Text = invText;
		}

		// 3. Controlar visibilidad del botón de fundación
		if (_foundSettlementButton != null)
		{
			_foundSettlementButton.Visible = !activeTile.HasSettlement;
		}
	}

	private void OnFoundSettlementPressed()
	{
		if (GameManager.Instance == null || GameManager.Instance.ActiveCell == null) return;

		var activeTile = GameManager.Instance.ActiveCell;
		if (!activeTile.HasSettlement)
		{
			// Fundar el asentamiento con un nombre predeterminado o genérico
			activeTile.FoundSettlement($"Bastión de los Pioneros (#{activeTile.Id})");
			GD.Print($"[LocalHUD] Asentamiento fundado en el Tile #{activeTile.Id}.");
		}
	}

	private void OnBackToPlanetPressed()
	{
		// Regresar a la escena del planeta global
		GetTree().ChangeSceneToFile("res://Scenes/Global/Planet.tscn");
	}
}
