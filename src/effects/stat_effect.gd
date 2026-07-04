class_name StatEffect extends Effect

const StatType = Stats.Type

@export var stat: StatType
@export var value: float = 0.0

func apply(target) -> void:
	target.modify_stat(stat, value)

func get_description() -> String:
	var name = Stats.get_display_name(stat)
	if value > 0:
		return "Restaura %s de %s" % [value, name]
	else:
		return "Reduce %s de %s" % [abs(value), name]
