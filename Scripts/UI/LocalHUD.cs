using Godot;

public partial class LocalHUD : CanvasLayer
{
	private Label _sectorTitleLabel;
	private Label _inventoryBarLabel;
	private Label _statusBannerLabel;
	private Label _triangleInfoLabel;
	private Button _backToPlanetButton;

	private Button _btnFound;
	private Button _btnInspect;
	private Button _btnHarvest;
	private Button _btnFarm;
	private Button _btnClear;

	private LocalTileSelector _selector;
	private LocalSettlementController _settlementController;

	public override void _Ready()
	{
		BuildHudLayout();
		PopulateSectorHeader();

		_selector = GetTree().Root.FindChild("TileSelector", true, false) as LocalTileSelector;
		if (_selector != null)
		{
			_selector.OnTriangleHovered += HandleTriangleHovered;
		}

		_settlementController = GetTree().Root.FindChild("SettlementController", true, false) as LocalSettlementController;
		if (_settlementController != null)
		{
			_settlementController.OnInventoryOrStateChanged += RefreshInventoryAndToolsUI;
			_settlementController.OnStatusMessage += ShowStatusMessage;
		}

		RefreshInventoryAndToolsUI();
	}

	private void BuildHudLayout()
	{
		var rootControl = new Control
		{
			Name = "LocalHudRoot",
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		rootControl.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(rootControl);

		// --- 1. PANEL SUPERIOR IZQUIERDO: Sector, Inventario Medieval y Regreso ---
		var topPanel = new PanelContainer
		{
			Position = new Vector2(16, 16),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		rootControl.AddChild(topPanel);

		var topMargin = new MarginContainer();
		topMargin.AddThemeConstantOverride("margin_left", 14);
		topMargin.AddThemeConstantOverride("margin_right", 14);
		topMargin.AddThemeConstantOverride("margin_top", 8);
		topMargin.AddThemeConstantOverride("margin_bottom", 8);
		topPanel.AddChild(topMargin);

		var topVBox = new VBoxContainer();
		topVBox.AddThemeConstantOverride("separation", 6);
		topMargin.AddChild(topVBox);

		var headerRow = new HBoxContainer();
		headerRow.AddThemeConstantOverride("separation", 18);
		topVBox.AddChild(headerRow);

		_sectorTitleLabel = new Label
		{
			Text = "SECTOR LOCAL",
			VerticalAlignment = VerticalAlignment.Center
		};
		headerRow.AddChild(_sectorTitleLabel);

		_backToPlanetButton = new Button
		{
			Text = "← Volver al Planeta",
			CustomMinimumSize = new Vector2(155, 30)
		};
		_backToPlanetButton.Pressed += OnBackToPlanetPressed;
		headerRow.AddChild(_backToPlanetButton);

		_inventoryBarLabel = new Label
		{
			Text = "Sin asentamiento fundado en este sector.",
			Modulate = new Color(0.95f, 0.90f, 0.72f)
		};
		topVBox.AddChild(_inventoryBarLabel);

		_statusBannerLabel = new Label
		{
			Text = "Selecciona una celda edificable para fundar tu colonia.",
			Modulate = new Color(0.65f, 0.95f, 0.72f)
		};
		topVBox.AddChild(_statusBannerLabel);

		// --- 2. BARRA INFERIOR IZQUIERDA: Herramientas Tácticas ---
		var toolsPanel = new PanelContainer
		{
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		toolsPanel.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
		toolsPanel.OffsetLeft = 16;
		toolsPanel.OffsetTop = -68;
		toolsPanel.OffsetRight = 690;
		toolsPanel.OffsetBottom = -16;
		rootControl.AddChild(toolsPanel);

		var toolsMargin = new MarginContainer();
		toolsMargin.AddThemeConstantOverride("margin_left", 10);
		toolsMargin.AddThemeConstantOverride("margin_right", 10);
		toolsMargin.AddThemeConstantOverride("margin_top", 8);
		toolsMargin.AddThemeConstantOverride("margin_bottom", 8);
		toolsPanel.AddChild(toolsMargin);

		var toolsRow = new HBoxContainer();
		toolsRow.AddThemeConstantOverride("separation", 8);
		toolsMargin.AddChild(toolsRow);

		_btnFound = CreateToolButton("🏰 Fundar Colonia", TacticalToolMode.FundarAsentamiento, toolsRow);
		_btnInspect = CreateToolButton("🔍 Inspeccionar", TacticalToolMode.Inspeccionar, toolsRow);
		_btnHarvest = CreateToolButton("🪓 Talar / Minar", TacticalToolMode.OrdenarExtraccion, toolsRow);
		_btnFarm = CreateToolButton("🌾 Pintar Cultivo", TacticalToolMode.PintarCultivo, toolsRow);
		_btnClear = CreateToolButton("🧹 Limpiar", TacticalToolMode.LimpiarZona, toolsRow);

		// --- 3. PANEL INFERIOR DERECHO: Inspector de Celda ---
		var bottomPanel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(320, 155),
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		bottomPanel.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
		bottomPanel.OffsetLeft = -338;
		bottomPanel.OffsetTop = -172;
		bottomPanel.OffsetRight = -16;
		bottomPanel.OffsetBottom = -16;
		rootControl.AddChild(bottomPanel);

		var botMargin = new MarginContainer();
		botMargin.AddThemeConstantOverride("margin_left", 14);
		botMargin.AddThemeConstantOverride("margin_right", 14);
		botMargin.AddThemeConstantOverride("margin_top", 10);
		botMargin.AddThemeConstantOverride("margin_bottom", 10);
		bottomPanel.AddChild(botMargin);

		_triangleInfoLabel = new Label
		{
			Text = "Pasa el cursor sobre el terreno\npara inspeccionar cada celda triangular.",
			AutowrapMode = TextServer.AutowrapMode.WordSmart
		};
		botMargin.AddChild(_triangleInfoLabel);
	}

	private Button CreateToolButton(string text, TacticalToolMode mode, Control parent)
	{
		var btn = new Button
		{
			Text = text,
			CustomMinimumSize = new Vector2(118, 34),
			ToggleMode = true
		};
		btn.Pressed += () =>
		{
			if (_settlementController != null)
			{
				_settlementController.SetToolMode(mode);
			}
		};
		parent.AddChild(btn);
		return btn;
	}

	private void PopulateSectorHeader()
	{
		if (GameManager.Instance != null && GameManager.Instance.ActiveCell != null)
		{
			var cell = GameManager.Instance.ActiveCell;
			string shape = (cell.CornerDirections != null && cell.CornerDirections.Length == 5) || cell.Id < 12
				? "Pentágono"
				: "Hexágono";
			_sectorTitleLabel.Text = $"Sector #{cell.Id} ({shape})  |  Bioma: {cell.Biome}";
		}
	}

	private void RefreshInventoryAndToolsUI()
	{
		var cell = GameManager.Instance != null ? GameManager.Instance.ActiveCell : null;
		bool hasSettlement = cell != null && cell.HasSettlement;

		if (hasSettlement && cell.LocalInventory != null)
		{
			var inv = cell.LocalInventory;
			int GetVal(string k) => inv.TryGetValue(k, out int v) ? v : 0;

			_inventoryBarLabel.Text =
				$"👥 Pob: {cell.TotalPopulation}  |  🍞 {GetVal("Comida")}  |  🪵 {GetVal("Madera")}  |  🪨 {GetVal("Piedra")}  |  " +
				$"⚫ Carbón: {GetVal("Carbon")}  |  🟠 Cobre: {GetVal("Cobre")}  |  ⚪ Hierro: {GetVal("Hierro")}  |  " +
				$"🛡️ Bronce: {GetVal("Bronce")}  |  ⚔️ Acero: {GetVal("Acero")}";
		}
		else
		{
			_inventoryBarLabel.Text = "Colonia no fundada — Haz clic en una pradera libre para establecer tu Ayuntamiento.";
		}

		// Habilitar herramientas de gestión solo después de fundar el asentamiento
		_btnFound.Visible = !hasSettlement;
		_btnHarvest.Disabled = !hasSettlement;
		_btnFarm.Disabled = !hasSettlement;
		_btnClear.Disabled = !hasSettlement;

		if (_settlementController != null)
		{
			var mode = _settlementController.CurrentTool;
			_btnFound.ButtonPressed = mode == TacticalToolMode.FundarAsentamiento;
			_btnInspect.ButtonPressed = mode == TacticalToolMode.Inspeccionar;
			_btnHarvest.ButtonPressed = mode == TacticalToolMode.OrdenarExtraccion;
			_btnFarm.ButtonPressed = mode == TacticalToolMode.PintarCultivo;
			_btnClear.ButtonPressed = mode == TacticalToolMode.LimpiarZona;
		}
	}

	private void ShowStatusMessage(string msg)
	{
		_statusBannerLabel.Text = msg;
	}

	private void HandleTriangleHovered(LocalTriangleData tri)
	{
		if (tri == null)
		{
			_triangleInfoLabel.Text = "Fuera del área del diorama.\nMueve el cursor sobre el terreno.";
			return;
		}

		string buildableText;
		if (tri.IsBuildable)
			buildableText = "Sí (Apto para construcción)";
		else if (tri.ZoneState != LocalZoneState.Libre)
			buildableText = $"No (Ocupado: {tri.ZoneState})";
		else
			buildableText = "No (Agua o terreno escarpado)";

		int slopeDegrees = Mathf.RoundToInt(Mathf.RadToDeg(Mathf.Acos(Mathf.Clamp(tri.Normal.Y, 0f, 1f))));

		_triangleInfoLabel.Text =
			$"Celda Triangular #{tri.Index}\n" +
			$"• Suelo: {tri.TerrainType}  |  Estado: {tri.ZoneState}\n" +
			$"• Altitud: {tri.AverageHeight:F1} m  |  Inclinación: {slopeDegrees}°\n" +
			$"• Edificable: {buildableText}\n" +
			$"• Recurso: {tri.ResourceHint}";
	}

	private void OnBackToPlanetPressed()
	{
		GetTree().ChangeSceneToFile("res://Scenes/Global/Planet.tscn");
	}
}
