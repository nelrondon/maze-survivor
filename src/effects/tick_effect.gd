class_name TickEffect extends Effect

const StatType = Stats.Type

@export var stat: StatType
@export var value: float = 0.0
@export var interval: float = 1.0
@export var duration: float = 0.0

func apply(target) -> void:
	if interval <= 0: return
	if duration <= 0: return
	target.start_tick_effect(stat, value, interval, duration)

func get_description() -> String:
	var name = Stats.get_display_name(stat)
	if value > 0:
		return "Regenera %s de %s cada %ss por %ss" % [value, name, interval, duration]
	else:
		return "Reduce %s de %s cada %ss por %ss" % [abs(value), name, interval, duration]
