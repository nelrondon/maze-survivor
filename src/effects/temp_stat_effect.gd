class_name TempStatEffect extends Effect

const StatType = Stats.Type

@export var stat: StatType
@export var value: float = 0.0
@export var duration: float = 0.0

func apply(target) -> void:
	if duration <= 0: return
	target.modify_stat(stat, value)
	target.start_temp_effect(stat, value, duration)

func get_description() -> String:
	var name = Stats.get_display_name(stat)
	if value > 0:
		return "Aumenta %s en %s por %ss" % [name, value, duration]
	else:
		return "Reduce %s en %s por %ss" % [name, abs(value), duration]
