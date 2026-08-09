class_name ArrowProjectile extends Area3D

@export var lifetime: float = 5.0

var _velocity: Vector3 = Vector3.ZERO
var _effects: Array[Effect] = []
var _launched: bool = false

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	get_tree().create_timer(lifetime).timeout.connect(queue_free)

## Llamado por la trampa que la dispara.
func launch(direction: Vector3, speed: float, effects: Array[Effect]) -> void:
	_velocity = direction * speed
	_effects = effects
	_launched = true
	if direction.length() > 0.001:
		look_at(global_position + direction, Vector3.UP)

func _physics_process(delta: float) -> void:
	if not _launched:
		return
	global_position += _velocity * delta

func _on_body_entered(body: Node3D) -> void:
	# Ignorar colisión con la propia trampa de origen o zonas de detección
	if body is Area3D:
		return
	if body is StaticBody3D and (body.get_parent() is ArrowTrap or body.name == "MountPlate"):
		return

	if body.is_in_group("boss") or body.is_in_group("enemies"):
		queue_free()
		return

	if body.is_in_group("player") or body.is_in_group("Players"):
		for effect in _effects:
			if effect:
				effect.apply(body)
		queue_free()
	else:
		# Se destruye al chocar contra una pared
		queue_free()
