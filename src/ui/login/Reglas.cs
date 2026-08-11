using Godot;
using System;

public partial class Reglas : Control
{
	private Button botonVolverMenu;

	public override void _Ready()
	{
		botonVolverMenu = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");

		if (botonVolverMenu != null)
		{
			botonVolverMenu.Pressed += OnBotonVolverMenuPressed;
		}
	}

	private void OnBotonVolverMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/menu.tscn");
	}
}
