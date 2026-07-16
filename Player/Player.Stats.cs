using Godot;
using System.Collections.Generic;

public partial class Player {

	private Dictionary<int, float> _stats = new() {
		{ 0, 100f },
		{ 1, 100f },
		{ 2, 100f },
		{ 3, 9.0f },
		{ 4, 15f },
		{ 5, 5f },
		{ 6, 5f },
		{ 7, 1f }
	};

	private Dictionary<int, float> _maxStats = new() {
		{ 0, 100f },
		{ 1, 100f },
		{ 2, 100f }
	};

	public void modify_stat(int stat, float value) {
		if (!_stats.ContainsKey(stat)) return;

		float oldValue = _stats[stat];
		_stats[stat] += value;

		if (_maxStats.ContainsKey(stat)) _stats[stat] = Mathf.Clamp(_stats[stat], 0f, _maxStats[stat]);

		if (stat == 3) _speed = _stats[stat];
		else if (stat == 0 && _stats[stat] <= 0f) TakeDamage();

		EmitSignal(SignalName.stats_changed);
	}

	public async void start_temp_effect(int stat, float value, float duration) {
		EmitSignal(SignalName.stats_changed);

		await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);

		modify_stat(stat, -value);
		EmitSignal(SignalName.stats_changed);
	}

	public async void start_tick_effect(int stat, float value, float interval, float duration) {
		EmitSignal(SignalName.stats_changed);

		int ticks = (int)(duration / interval);
		for (int i = 0; i < ticks; i++) {
			await ToSignal(GetTree().CreateTimer(interval), SceneTreeTimer.SignalName.Timeout);
			
			if (_stats[0] <= 0f) break;
			modify_stat(stat, value);
		}
	}

	public string get_stats_text() {
		return $"HP: {_stats[0]} / 100\nEstamina: {_stats[1]} / 100\nHambre: {_stats[2]} / 100\nVelocidad: {_stats[3]}";
	}

	public string get_active_effects_text() {
		return "Activo";
	}
}
