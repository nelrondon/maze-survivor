class_name WeaponComponent extends ComponentBase

@export var damage: float = 0.0
@export var attack_speed: float = 1.0
@export var knockback: float = 0.0

func can_execute(target) -> bool:
	return false
