using Godot;
using System;

public partial class Menu : Control
{
	// Declaración de los 4 botones del menú
	private Button botonIrLogin;
	private Button botonIrRegistro;
	private Button botonIrReglas;
	private Button botonSalir;

	public override void _Ready()
	{
		// 1. Obtener las referencias de los nodos según el árbol de la escena
		botonIrLogin    = GetNode<Button>("CenterContainer/VBoxContainer/Button");
		botonIrRegistro = GetNode<Button>("CenterContainer/VBoxContainer/Button2");
		botonIrReglas   = GetNode<Button>("CenterContainer/VBoxContainer/Button3");
		botonSalir      = GetNode<Button>("CenterContainer/VBoxContainer/Button4");

		// 2. Conectar los eventos 'Pressed' a sus respectivas funciones
		if (botonIrLogin != null)
		{
			botonIrLogin.Pressed += OnBotonIrLoginPressed;
		}

		if (botonIrRegistro != null)
		{
			botonIrRegistro.Pressed += OnBotonIrRegistroPressed;
		}

		if (botonIrReglas != null)
		{
			botonIrReglas.Pressed += OnBotonIrReglasPressed;
		}

		if (botonSalir != null)
		{
			botonSalir.Pressed += OnBotonSalirPressed;
		}
	}

	// --- MÉTODOS DE NAVEGACIÓN ---

	// Botón 1: Iniciar Sesión
	private void OnBotonIrLoginPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/login.tscn");
	}

	// Botón 2: Regístrate
	private void OnBotonIrRegistroPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/registro.tscn");
	}

	// Botón 3: Reglas del Juego
	private void OnBotonIrReglasPressed()
	{
		GetTree().ChangeSceneToFile("res://LoginInterface/reglas.tscn");
	}

	// Botón 4: Salir del Menú
	private void OnBotonSalirPressed()
	{
		// Cierra la aplicación / juego
		GetTree().Quit();
	}
}
