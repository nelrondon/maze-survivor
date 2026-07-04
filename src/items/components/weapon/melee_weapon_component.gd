class_name MeleeWeaponComponent extends WeaponComponent

@export var max_durability: float = 100.0
@export var attack_range: float = 2.0
var current_durability: float = 0.0

func _ready():
	current_durability = max_durability

func can_execute(target) -> bool:
	return current_durability > 0

func execute(target) -> void:
	if !can_execute(target): return
	pass
