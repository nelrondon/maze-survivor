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

	public float get_stat(int stat) {
		return _stats.TryGetValue(stat, out float val) ? val : 0f;
	}

	public float get_max_stat(int stat) {
		return _maxStats.TryGetValue(stat, out float val) ? val : 100f;
	}

	public float GetStat(int stat) => get_stat(stat);
	public float GetMaxStat(int stat) => get_max_stat(stat);

	public string get_stats_text() {
		return $"HP: {_stats[0]} / 100\nEstamina: {_stats[1]} / 100\nHambre: {_stats[2]} / 100\nVelocidad: {_stats[3]}";
	}

	public string get_active_effects_text() {
		if (_statusManager == null) return "Ninguno";
		
		var activeStatuses = _statusManager.Get("active_statuses").AsGodotDictionary();
		if (activeStatuses == null || activeStatuses.Count == 0) return "Ninguno";
		
		var statusTexts = new List<string>();
		foreach (var key in activeStatuses.Keys) {
			var statusObj = activeStatuses[key].AsGodotObject();
			if (statusObj != null) {
				string statusId = statusObj.Get("id").AsString();
				float currentDuration = (float)statusObj.Get("current_duration").AsDouble();
				bool isEnv = statusObj.Get("is_environment_based").AsBool();
				
				if (isEnv) {
					statusTexts.Add($"{statusId} (Entorno)");
				}
				else {
					statusTexts.Add($"{statusId} ({currentDuration:F1}s)");
				}
			}
		}
		
		return string.Join("\n", statusTexts);
	}
}
