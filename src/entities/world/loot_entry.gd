class_name LootEntry extends Resource
## Una entrada de loot: qué ítem puede aparecer, con qué probabilidad y cuántos.

@export var item_id: String = ""
@export_range(0.0, 1.0) var probability: float = 0.5
@export var min_amount: int = 1
@export var max_amount: int = 1
