class_name WeaponComponent extends ComponentBase

@export var damage: float = 0.0
@export var knockback: float = 0.0

func _init() -> void:
	consumable = false

func can_execute(_user: Node) -> bool:
	return true

func execute(_user: Node) -> void:
	pass
