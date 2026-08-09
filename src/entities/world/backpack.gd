class_name WorldBackpack extends Node3D
## Mochila en el mundo (cofre). Genera loot aleatorio al spawnear.
## Al interactuar con E, abre el BackpackUI del jugador.

@export var loot_table: LootTable
@export var container_capacity: int = 10
@export var container_max_stack: int = 5

var container: ItemContainer = null
var _loot_generated: bool = false


func _ready() -> void:
	container = ItemContainer.new()
	container.capacity = container_capacity
	container.max_stack = container_max_stack
	add_child(container)
	call_deferred("_generate_loot")


func _generate_loot() -> void:
	if _loot_generated or loot_table == null:
		return
	_loot_generated = true
	loot_table.generate(container)


## Llamado por el RayCast del Player al presionar E
func interact(player: Node) -> void:
	var inv = player.get_node_or_null("Inventory")
	if inv == null:
		print("Backpack interact: No se encontro nodo 'Inventory'")
		return
		
	var ui = player.get_node_or_null("BackpackUI")
	if ui != null and ui.has_method("open"):
		ui.open(container, inv)
		return
		
	for child in player.get_children():
		if child.has_method("open") and ("BackpackUI" in child.name or "backpack" in child.name.to_lower()):
			child.open(container, inv)
			return

	for node in player.get_tree().get_nodes_in_group("container_ui"):
		if node.has_method("open"):
			node.open(container, inv)
			return
			
	print("Backpack interact: No se encontro BackpackUI en el jugador ni en el arbol")
