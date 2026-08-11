using Godot;
using System;

public partial class Menu : Control
{
	private Button _botonJugar;
	private Button _botonSalir;

	public override void _Ready()
	{
		_botonJugar = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
			?? GetNodeOrNull<Button>("%Button") 
			?? (FindChild("Button", true, false) as Button);

		_botonSalir = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button2") 
			?? GetNodeOrNull<Button>("%Button2") 
			?? (FindChild("Button2", true, false) as Button);

		if (_botonJugar != null)
		{
			_botonJugar.Pressed += OnBotonJugarPressed;
			GD.Print("[Menu] Botón JUGAR conectado exitosamente.");
		}
		else
		{
			GD.PrintErr("[Menu] ERROR: No se encontró el botón JUGAR.");
		}

		if (_botonSalir != null)
		{
			_botonSalir.Pressed += OnBotonSalirPressed;
			GD.Print("[Menu] Botón SALIR conectado exitosamente.");
		}
		else
		{
			GD.PrintErr("[Menu] ERROR: No se encontró el botón SALIR.");
		}
	}

	private void OnBotonJugarPressed()
	{
		GD.Print("[Menu] Transicionando a la escena del Lobby...");
		GetTree().ChangeSceneToFile("res://src/multiplayer/Lobby.tscn");
	}

	private void OnBotonSalirPressed()
	{
		GD.Print("[Menu] Saliendo del juego...");
		GetTree().Quit();
	}
}
