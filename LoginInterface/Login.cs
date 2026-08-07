using Godot;
using System;

public partial class Login : Control
{
	private Button botonIniciarSesion;
	private Button botonVolverMenu;

	public override void _Ready()
	{
		// 1. Obtener las referencias de los botones según la jerarquía del árbol
		botonIniciarSesion = GetNode<Button>("CenterContainer/VBoxContainer/Button");
		botonVolverMenu    = GetNode<Button>("CenterContainer/VBoxContainer/Button2");

		// 2. Registrar los eventos 'Pressed'
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

	// Botón 1: "INICIAR SESIÓN" -> Cambia a la escena de sesión activa o lobby
	private void OnBotonIniciarSesionPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/sesion.tscn");
	}

	// Botón 2: "VOLVER AL MENÚ" -> Regresa al Menú Principal
	private void OnBotonVolverMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/menu.tscn");
	}
}
