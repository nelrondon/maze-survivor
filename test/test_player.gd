extends Node3D

signal stats_changed

var stats = {
	Stats.Type.HP: 100.0,
	Stats.Type.STAMINA: 100.0,
	Stats.Type.HUNGER: 100.0,
	Stats.Type.SPEED: 10.0,
	Stats.Type.SPRINT_SPEED: 15.0,
	Stats.Type.DEFENSE: 5.0,
	Stats.Type.BASE_DAMAGE: 5.0,
	Stats.Type.ATTACK_SPEED: 1.0
}

var max_stats = {
	Stats.Type.HP: 100.0,
	Stats.Type.STAMINA: 100.0,
	Stats.Type.HUNGER: 100.0,
}

var active_temp_effects: Array = []
var active_tick_effects: Array = []

func modify_stat(stat: Stats.Type, value: float):
	if not stats.has(stat):
		print("[ERROR] Stat no existe")
		return
	var old_value = stats[stat]
	stats[stat] += value
	if max_stats.has(stat):
		stats[stat] = clamp(stats[stat], 0, max_stats[stat])
	var name = Stats.get_display_name(stat)
	print("[STAT] %s: %s -> %s" % [name, old_value, stats[stat]])
	stats_changed.emit()

func start_temp_effect(stat: Stats.Type, value: float, duration: float):
	var effect_data = {"stat": stat, "value": value, "remaining": duration}
	active_temp_effects.append(effect_data)
	var name = Stats.get_display_name(stat)
	print("[TEMP] +%s %s por %ss" % [value, name, duration])
	stats_changed.emit()

	var timer = Timer.new()
	timer.wait_time = duration
	timer.one_shot = true
	add_child(timer)
	timer.start()
	await timer.timeout
	timer.queue_free()

	modify_stat(stat, -value)
	active_temp_effects.erase(effect_data)
	print("[TEMP] Efecto de %s terminado" % name)
	stats_changed.emit()

func start_tick_effect(stat: Stats.Type, value: float, interval: float, duration: float):
	var effect_data = {"stat": stat, "value": value, "remaining": duration}
	active_tick_effects.append(effect_data)
	var name = Stats.get_display_name(stat)
	print("[TICK] %s %s cada %ss por %ss" % [value, name, interval, duration])
	stats_changed.emit()

	var ticks = int(duration / interval)
	var timer = Timer.new()
	timer.wait_time = interval
	timer.one_shot = false
	add_child(timer)
	timer.start()

	for i in range(ticks):
		await timer.timeout
		if stats[Stats.Type.HP] <= 0:
			break
		modify_stat(stat, value)
		effect_data["remaining"] -= interval
		stats_changed.emit()

	timer.queue_free()
	active_tick_effects.erase(effect_data)
	print("[TICK] Efecto de %s terminado" % name)
	stats_changed.emit()

func get_stats_text() -> String:
	var text = ""
	for stat in stats:
		var name = Stats.get_display_name(stat)
		var line = "%s: %s" % [name, stats[stat]]
		if max_stats.has(stat):
			line += " / %s" % max_stats[stat]
		text += line + "\n"
	return text

func get_active_effects_text() -> String:
	var text = ""
	for e in active_temp_effects:
		var name = Stats.get_display_name(e["stat"])
		text += "[TEMP] %s: %+.0f (%.1fs)\n" % [name, e["value"], e["remaining"]]
	for e in active_tick_effects:
		var name = Stats.get_display_name(e["stat"])
		text += "[TICK] %s: %+.0f (%.1fs)\n" % [name, e["value"], e["remaining"]]
	if text.is_empty():
		text = "Ninguno\n"
	return text
