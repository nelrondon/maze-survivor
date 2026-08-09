using Godot;
using System;

public partial class EndGameUI : CanvasLayer
{
	private Label _titleLabel;
	private Label _descriptionLabel;
	private Button _lobbyButton;

	public override void _Ready()
	{
		Layer = 100; // Render above all other UI elements
	}

	public static EndGameUI ShowResult(Node contextNode, bool isVictory, string titleText, string descriptionText)
	{
		if (contextNode == null) return null;

		var root = contextNode.GetTree().Root;
		var existingUI = root.GetNodeOrNull<EndGameUI>("EndGameUI");
		if (existingUI != null && IsInstanceValid(existingUI))
		{
			return existingUI;
		}

		var endGameUI = new EndGameUI();
		endGameUI.Name = "EndGameUI";
		root.AddChild(endGameUI);
		endGameUI.BuildUI(isVictory, titleText, descriptionText);

		// Unlock mouse cursor
		Input.MouseMode = Input.MouseModeEnum.Visible;

		return endGameUI;
	}

	private void BuildUI(bool isVictory, string titleText, string descriptionText)
	{
		// Fullscreen background overlay
		var bgRect = new ColorRect();
		bgRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bgRect.Color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
		AddChild(bgRect);

		// Center container
		var centerContainer = new CenterContainer();
		centerContainer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(centerContainer);

		// Panel container
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(500, 300);
		centerContainer.AddChild(panel);

		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_top", 30);
		margin.AddThemeConstantOverride("margin_bottom", 30);
		margin.AddThemeConstantOverride("margin_left", 40);
		margin.AddThemeConstantOverride("margin_right", 40);
		panel.AddChild(margin);

		var vbox = new VBoxContainer();
		vbox.AddThemeConstantOverride("separation", 20);
		margin.AddChild(vbox);

		// Title Label (Green for Victory, Red for Defeat)
		_titleLabel = new Label();
		_titleLabel.Text = titleText;
		_titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_titleLabel.AddThemeFontSizeOverride("font_size", 42);

		Color titleColor = isVictory ? new Color(0.1f, 0.95f, 0.3f) : new Color(0.95f, 0.2f, 0.2f);
		_titleLabel.AddThemeColorOverride("font_color", titleColor);
		vbox.AddChild(_titleLabel);

		// Description Label
		_descriptionLabel = new Label();
		_descriptionLabel.Text = descriptionText;
		_descriptionLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_descriptionLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		_descriptionLabel.AddThemeFontSizeOverride("font_size", 20);
		_descriptionLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 0.9f));
		vbox.AddChild(_descriptionLabel);

		// Spacer
		var spacer = new Control();
		spacer.CustomMinimumSize = new Vector2(0, 10);
		vbox.AddChild(spacer);

		// Button to return to lobby
		_lobbyButton = new Button();
		_lobbyButton.Text = "Volver al Lobby";
		_lobbyButton.CustomMinimumSize = new Vector2(220, 50);
		_lobbyButton.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
		_lobbyButton.AddThemeFontSizeOverride("font_size", 20);
		_lobbyButton.Pressed += OnLobbyButtonPressed;
		vbox.AddChild(_lobbyButton);
	}

	private void OnLobbyButtonPressed()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;

		// Check if a LobbyHandler node exists in tree
		var lobbyHandler = GetTree().Root.GetNodeOrNull<LobbyHandler>("Lobby") 
			?? GetTree().Root.FindChild("Lobby", recursive: true, owned: false) as LobbyHandler;

		if (lobbyHandler != null && IsInstanceValid(lobbyHandler))
		{
			QueueFree();
			lobbyHandler.ReturnToLobby("Regresó al Lobby desde la partida");
		}
		else
		{
			QueueFree();
			GetTree().ChangeSceneToFile("res://src/multiplayer/Lobby.tscn");
		}
	}
}
