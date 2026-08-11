using Godot;
using System;
using System.Threading.Tasks;

public partial class LoadingScreen : CanvasLayer
{
	private Label _statusLabel;
	private Label _tipLabel;
	private ProgressBar _progressBar;
	private Tween _progressTween;

	private string _baseStatusText = "Construyendo pasillos y muros";
	private float _animTimer = 0f;
	private int _dotCount = 0;

	private readonly string[] _tips = new string[]
	{
		"CONSEJO: Los botiquines grandes restauran 100 HP. ¡Búscalos en las mochilas!",
		"CONSEJO: Si algún jugador toma la llave del Boss, este correrá directamente a bloquear la salida.",
		"CONSEJO: Hay 12 MiniBosses patrullando el laberinto. ¡Evita llamar su atención si no tienes armas!",
		"CONSEJO: Las trampas de pinchos y disparadores de flechas dañan a todo el que camine sobre ellas.",
		"CONSEJO: Trabaja en equipo para encontrar la llave y ubicar la salida rápidamente."
	};

	public override void _Ready()
	{
		Layer = 100; // Mantener siempre por encima de la UI y cámara 3D
		_statusLabel = GetNodeOrNull<Label>("%StatusLabel");
		_tipLabel = GetNodeOrNull<Label>("%TipLabel");
		_progressBar = GetNodeOrNull<ProgressBar>("%ProgressBar");

		SetRandomTip();
		SetProgress(0f, true);
	}

	public override void _Process(double delta)
	{
		// Animación activa de puntos "Cargando..."
		_animTimer += (float)delta * 3f;
		int newDotCount = (int)_animTimer % 4;
		if (newDotCount != _dotCount)
		{
			_dotCount = newDotCount;
			UpdateStatusDisplay();
		}
	}

	public void SetStatus(string statusText)
	{
		_baseStatusText = statusText;
		UpdateStatusDisplay();
	}

	private void UpdateStatusDisplay()
	{
		if (_statusLabel != null)
		{
			string dots = new string('.', _dotCount);
			_statusLabel.Text = $"{_baseStatusText}{dots}";
		}
	}

	public void SetProgress(float targetValue, bool instant = false)
	{
		targetValue = Math.Clamp(targetValue, 0f, 100f);
		if (_progressBar == null) return;

		if (_progressTween != null && _progressTween.IsValid())
		{
			_progressTween.Kill();
		}

		if (instant)
		{
			_progressBar.Value = targetValue;
		}
		else
		{
			_progressTween = CreateTween().SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
			_progressTween.TweenProperty(_progressBar, "value", targetValue, 0.35f);
		}
	}

	public void SetRandomTip()
	{
		if (_tipLabel != null && _tips.Length > 0)
		{
			var rng = new Random();
			_tipLabel.Text = _tips[rng.Next(_tips.Length)];
		}
	}

	public async Task FadeOutAndFreeAsync()
	{
		SetProgress(100f);
		await ToSignal(GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);

		var tween = CreateTween();
		var control = GetNodeOrNull<Control>("Control");
		if (control != null)
		{
			tween.TweenProperty(control, "modulate:a", 0.0f, 0.4f);
			await ToSignal(tween, Tween.SignalName.Finished);
		}
		QueueFree();
	}
}
