using Godot;
using System;

public partial class SpectatorUI : CanvasLayer
{
	[Signal] public delegate void CycleTargetEventHandler(int direction);

	private Label _spectateLabel;
	private Button _prevButton;
	private Button _nextButton;

	public override void _Ready()
	{
		_spectateLabel = GetNodeOrNull<Label>("%SpectateLabel") ?? FindChild("SpectateLabel") as Label;
		_prevButton = GetNodeOrNull<Button>("%PrevPlayerButton") ?? FindChild("PrevPlayerButton") as Button;
		_nextButton = GetNodeOrNull<Button>("%NextPlayerButton") ?? FindChild("NextPlayerButton") as Button;

		if (_prevButton != null)
		{
			_prevButton.Pressed += () => EmitSignal(SignalName.CycleTarget, -1);
		}

		if (_nextButton != null)
		{
			_nextButton.Pressed += () => EmitSignal(SignalName.CycleTarget, 1);
		}

		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && !keyEvent.Echo)
		{
			if (keyEvent.Keycode == Key.Space || keyEvent.Keycode == Key.Tab)
			{
				EmitSignal(SignalName.CycleTarget, 1);
			}
		}
	}

	public void UpdateSpectateText(string targetName, int targetId)
	{
		if (_spectateLabel != null)
		{
			if (string.IsNullOrEmpty(targetName))
			{
				_spectateLabel.Text = "Spectating: No active players available";
			}
			else
			{
				_spectateLabel.Text = $"Spectating: {targetName} (ID: {targetId})";
			}
		}
	}
}
