class_name ArrowProjectile extends Area3D

## Escena raíz sugerida: Area3D (este script) + CollisionShape3D + MeshInstance3D.
## Configura la Area3D con monitoring = true y que su capa/máscara detecte al
## jugador y a las paredes/suelo (StaticBody3D) para que se destruya al chocar.

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
	if body.is_in_group("player"):
		for effect in _effects:
			effect.apply(body)
	# Se destruye al golpear cualquier cosa (jugador, pared, etc.).
	queue_free()
