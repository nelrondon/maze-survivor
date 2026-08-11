extends Node
## Test loader: carga TODOS los ítems del ItemRegistry en el inventario.
## Escanea las carpetas de ítems y agrega uno de cada uno.

func _ready() -> void:
	call_deferred("_load_test_items")

func _load_test_items() -> void:
	var players: Array[Node] = get_tree().get_nodes_in_group("player")
	if players.is_empty():
		push_warning("TestLoader: no se encontró player")
		return
	var player: Node = players[0]
	var inv: Inventory = player.get_node_or_null("Inventory") as Inventory
	if inv == null:
		push_warning("TestLoader: no se encontró Inventory en el player")
		return

	print("\n=== CARGANDO TODOS LOS ITEMS ===\n")

	# Escanear todas las carpetas de ítems recursivamente
	var ids: Array[String] = []
	_scan_folder("res://src/items/consumables/", ids)
	_scan_folder("res://src/items/weapons/", ids)

	for id: String in ids:
		var data: ItemData = ItemRegistry.get_data(id)
		if data == null:
			print("  [SKIP] '%s' no cargó en el registry" % id)
			continue
		var amount: int = 1
		var inst: Dictionary = {}
		# Si es arma, agregar instance_data
		var comp: ComponentBase = ItemRegistry.get_component(id)
		if comp is MeleeWeaponComponent:
			var melee: MeleeWeaponComponent = comp as MeleeWeaponComponent
			inst = MeleeWeaponComponent.create_instance_data(melee.max_durability)
		elif comp is ProjectileWeaponComponent:
			var proj: ProjectileWeaponComponent = comp as ProjectileWeaponComponent
			inst = ProjectileWeaponComponent.create_instance_data(proj.max_ammo)
		# Consumibles stackeables: agregar 3
		if data.stackable:
			amount = 3
		var left: int = inv.add_item(data, amount, inst)
		var extra: String = " " + str(inst) if not inst.is_empty() else ""
		if left > 0:
			print("  + %s x%d (sobrante: %d)%s" % [id, amount, left, extra])
		else:
			print("  + %s x%d%s" % [id, amount, extra])

	_print_inventory(inv)
	print("\n=== TEST LOADER COMPLETO ===\n")


func _scan_folder(path: String, ids: Array[String]) -> void:
	var dir := DirAccess.open(path)
	if dir == null:
		return
	dir.list_dir_begin()
	var file_name: String = dir.get_next()
	while file_name != "":
		var full_path: String = path + file_name
		if dir.current_is_dir():
			# Buscar .tscn con el mismo nombre que la carpeta (convención de ItemEntity)
			var tscn_path: String = full_path + "/" + file_name + ".tscn"
			if ResourceLoader.exists(tscn_path):
				ids.append(file_name)
			else:
				# Subcarpeta (ej: weapons/melee/) — seguir buscando
				_scan_folder(full_path + "/", ids)
		file_name = dir.get_next()
	dir.list_dir_end()


@warning_ignore("integer_division")
func _print_inventory(inv: Inventory) -> void:
	print("\n  --- Estado del inventario ---")
	for i: int in range(inv.slots.size()):
		var slot: InventorySlot = inv.slots[i]
		if slot.is_empty():
			continue
		var row: int = i / Inventory.COLUMNS
		var col: int = i % Inventory.COLUMNS
		var extra: String = ""
		if not slot.instance_data.is_empty():
			extra = " " + str(slot.instance_data)
		var hotbar_tag: String = " [HOTBAR]" if row == 0 else ""
		print("  [%d] (%d,%d) %s x%d%s%s" % [i, row, col, slot.item_data.display_name, slot.current_amount, extra, hotbar_tag])
