class_name PoisonStatus extends StatusEffect

@export var damage: float = 5.0

func _init() -> void:
	id = "veneno"
	is_environment_based = false

func on_tick(target: Node) -> void:
	if target.has_method("modify_stat"):
		target.modify_stat(Stats.Type.HP, -damage)
