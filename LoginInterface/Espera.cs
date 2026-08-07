using Godot;
using System;

public partial class Espera : Control
{
	private Button botonVolverSesion;

	public override void _Ready()
	{
		// 1. Obtener la referencia según la jerarquía de nodos en la imagen
		botonVolverSesion = GetNode<Button>("CenterContainer/VBoxContainer/Button");

		// 2. Conectar el evento Pressed
		if (botonVolverSesion != null)
		{
			botonVolverSesion.Pressed += OnBotonVolverSesionPressed;
		}
	}

	// --- MÉTODO DE NAVEGACIÓN ---

	// Cambia de vuelta a la escena de sesión activa
	private void OnBotonVolverSesionPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/sesion.tscn");
	}
}
