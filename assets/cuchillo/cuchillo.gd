extends ViewModelBase
## Viewmodel del cuchillo de combate.

var enemies_in_range: Array[Node3D] = []
var can_use: bool = true
var damage: float = 0.0

@onready var _anim: AnimationPlayer = $anim
@onready var _sound: AudioStreamPlayer = $Sonido_cuchillo

func use() -> void:
	if not can_use or _anim.is_playing():
		return
	_anim.play("Golpear")
	_sound.play()
	can_use = false
	get_tree().create_timer(0.25).timeout.connect(_deal_damage)

func _deal_damage() -> void:
	# Iterar copiando el array por si cambia durante la iteración
	for enemy: Node3D in enemies_in_range.duplicate():
		if is_instance_valid(enemy) and enemy.has_method("hit"):
			enemy.hit(damage)

func equip() -> void:
	visible = true
	if _anim:
		_anim.play("Equipar")

func unequip() -> void:
	if _anim:
		_anim.play("Desequipar")
	can_use = false

func _on_hitbox_body_entered(body: Node3D) -> void:
	if body.has_method("hit") and not body.is_ancestor_of(self) and not enemies_in_range.has(body):
		enemies_in_range.append(body)

func _on_hitbox_body_exited(body: Node3D) -> void:
	enemies_in_range.erase(body)

func _on_anim_animation_finished(anim_name: StringName) -> void:
	if anim_name == &"Golpear" or anim_name == &"Equipar":
		can_use = true
	elif anim_name == &"Desequipar":
		visible = false
