extends ViewModelBase
## Viewmodel del palo de madera. Golpe melee con hitbox.

var enemies_in_range: Array[Node3D] = []
var can_use: bool = true
var damage: float = 0.0

@onready var _anim: AnimationPlayer = $anim
@onready var _sound: AudioStreamPlayer = $palo_de_madera_sound


var _portador: Node3D = null

func _ready() -> void:
	_actualizar_portador()

func _actualizar_portador() -> void:
	var p = get_parent()
	while p != null:
		if p.is_in_group("player") or p.is_in_group("Players"):
			_portador = p
			break
		p = p.get_parent()

func _find_damageable_target(node: Node) -> Node:
	var curr: Node = node
	while curr != null:
		if curr.has_method("hit"):
			return curr
		if curr.is_in_group("player") or curr.is_in_group("Players"):
			return curr
		curr = curr.get_parent()
	return null


func use() -> void:
	if not can_use or _anim.is_playing():
		return
	if not is_instance_valid(_portador):
		_actualizar_portador()
	_anim.play("Golpear")
	_sound.play()
	can_use = false
	get_tree().create_timer(0.25).timeout.connect(_deal_damage)

func _deal_damage() -> void:
	for enemy: Node3D in enemies_in_range.duplicate():
		if is_instance_valid(enemy) and enemy.has_method("hit"):
			print("[PaloDeMadera] Asestando golpe melee a ", enemy.name, " (Daño: ", damage, ")")
			enemy.call("hit", damage, _portador)


func equip() -> void:
	visible = true
	if _anim:
		_anim.play("equipar")


func unequip() -> void:
	if _anim:
		_anim.play("desequipar")
	can_use = false


func _on_hitbox_body_entered(body: Node3D) -> void:
	if not is_instance_valid(_portador):
		_actualizar_portador()
	var target = _find_damageable_target(body)
	if target != null and target != _portador and not target.is_ancestor_of(self):
		if not enemies_in_range.has(target):
			enemies_in_range.append(target)


func _on_hitbox_body_exited(body: Node3D) -> void:
	var target = _find_damageable_target(body)
	if target != null:
		enemies_in_range.erase(target)


func _on_anim_animation_finished(anim_name: StringName) -> void:
	match anim_name:
		&"Golpear", &"equipar":
			can_use = true
		&"desequipar":
			visible = false

