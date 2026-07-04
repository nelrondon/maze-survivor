class_name ProjectileWeaponComponent extends WeaponComponent

@export var max_ammo: int = 10
@export var reload_time: float = 1.0
@export var projectile_speed: float = 10.0
var current_ammo: int = 0

func _ready():
	current_ammo = max_ammo

func can_execute(target) -> bool:
	return current_ammo > 0

func execute(target) -> void:
	if !can_execute(target): return
	pass
