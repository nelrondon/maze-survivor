class_name ConsumableComponent extends ComponentBase

@export var effects: Array[Effect] = []

func _init() -> void:
	consumable = true

func can_execute(_user: Node) -> bool:
	return effects.size() > 0

func execute(user: Node) -> void:
	if not can_execute(user):
		return
	for effect: Effect in effects:
		effect.apply(user)
