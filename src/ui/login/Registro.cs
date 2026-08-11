using Godot;
using System;
using System.Threading.Tasks;

public partial class Registro : Control
{
	private LineEdit inputNombre;
	private LineEdit inputCedula;
	private LineEdit inputEmail;
	private LineEdit inputUsername;
	private LineEdit inputPassword;
	private Label labelMensaje;

	private Button botonRegistrarse;
	private Button botonVolverMenu;

	public override void _Ready()
	{
		inputNombre   = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit");

		inputCedula   = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit2") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit2");

		inputEmail    = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit3") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit3");

		inputUsername = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit4") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit4");

		inputPassword = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit5") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit5");

		labelMensaje  = GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LabelMensaje") 
		             ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/LabelMensaje");

		botonRegistrarse = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
		                ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");

		botonVolverMenu   = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button2") 
		                ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button2");

		if (botonRegistrarse != null)
		{
			botonRegistrarse.Pressed += OnBotonRegistrarsePressed;
		}

		if (botonVolverMenu != null)
		{
			botonVolverMenu.Pressed += OnBotonVolverMenuPressed;
		}
	}

	private async void OnBotonRegistrarsePressed()
	{
		string nombre   = inputNombre?.Text?.Trim();
		string cedula   = inputCedula?.Text?.Trim();
		string email    = inputEmail?.Text?.Trim();
		string username = inputUsername?.Text?.Trim();
		string password = inputPassword?.Text?.Trim();

		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(username))
		{
			MostrarMensaje("Ingresa al menos Correo, Usuario y Contraseña.", true);
			return;
		}

		MostrarMensaje("Registrando usuario...", false);
		if (botonRegistrarse != null) botonRegistrarse.Disabled = true;

		var (success, error) = await SupabaseManager.Instance.SignUpAsync(email, password, string.IsNullOrEmpty(nombre) ? username : nombre, username, "jugador");


		if (botonRegistrarse != null) botonRegistrarse.Disabled = false;

		if (success)
		{
			MostrarMensaje("¡Registro exitoso! Iniciando sesión...", false);
			await Task.Delay(1000);
			GetTree().ChangeSceneToFile("res://src/ui/login/sesion.tscn");
		}
		else
		{
			MostrarMensaje($"Error: {error}", true);
		}
	}

	private void MostrarMensaje(string text, bool esError)
	{
		if (labelMensaje != null)
		{
			labelMensaje.Text = text;
			labelMensaje.Modulate = esError ? new Color(1, 0.4f, 0.4f) : new Color(0.4f, 1, 0.4f);
		}
	}

	private void OnBotonVolverMenuPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/menu.tscn");
	}
}
