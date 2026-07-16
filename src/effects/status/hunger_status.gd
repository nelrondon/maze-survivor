class_name HungerStatus extends StatusEffect

@export var hunger_drain: float = 2.0

func _init() -> void:
	id = "hambre"
	is_environment_based = false

func on_tick(target: Node) -> void:
	if target.has_method("modify_stat"):
		target.modify_stat(Stats.Type.HUNGER, -hunger_drain)
