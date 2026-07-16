class_name AsphyxiaStatus extends StatusEffect

@export var damage: float = 10.0

func _init() -> void:
	id = "asfixia"
	is_environment_based = true # El estado de asfixia dura mientras el jugador esté en la zona

func on_start(target: Node) -> void:
	if target.has_method("set_stamina_regen"):
		target.set_stamina_regen(0.0)

func on_tick(target: Node) -> void:
	if target.has_method("modify_stat"):
		target.modify_stat(Stats.Type.HP, -damage)

func on_end(target: Node) -> void:
	if target.has_method("set_stamina_regen"):
		# Restaura la regeneración de estamina por defecto
		target.set_stamina_regen(5.0)
