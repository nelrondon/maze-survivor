extends Node
## Test intensivo de UI: busca bugs de duplicación, señales, edge cases.
## Agregar al Maze igual que test_inventory_loader.
## Ejecuta los tests y reporta en consola.

var _passed: int = 0
var _failed: int = 0
var _inv: Inventory = null

func _ready() -> void:
	call_deferred("_run_tests")

func _run_tests() -> void:
	var players: Array[Node] = get_tree().get_nodes_in_group("player")
	if players.is_empty():
		push_warning("TestUI: no player found")
		return
	var player: Node = players[0]
	_inv = player.get_node_or_null("Inventory") as Inventory
	if _inv == null:
		push_warning("TestUI: no Inventory found")
		return

	print("\n========================================")
	print("    TEST INTENSIVO UI — INVENTARIO")
	print("========================================\n")

	# Limpiar inventario para tests controlados
	_inv.clear_all()

	_test_signals_opened_closed()
	_test_signal_hotbar_changed()
	_test_signal_item_use_requested()
	_test_signal_changed_on_add()
	_test_signal_changed_on_swap()
	_test_add_to_full_inventory()
	_test_swap_same_slot()
	_test_swap_with_empty()
	_test_stack_same_item()
	_test_stack_different_items()
	_test_stack_overflow()
	_test_weapon_no_stack()
	_test_remove_more_than_exists()
	_test_clear_slot_already_empty()
	_test_double_clear()
	_test_transfer_empty_slot()
	_test_transfer_to_full()
	_test_instance_data_survives_swap()
	_test_instance_data_survives_transfer()
	_test_hotbar_select_rapid()
	_test_use_empty_slot()
	_test_use_handler_double_use()
	_test_add_zero_amount()
	_test_remove_zero_amount()
	_test_slot_amount_label_accuracy()

	print("\n========================================")
	if _failed == 0:
		print("  RESULTADO: %d tests PASSED" % _passed)
	else:
		print("  RESULTADO: %d passed, %d FAILED" % [_passed, _failed])
	print("========================================\n")

	# Restaurar inventario con items de prueba
	_inv.clear_all()

func _assert(condition: bool, msg: String) -> void:
	if condition:
		_passed += 1
		print("  [OK]   %s" % msg)
	else:
		_failed += 1
		print("  [FAIL] %s" % msg)

func _bandages() -> ItemData:
	return ItemRegistry.get_data("bandages")

func _water() -> ItemData:
	return ItemRegistry.get_data("water")

func _palo() -> ItemData:
	return ItemRegistry.get_data("palo_de_madera")

# =============================================
# SEÑALES
# =============================================

func _test_signals_opened_closed() -> void:
	var opened: Array = [0]
	var closed: Array = [0]
	_inv.inventory_opened.connect(func() -> void: opened[0] += 1)
	_inv.inventory_closed.connect(func() -> void: closed[0] += 1)
	_inv.inventory_opened.emit()
	_inv.inventory_closed.emit()
	_assert(opened[0] == 1, "Señal inventory_opened se emitió 1 vez (actual: %d)" % opened[0])
	_assert(closed[0] == 1, "Señal inventory_closed se emitió 1 vez (actual: %d)" % closed[0])
	# Desconectar para no interferir con otros tests
	for c: Dictionary in _inv.inventory_opened.get_connections():
		_inv.inventory_opened.disconnect(c["callable"])
	for c: Dictionary in _inv.inventory_closed.get_connections():
		_inv.inventory_closed.disconnect(c["callable"])

func _test_signal_hotbar_changed() -> void:
	var result: Array = [-1]
	_inv.hotbar_selection_changed.connect(func(idx: int) -> void: result[0] = idx)
	_inv.select_hotbar(3)
	_assert(result[0] == 3, "hotbar_selection_changed emitió index=3 (actual: %d)" % result[0])
	_inv.select_hotbar(0)
	for c: Dictionary in _inv.hotbar_selection_changed.get_connections():
		_inv.hotbar_selection_changed.disconnect(c["callable"])

func _test_signal_item_use_requested() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 1)
	_inv.select_hotbar(0)
	var result: Array = [-1]
	_inv.item_use_requested.connect(func(idx: int) -> void: result[0] = idx)
	_inv.request_use_item()
	_assert(result[0] == 0, "item_use_requested emitió index=0 (actual: %d)" % result[0])
	for c: Dictionary in _inv.item_use_requested.get_connections():
		_inv.item_use_requested.disconnect(c["callable"])
	_inv.clear_all()

func _test_signal_changed_on_add() -> void:
	var count: Array = [0]
	_inv.changed.connect(func() -> void: count[0] += 1)
	_inv.add_item(_bandages(), 1)
	_assert(count[0] >= 1, "changed se emitió al agregar ítem (veces: %d)" % count[0])
	for c: Dictionary in _inv.changed.get_connections():
		_inv.changed.disconnect(c["callable"])
	_inv.clear_all()

