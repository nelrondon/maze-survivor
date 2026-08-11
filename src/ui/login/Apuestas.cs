using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using InterfazMaze.Models;

public partial class Apuestas : Control
{
	private Label labelSaldo;
	private OptionButton optionPartida;
	private OptionButton optionJugador;
	private OptionButton optionMercado;
	private LineEdit inputMonto;
	private Label labelGanancia;
	private Label labelMensaje;
	private Button buttonApostar;
	private Button buttonVolver;

	private List<Partida> listaPartidas = new List<Partida>();
	private List<Jugador> listaJugadores = new List<Jugador>();

	private decimal cuotaActual = 2.50m;

	public override async void _Ready()
	{
		labelSaldo     = GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LabelSaldo") 
		             ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/LabelSaldo");

		optionPartida  = GetNodeOrNull<OptionButton>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/OptionButtonPartida") 
		             ?? GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/OptionButtonPartida");

		optionJugador  = GetNodeOrNull<OptionButton>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/OptionButtonJugador") 
		             ?? GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/OptionButtonJugador");

		optionMercado  = GetNodeOrNull<OptionButton>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/OptionButtonMercado") 
		             ?? GetNodeOrNull<OptionButton>("CenterContainer/VBoxContainer/OptionButtonMercado");

		inputMonto     = GetNodeOrNull<LineEdit>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LineEditMonto") 
		             ?? GetNodeOrNull<LineEdit>("CenterContainer/VBoxContainer/LineEditMonto");

		labelGanancia  = GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LabelGanancia") 
		             ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/LabelGanancia");

		labelMensaje   = GetNodeOrNull<Label>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/LabelMensaje") 
		             ?? GetNodeOrNull<Label>("CenterContainer/VBoxContainer/LabelMensaje");

		buttonApostar  = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ButtonApostar") 
		             ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonApostar");

		buttonVolver   = GetNodeOrNull<Button>("CenterContainer/PanelContainer/MarginContainer/VBoxContainer/ButtonVolver") 
		             ?? GetNodeOrNull<Button>("CenterContainer/VBoxContainer/ButtonVolver");

		if (buttonApostar != null) buttonApostar.Pressed += OnButtonApostarPressed;
		if (buttonVolver != null) buttonVolver.Pressed += OnButtonVolverPressed;

		if (inputMonto != null)
		{
			inputMonto.TextChanged += OnMontoChanged;
		}

		if (optionMercado != null)
		{
			optionMercado.ItemSelected += OnMercadoSelected;
		}

		ActualizarSaldoUI();
		ConfigurarMercados();
		await CargarPartidasYJugadoresAsync();
	}

	private void ActualizarSaldoUI()
	{
		var mgr = SupabaseManager.Instance;
		decimal saldo = mgr != null ? mgr.GetSaldo() : 100.00m;
		if (labelSaldo != null)
		{
			labelSaldo.Text = $"Saldo Disponible: ${saldo:F2}";
		}
	}


	private void ConfigurarMercados()
	{
		if (optionMercado == null) return;

		optionMercado.Clear();
		optionMercado.AddItem("Ganador de la Partida (Cuota: 2.50)", 0);
		optionMercado.AddItem("Primera Kill (Cuota: 3.00)", 1);
		optionMercado.AddItem("Primera Llave (Cuota: 2.10)", 2);
	}

	private void OnMercadoSelected(long index)
	{
		switch (index)
		{
			case 0: cuotaActual = 2.50m; break;
			case 1: cuotaActual = 3.00m; break;
			case 2: cuotaActual = 2.10m; break;
		}

		CalcularGanancia();
	}

	private void OnMontoChanged(string text)
	{
		CalcularGanancia();
	}

	private void CalcularGanancia()
	{
		if (labelGanancia == null) return;

		if (decimal.TryParse(inputMonto?.Text, out decimal monto) && monto > 0)
		{
			decimal ganancia = monto * cuotaActual;
			labelGanancia.Text = $"Ganancia Potencial: ${ganancia:F2} (Cuota: {cuotaActual:F2})";
		}
		else
		{
			labelGanancia.Text = $"Ganancia Potencial: $0.00 (Cuota: {cuotaActual:F2})";
		}
	}

	private async Task CargarPartidasYJugadoresAsync()
	{
		var mgr = SupabaseManager.Instance;
		if (mgr == null) return;

		MostrarMensaje("Cargando partidas y jugadores...", false);

		listaPartidas = await mgr.ObtenerPartidasActivasAsync();
		listaJugadores = await mgr.ObtenerJugadoresDisponiblesAsync();

		if (optionPartida != null)
		{
			optionPartida.Clear();
			if (listaPartidas.Count > 0)
			{
				foreach (var p in listaPartidas)
				{
					optionPartida.AddItem($"Partida #{p.Id.Substring(0, Math.Min(8, p.Id.Length))} - {p.Estado}");
				}
			}
			else
			{
				optionPartida.AddItem("Partida Demo #1 (Esperando)");
			}
		}

		if (optionJugador != null)
		{
			optionJugador.Clear();
			if (listaJugadores.Count > 0)
			{
				foreach (var j in listaJugadores)
				{
					optionJugador.AddItem($"{j.Nombre} ({j.Rol})");
				}
			}
			else
			{
				optionJugador.AddItem("Superviviente Alfa");
				optionJugador.AddItem("Superviviente Beta");
			}
		}

		MostrarMensaje("", false);
	}

	private async void OnButtonApostarPressed()
	{
		if (!decimal.TryParse(inputMonto?.Text, out decimal monto) || monto <= 0)
		{
			MostrarMensaje("Ingresa un monto válido a apostar.", true);
			return;
		}

		string partidaId = listaPartidas.Count > 0 && optionPartida != null && optionPartida.Selected >= 0 && optionPartida.Selected < listaPartidas.Count
			? listaPartidas[optionPartida.Selected].Id
			: Guid.NewGuid().ToString();

		string jugadorId = listaJugadores.Count > 0 && optionJugador != null && optionJugador.Selected >= 0 && optionJugador.Selected < listaJugadores.Count
			? listaJugadores[optionJugador.Selected].Id
			: Guid.NewGuid().ToString();

		string tipoMercado = optionMercado != null ? optionMercado.GetItemText(optionMercado.Selected) : "Ganador";

		MostrarMensaje("Procesando apuesta...", false);
		if (buttonApostar != null) buttonApostar.Disabled = true;

		var (success, error) = await SupabaseManager.Instance.RealizarApuestaAsync(
			partidaId,
			jugadorId,
			tipoMercado,
			monto,
			cuotaActual
		);

		if (buttonApostar != null) buttonApostar.Disabled = false;

		if (success)
		{
			ActualizarSaldoUI();
			MostrarMensaje($"¡Apuesta registrada con éxito! Ganancia potencial: ${monto * cuotaActual:F2}", false);
			if (inputMonto != null) inputMonto.Text = "";
			CalcularGanancia();
		}
		else
		{
			MostrarMensaje($"Error al apostar: {error}", true);
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

	private void OnButtonVolverPressed()
	{
		GetTree().ChangeSceneToFile("res://src/ui/login/sesion.tscn");
	}
}
