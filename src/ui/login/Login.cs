using Godot;
using System;
using System.Threading.Tasks;

public partial class Login : Control
{
	private LineEdit inputEmail;
	private LineEdit inputPassword;
	private Label labelMensaje;

	private Button botonIniciarSesion;
	private Button botonVolverMenu;

	public override void _Ready()
	{
		inputEmail    = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit");

		inputPassword = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEdit2") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEdit2");

		labelMensaje  = GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LabelMensaje") 
		             ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/LabelMensaje");

		botonIniciarSesion = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button") 
		                  ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button");

		botonVolverMenu    = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/Button2") 
		                  ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/Button2");

		if (botonIniciarSesion != null)
		{
			botonIniciarSesion.Pressed += OnBotonIniciarSesionPressed;
		}

		if (botonVolverMenu != null)
		{
			botonVolverMenu.Pressed += OnBotonVolverMenuPressed;
		}
	}

	private async void OnBotonIniciarSesionPressed()
	{
		string email    = inputEmail?.Text?.Trim();
		string password = inputPassword?.Text?.Trim();

		if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
		{
			MostrarMensaje("Por favor ingresa tu correo y contraseña.", true);
			return;
		}

		MostrarMensaje("Verificando credenciales...", false);
		if (botonIniciarSesion != null) botonIniciarSesion.Disabled = true;

		var (success, error) = await SupabaseManager.Instance.SignInAsync(email, password);

		if (botonIniciarSesion != null) botonIniciarSesion.Disabled = false;

		if (success)
		{
			MostrarMensaje("¡Inicio de sesión exitoso!", false);
			await Task.Delay(500);
			GetTree().ChangeSceneToFile("res://src/ui/login/sesion.tscn");
		}
		else
		{
			MostrarMensaje($"Error de acceso: {error}", true);
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
