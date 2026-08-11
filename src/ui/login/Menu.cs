using Godot;
using System;

public partial class Menu : Control
{
	private Button botonIrLogin;
	private Button botonIrRegistro;
	private Button botonIrReglas;
	private Button botonSalir;

	public override void _Ready()
	{
		botonIrLogin    = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");

		botonIrRegistro = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button2") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button2");

		botonIrReglas   = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button3") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button3");

		botonSalir      = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button4") 
		               ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button4");

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

	private void OnBotonIrLoginPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/login.tscn");
	}

	private void OnBotonIrRegistroPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/registro.tscn");
	}

	private void OnBotonIrReglasPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/reglas.tscn");
	}

	private void OnBotonSalirPressed()
	{
		GetTree().Quit();
	}
}
