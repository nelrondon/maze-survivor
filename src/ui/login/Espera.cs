using Godot;
using System;

public partial class Espera : Control
{
	private Button botonVolverSesion;

	public override void _Ready()
	{
		botonVolverSesion = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");

		if (botonVolverSesion != null)
		{
			botonVolverSesion.Pressed += OnBotonVolverSesionPressed;
		}
	}

	private void OnBotonVolverSesionPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/sesion.tscn");
	}
}
