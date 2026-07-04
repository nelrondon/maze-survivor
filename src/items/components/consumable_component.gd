class_name ConsumableComponent extends ComponentBase

@export var effects: Array[Effect] = []
@export var use_time: float = 0.0

func can_execute(target) -> bool:
	return effects.size() > 0

func execute(target) -> void:
	if !can_execute(target): return
	for effect in effects:
		effect.apply(target)
