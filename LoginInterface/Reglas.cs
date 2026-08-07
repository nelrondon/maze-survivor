using Godot;
using System;

public partial class Reglas : Control
{
	private Button botonVolverMenu;

	public override void _Ready()
	{
		// Ruta al botón según tu árbol: Control -> CenterContainer -> VBoxContainer -> Button
		botonVolverMenu = GetNode<Button>("CenterContainer/VBoxContainer/Button");

		if (botonVolverMenu != null)
		{
			botonVolverMenu.Pressed += OnBotonVolverMenuPressed;
		}
	}

	private void OnBotonVolverMenuPressed()
	{
		// Cambia de vuelta a la escena del menú principal
		GetTree().ChangeSceneToFile("res://LoginInterface/menu.tscn");
	}
}
