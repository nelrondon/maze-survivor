using Godot;
using System;

public partial class Sesion : Control
{
	private Button botonEntrarJugador;
	private Button botonEntrarEspectador;
	private Button botonCerrarSesion;

	public override void _Ready()
	{
		// 1. Obtener las referencias según la jerarquía de la escena (CenterContainer3)
		botonEntrarJugador   = GetNode<Button>("CenterContainer3/VBoxContainer/Button");
		botonEntrarEspectador = GetNode<Button>("CenterContainer3/VBoxContainer/Button2");
		
		// El último botón dentro de VBoxContainer o si tienes un botón para salir:
		// Buscamos el nodo correspondiente al botón de cerrar sesión
		botonCerrarSesion    = GetNodeOrNull<Button>("CenterContainer3/VBoxContainer/Button3");

		// 2. Conectar los eventos Pressed
		if (botonEntrarJugador != null)
		{
			botonEntrarJugador.Pressed += OnBotonEntrarJugadorPressed;
		}

		if (botonEntrarEspectador != null)
		{
			botonEntrarEspectador.Pressed += OnBotonEntrarEspectadorPressed;
		}

		if (botonCerrarSesion != null)
		{
			botonCerrarSesion.Pressed += OnBotonCerrarSesionPressed;
		}
	}

	// --- MÉTODOS DE NAVEGACIÓN ---

	// Botón 1: "ENTRAR COMO JUGADOR" -> Cambia a la escena de espera
	private void OnBotonEntrarJugadorPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/espera.tscn");
	}

	// Botón 2: "ENTRAR COMO EXPECTADOR" -> Cambia también a la escena de espera
	private void OnBotonEntrarEspectadorPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/espera.tscn");
	}

	// Botón 3: "CERRAR SESIÓN" -> Regresa al Menú Principal o Login
	private void OnBotonCerrarSesionPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/menu.tscn");
	}
}
