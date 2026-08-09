extends ViewModelBase
## Viewmodel genérico para consumibles.
## Muestra el modelo en la mano con animación de equipar/desequipar/usar.

var can_use: bool = true

@onready var _anim: AnimationPlayer = $anim


func use() -> void:
	if not can_use or _anim.is_playing():
		return
	_anim.play("usar")
	can_use = false

func equip() -> void:
	visible = true
	if _anim:
		_anim.play("equipar")


func unequip() -> void:
	if _anim:
		_anim.play("desequipar")
	can_use = false


func _on_anim_animation_finished(anim_name: StringName) -> void:
	match anim_name:
		&"usar", &"equipar":
			can_use = true
		&"desequipar":
			visible = false
