using Godot;
using System;

public partial class Registro : Control
{
	private Button botonIniciarSesion;
	private Button botonVolverMenu;

	public override void _Ready()
	{
		// 1. Obtener nodos omitiendo los LineEdit según la jerarquía de la escena
		botonIniciarSesion = GetNode<Button>("CenterContainer/VBoxContainer/Button");
		botonVolverMenu    = GetNode<Button>("CenterContainer/VBoxContainer/Button2");

		// 2. Conectar los eventos Pressed
		if (botonIniciarSesion != null)
		{
			botonIniciarSesion.Pressed += OnBotonIniciarSesionPressed;
		}

		if (botonVolverMenu != null)
		{
			botonVolverMenu.Pressed += OnBotonVolverMenuPressed;
		}
	}

	// --- MÉTODOS DE NAVEGACIÓN ---

	// Botón 1: "INICIAR SESIÓN" -> Cambia a la escena de Login / Sesión
	private void OnBotonIniciarSesionPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/login.tscn");
	}

	// Botón 2: "VOLVER AL MENÚ" -> Regresa al Menú Principal
	private void OnBotonVolverMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/menu.tscn");
	}
}
