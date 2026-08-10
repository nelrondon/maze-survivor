using Godot;
using System.Collections.Generic;

public partial class Player {

	// Piso mínimo de velocidad: ningún debuff (actual o futuro) puede bajar
	// la velocidad a 0 o negativo, porque eso invierte el movimiento
	// (direction * velocidad_negativa = te mueves al revés).
	private const float _minSpeed = 1.5f;

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

	private float _timeSinceLastStaminaUse = 0f;
	private float _staminaRegenDelay = 2.0f;
	private float _staminaRegenRate = 2.0f;
	public bool CanRegenStamina = true;

	private float _hungerTickTimer = 0f;
	private float _hungerTickInterval = 15.0f;
	private float _hungerDrainAmount = 5.0f;

	private float _starvationDamageInterval = 3.0f;
	private float _starvationDamageTimer = 0f;
	private float _starvationLingeringTimer = 0f;
	private bool _isStarving = false;

	public void modify_stat(int stat, float value) {
		if (!_stats.ContainsKey(stat)) return;

		float oldValue = _stats[stat];
		_stats[stat] += value;

		if (_maxStats.ContainsKey(stat)) _stats[stat] = Mathf.Clamp(_stats[stat], 0f, _maxStats[stat]);

		if (stat == 1 && value < 0f) {
			_timeSinceLastStaminaUse = 0f;
		}

		if (stat == 3) {
			// Guardamos _stats[3] SIN clampear para que start_temp_effect pueda
			// revertir el debuff sumando -value más tarde y quede exacto.
			// Solo protegemos _speed (lo que realmente usa el movimiento) de
			// quedar en 0 o negativo.
			_speed = Mathf.Max(_stats[stat], _minSpeed);
		}
		else if (stat == 0 && _stats[stat] <= 0f) {
			TakeDamage();
			Die();
		}

		EmitSignal(SignalName.stats_changed);
	}

	public void ProcessStaminaRegen(double delta) {
		_timeSinceLastStaminaUse += (float)delta;

		if (_timeSinceLastStaminaUse >= _staminaRegenDelay && CanRegenStamina) {
			float currentStamina = _stats[1];
			float maxStamina = _maxStats[1];

			if (currentStamina < maxStamina) {
				float regenRate = _staminaRegenRate;

				// Si el hambre es baja (< 20), reducimos la velocidad de regeneración a la mitad
				if (_stats.ContainsKey(2) && _stats[2] < 20f) {
					regenRate *= 0.5f;
				}

				float newStamina = Mathf.Min(maxStamina, currentStamina + (regenRate * (float)delta));
				if (newStamina != currentStamina) {
					_stats[1] = newStamina;
					EmitSignal(SignalName.stats_changed);
				}
			}
		}
	}

	public void ProcessHunger(double delta) {
		if (_stats.ContainsKey(2) && _stats[0] > 0f) {
			_hungerTickTimer += (float)delta;
			if (_hungerTickTimer >= _hungerTickInterval) {
				_hungerTickTimer = 0f;
				modify_stat(2, -_hungerDrainAmount);
			}
		}
	}

	public void ProcessStarvation(double delta) {
		if (!_stats.ContainsKey(2) || _stats[0] <= 0f) return;

		if (_stats[2] <= 0f) {
			_isStarving = true;
			_starvationLingeringTimer = 3.0f; 
		} else {
			if (_starvationLingeringTimer > 0f) {
				_starvationLingeringTimer -= (float)delta;
			} else {
				_isStarving = false;
			}
		}

		if (_isStarving) {
			_starvationDamageTimer += (float)delta;
			if (_starvationDamageTimer >= _starvationDamageInterval) {
				_starvationDamageTimer = 0f;
				modify_stat(0, -5f); // Daño por inanición
				GD.Print("¡Inanición! Jugador pierde vida por hambre.");
			}
		} else {
			_starvationDamageTimer = 0f;
		}
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
