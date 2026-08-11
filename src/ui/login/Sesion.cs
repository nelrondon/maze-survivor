using Godot;
using System;

public partial class Sesion : Control
{
	private RichTextLabel labelUsername;
	private RichTextLabel labelDetalles;

	private Button botonJugar;
	private Button botonCerrarSesion;

	public override void _Ready()
	{
		labelUsername = GetNodeOrNull<RichTextLabel>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/RichTextLabel")
		             ?? GetNodeOrNull<RichTextLabel>("CenterContainer2/VBoxContainer/RichTextLabel");

		labelDetalles = GetNodeOrNull<RichTextLabel>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/RichTextLabel2")
		             ?? GetNodeOrNull<RichTextLabel>("CenterContainer2/VBoxContainer/RichTextLabel2");

		botonJugar    = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button")
		             ?? GetNodeOrNull<Button>("CenterContainer3/VBoxContainer/Button");

		botonCerrarSesion = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button2")
		                 ?? GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button3")
		                 ?? GetNodeOrNull<Button>("CenterContainer3/VBoxContainer/Button3");

		if (botonJugar != null)
		{
			botonJugar.Pressed += OnBotonJugarPressed;
		}

		if (botonCerrarSesion != null)
		{
			botonCerrarSesion.Pressed += OnBotonCerrarSesionPressed;
		}

		CargarPerfilUI();
	}

	private void CargarPerfilUI()
	{
		var mgr = SupabaseManager.Instance;
		if (mgr == null) return;

		string username = mgr.CurrentPerfil?.Username ?? "SOBREVIVIENTE";
		string nombre   = mgr.CurrentJugador?.Nombre ?? "Jugador";
		decimal saldo   = mgr.CurrentJugador?.Saldo ?? 0;
		int oro         = mgr.CurrentPerfil?.Oro ?? 0;
		int xp          = mgr.CurrentPerfil?.Experiencia ?? 0;

		if (labelUsername != null)
		{
			labelUsername.Text = $"[center][b][font_size=24]{username.ToUpper()}[/font_size][/b][/center]";
		}

		if (labelDetalles != null)
		{
			labelDetalles.Text = $"[center]{nombre} | Oro: {oro} | XP: {xp} | Saldo: ${saldo:F2}[/center]";
		}
	}

	private void OnBotonJugarPressed()
	{
		GetTree().ChangeSceneToFile("res://src/multiplayer/Lobby.tscn");
	}

	private async void OnBotonCerrarSesionPressed()
	{
		if (SupabaseManager.Instance != null)
		{
			await SupabaseManager.Instance.SignOutAsync();
		}
		GetTree().ChangeSceneToFile("res://src/ui/login/menu.tscn");
	}
}
