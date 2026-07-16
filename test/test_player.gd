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

var status_manager: StatusManager

func _ready() -> void:
	status_manager = StatusManager.new()
	status_manager.target = self
	add_child(status_manager)
	status_manager.status_added.connect(func(_id, _icon, _duration): stats_changed.emit())
	status_manager.status_removed.connect(func(_id): stats_changed.emit())
	status_manager.status_updated.connect(func(_id, _dur): stats_changed.emit())

func apply_status(status_effect: StatusEffect) -> void:
	if status_manager:
		status_manager.apply_status(status_effect)

func remove_status(status_id: String) -> void:
	if status_manager:
		status_manager.remove_status(status_id)

func modify_stat(stat: Stats.Type, value: float) -> void:
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
	if not status_manager or status_manager.active_statuses.is_empty():
		return "Ninguno\n"
	var text = ""
	for status_id in status_manager.active_statuses:
		var status = status_manager.active_statuses[status_id]
		if status.is_environment_based:
			text += "[ENV] %s (Entorno)\n" % status.id
		else:
			text += "[STATUS] %s: %.1fs\n" % [status.id, status.current_duration]
	return text