func _test_signal_changed_on_swap() -> void:
	_inv.add_item(_bandages(), 1)
	_inv.add_item(_water(), 1)
	var count: Array = [0]
	_inv.changed.connect(func() -> void: count[0] += 1)
	_inv.swap_slots(0, 1)
	_assert(count[0] >= 1, "changed se emitió al swap (veces: %d)" % count[0])
	for c: Dictionary in _inv.changed.get_connections():
		_inv.changed.disconnect(c["callable"])
	_inv.clear_all()

# =============================================
# DUPLICACIÓN Y EDGE CASES
# =============================================

func _test_add_to_full_inventory() -> void:
	_inv.clear_all()
	# Llenar los 20 slots con items no stackeables
	for i: int in range(20):
		var inst: Dictionary = MeleeWeaponComponent.create_instance_data(100.0)
		_inv.add_item(_palo(), 1, inst)
	var left: int = _inv.add_item(_bandages(), 1)
	_assert(left == 1, "Inventario lleno: sobrante=1 (actual: %d)" % left)
	# Verificar que sigue habiendo exactamente 20 items
	var count: int = 0
	for slot: InventorySlot in _inv.slots:
		if not slot.is_empty():
			count += 1
	_assert(count == 20, "Inventario lleno sigue con 20 slots ocupados (actual: %d)" % count)
	_inv.clear_all()

func _test_swap_same_slot() -> void:
	_inv.add_item(_bandages(), 3)
	var before: int = _inv.slots[0].current_amount
	_inv.swap_slots(0, 0)
	_assert(_inv.slots[0].current_amount == before, "Swap consigo mismo no cambia cantidad")
	_inv.clear_all()

func _test_swap_with_empty() -> void:
	_inv.add_item(_bandages(), 3)
	_inv.swap_slots(0, 10)
	_assert(_inv.slots[0].is_empty(), "Swap con vacío: origen queda vacío")
	_assert(_inv.slots[10].current_amount == 3, "Swap con vacío: destino tiene 3")
	# Verificar no duplicación
	var total: int = 0
	for slot: InventorySlot in _inv.slots:
		if not slot.is_empty():
			total += slot.current_amount
	_assert(total == 3, "Sin duplicación: total sigue siendo 3 (actual: %d)" % total)
	_inv.clear_all()

func _test_stack_same_item() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 3)
	_inv.add_item(_bandages(), 2)
	_assert(_inv.slots[0].current_amount == 5, "Stack: 3+2=5 (actual: %d)" % _inv.slots[0].current_amount)
	_assert(_inv.slots[1].is_empty(), "Stack: no se usó segundo slot")
	_inv.clear_all()

func _test_stack_different_items() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 2)
	_inv.add_item(_water(), 2)
	_assert(_inv.slots[0].item_data.id == "bandages", "Slot 0: bandages")
	_assert(_inv.slots[1].item_data.id == "water", "Slot 1: water (no stackeó)")
	_inv.clear_all()

func _test_stack_overflow() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 4)
	_inv.add_item(_bandages(), 4)
	# max_stack=5: 4+4=8, slot0=5, slot1=3
	_assert(_inv.slots[0].current_amount == 5, "Overflow: slot0=5 (actual: %d)" % _inv.slots[0].current_amount)
	_assert(_inv.slots[1].current_amount == 3, "Overflow: slot1=3 (actual: %d)" % _inv.slots[1].current_amount)
	_inv.clear_all()

func _test_weapon_no_stack() -> void:
	_inv.clear_all()
	var inst: Dictionary = MeleeWeaponComponent.create_instance_data(100.0)
	_inv.add_item(_palo(), 1, inst)
	_inv.add_item(_palo(), 1, inst)
	_assert(_inv.slots[0].current_amount == 1, "Arma en slot 0: cantidad=1")
	_assert(_inv.slots[1].current_amount == 1, "Arma en slot 1: no stackeó")
	_inv.clear_all()

func _test_remove_more_than_exists() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 2)
	var left: int = _inv.remove_item(_bandages(), 10)
	_assert(left == 8, "Remove 10 de 2: sobrante=8 (actual: %d)" % left)
	_assert(_inv.slots[0].is_empty(), "Slot vacío después de remover más de lo que hay")
	_inv.clear_all()

func _test_clear_slot_already_empty() -> void:
	_inv.clear_all()
	_inv.clear_slot(5)
	_assert(_inv.slots[5].is_empty(), "Clear slot vacío no crashea")

func _test_double_clear() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 3)
	_inv.clear_all()
	_inv.clear_all()
	_assert(_inv.is_empty(), "Doble clear no crashea")

