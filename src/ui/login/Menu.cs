using Godot;
using System;

public partial class Menu : Control
{
	private Button _botonJugar;
	private Button _botonSalir;

	public override void _Ready()
	{
		_botonJugar = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");
		_botonSalir = GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button2");

		if (_botonJugar != null)
		{
			_botonJugar.Pressed += OnBotonJugarPressed;
		}

		if (_botonSalir != null)
		{
			_botonSalir.Pressed += OnBotonSalirPressed;
		}
	}

	private void OnBotonJugarPressed()
	{
		GetTree().ChangeSceneToFile("res://src/multiplayer/Lobby.tscn");
	}

	private void OnBotonSalirPressed()
	{
		GetTree().Quit();
	}
}