func _test_transfer_empty_slot() -> void:
	_inv.clear_all()
	var other: ItemContainer = ItemContainer.new()
	other.capacity = 5
	other.max_stack = 5
	add_child(other)
	var left: int = _inv.transfer_to(other, 0, 1)
	_assert(left == 1, "Transfer slot vacío: retorna amount sin cambios")
	other.queue_free()

func _test_transfer_to_full() -> void:
	_inv.clear_all()
	var other: ItemContainer = ItemContainer.new()
	other.capacity = 1
	other.max_stack = 1
	add_child(other)
	other.add_item(_water(), 1)
	_inv.add_item(_bandages(), 3)
	var left: int = _inv.transfer_to(other, 0, 3)
	_assert(left == 3, "Transfer a contenedor lleno: sobrante=3 (actual: %d)" % left)
	_assert(_inv.slots[0].current_amount == 3, "Origen mantiene los 3")
	other.queue_free()
	_inv.clear_all()

func _test_instance_data_survives_swap() -> void:
	_inv.clear_all()
	var inst: Dictionary = MeleeWeaponComponent.create_instance_data(42.0)
	_inv.add_item(_palo(), 1, inst)
	_inv.add_item(_bandages(), 2)
	_inv.swap_slots(0, 1)
	_assert(_inv.slots[1].instance_data.has("durability"), "instance_data sobrevive swap")
	_assert(_inv.slots[1].instance_data["durability"] == 42.0, "durability=42 tras swap")
	_inv.clear_all()

func _test_instance_data_survives_transfer() -> void:
	_inv.clear_all()
	var other: ItemContainer = ItemContainer.new()
	other.capacity = 5
	other.max_stack = 5
	add_child(other)
	var inst: Dictionary = MeleeWeaponComponent.create_instance_data(77.0)
	_inv.add_item(_palo(), 1, inst)
	_inv.transfer_to(other, 0, 1)
	_assert(other.slots[0].instance_data.has("durability"), "instance_data sobrevive transfer")
	_assert(other.slots[0].instance_data["durability"] == 77.0, "durability=77 tras transfer")
	other.queue_free()
	_inv.clear_all()

func _test_hotbar_select_rapid() -> void:
	# Seleccionar todos los slots rápidamente, no debería crashear
	for i: int in range(20):
		_inv.select_hotbar(i % Inventory.COLUMNS)
	_assert(_inv.selected_hotbar >= 0 and _inv.selected_hotbar < Inventory.COLUMNS, 
		"Selección rápida no crashea, hotbar=%d" % _inv.selected_hotbar)

func _test_use_empty_slot() -> void:
	_inv.clear_all()
	_inv.select_hotbar(0)
	# Debe no hacer nada, no crashear
	_inv.request_use_item()
	_assert(true, "request_use_item en slot vacío no crashea")

func _test_use_handler_double_use() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 5)
	_inv.select_hotbar(0)
	var handler: ItemUseHandler = _inv.get_parent().get_node_or_null("ItemUseHandler") as ItemUseHandler
	if handler == null:
		_assert(true, "[SKIP] ItemUseHandler no encontrado")
		_inv.clear_all()
		return
	_inv.request_use_item()
	_inv.request_use_item()  # Segundo uso mientras el primero está en curso
	# No debería duplicar el consumo ni crashear
	# Esperar un frame para que se procese
	await get_tree().process_frame
	await get_tree().process_frame
	var amount: int = _inv.slots[0].current_amount
	# Con use_time > 0 no debería haber consumido aún (está en timer)
	# Con use_time = 0 debería haber consumido solo 1 (el segundo se ignora)
	_assert(amount >= 3, "Doble uso no consume de más: cantidad=%d (esperado >=3)" % amount)
	handler.cancel_use()
	_inv.clear_all()

func _test_add_zero_amount() -> void:
	_inv.clear_all()
	var left: int = _inv.add_item(_bandages(), 0)
	_assert(left == 0, "add_item(0) retorna 0")
	_assert(_inv.is_empty(), "add_item(0) no agrega nada")

func _test_remove_zero_amount() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 3)
	var removed: int = _inv.remove_item(_bandages(), 0)
	_assert(removed == 0, "remove_item(0) retorna 0")
	_assert(_inv.slots[0].current_amount == 3, "remove_item(0) no quita nada")
	_inv.clear_all()

func _test_slot_amount_label_accuracy() -> void:
	_inv.clear_all()
	_inv.add_item(_bandages(), 1)
	_assert(_inv.slots[0].current_amount == 1, "1 bandage: amount=1")
	_inv.add_item(_bandages(), 1)
	_assert(_inv.slots[0].current_amount == 2, "2 bandages: amount=2")
	_inv.slots[0].remove(1)
	_assert(_inv.slots[0].current_amount == 1, "Remove 1: amount=1")
	_inv.slots[0].remove(1)
	_assert(_inv.slots[0].is_empty(), "Remove último: slot vacío")
	_inv.clear_all()
