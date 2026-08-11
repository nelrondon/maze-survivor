extends Node
## Test Fase 2: Container + Inventory + ItemUseHandler.

var _passed: int = 0
var _failed: int = 0

func _ready() -> void:
	print("\n========================================")
	print("       TEST FASE 2: INVENTARIO")
	print("========================================\n")

	_test_container_empty()
	_test_add_single()
	_test_add_stacks()
	_test_add_overflow()
	_test_add_weapon_no_stack()
	_test_remove_item()
	_test_remove_partial()
	_test_swap_slots()
	_test_swap_empty()
	_test_transfer_to()
	_test_transfer_overflow()
	_test_hotbar_select()
	_test_hotbar_bounds()
	_test_request_use_empty()
	_test_use_handler_instant()
	_test_use_handler_cancel()
	_test_clear_all()
	_test_contains()
	_test_instance_data_preserved()

	print("\n========================================")
	if _failed == 0:
		print("  RESULTADO: %d tests PASSED" % _passed)
	else:
		print("  RESULTADO: %d passed, %d FAILED" % [_passed, _failed])
	print("========================================\n")

func _assert(condition: bool, msg: String) -> void:
	if condition:
		_passed += 1
		print("  [OK]   %s" % msg)
	else:
		_failed += 1
		print("  [FAIL] %s" % msg)

# --- Helpers ---

func _make_container(cap: int = 10, max_s: int = 5) -> ItemContainer:
	var c := ItemContainer.new()
	c.capacity = cap
	c.max_stack = max_s
	# add_child dispara _ready() automaticamente, no llamar _ready() manual
	add_child(c)
	return c

func _make_inventory() -> Inventory:
	var inv := Inventory.new()
	add_child(inv)
	return inv

func _make_player_with_handler() -> Array:
	# Crea: player > Inventory + ItemUseHandler
	# Retorna [player, inventory, handler]
	var player := Node.new()
	var inv := Inventory.new()
	inv.name = "Inventory"
	var handler := ItemUseHandler.new()
	handler.name = "ItemUseHandler"
	# Agregar hijos ANTES de add_child(player) para que _ready los encuentre
	player.add_child(inv)
	player.add_child(handler)
	add_child(player)
	return [player, inv, handler]

func _bandages() -> ItemData:
	return ItemRegistry.get_data("bandages")

func _palo() -> ItemData:
	return ItemRegistry.get_data("palo_de_madera")

# --- Container tests ---

func _test_container_empty() -> void:
	var c: ItemContainer = _make_container()
	_assert(c.is_empty(), "Container nuevo está vacío")
	_assert(c.slots.size() == 10, "Container tiene 10 slots (actual: %d)" % c.slots.size())
	c.queue_free()

func _test_add_single() -> void:
	var c: ItemContainer = _make_container()
	var left: int = c.add_item(_bandages(), 1)
	_assert(left == 0, "add_item(1) retorna 0 sobrante")
	_assert(c.slots[0].item_data.id == "bandages", "Slot 0 tiene bandages")
	_assert(c.slots[0].current_amount == 1, "Slot 0 tiene cantidad 1")
	c.queue_free()

func _test_add_stacks() -> void:
	var c: ItemContainer = _make_container(5, 3)
	var left: int = c.add_item(_bandages(), 7)
	_assert(left == 0, "add_item(7, max=3, cap=5) cabe todo: sobrante=%d" % left)
	_assert(c.slots[0].current_amount == 3, "Slot 0: 3")
	_assert(c.slots[1].current_amount == 3, "Slot 1: 3")
	_assert(c.slots[2].current_amount == 1, "Slot 2: 1")
	c.queue_free()

func _test_add_overflow() -> void:
	var c: ItemContainer = _make_container(2, 3)
	var left: int = c.add_item(_bandages(), 10)
	_assert(left == 4, "add_item(10, max=3, cap=2) sobrante=4, actual=%d" % left)
	c.queue_free()

func _test_add_weapon_no_stack() -> void:
	var c: ItemContainer = _make_container(5, 5)
	var inst: Dictionary = MeleeWeaponComponent.create_instance_data(100.0)
	c.add_item(_palo(), 1, inst)
	c.add_item(_palo(), 1, inst)
	_assert(c.slots[0].current_amount == 1, "Palo 1 en slot 0")
	_assert(c.slots[1].current_amount == 1, "Palo 2 en slot 1 (no stackeó)")
	c.queue_free()

func _test_remove_item() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 3)
	var left: int = c.remove_item(_bandages(), 3)
	_assert(left == 0, "remove_item(3) de 3: sobrante=0")
	_assert(c.slots[0].is_empty(), "Slot vacío después de remover todo")
	c.queue_free()

func _test_remove_partial() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 3)
	c.remove_item(_bandages(), 1)
	_assert(c.slots[0].current_amount == 2, "remove_item(1) de 3: queda 2")
	c.queue_free()

func _test_swap_slots() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 2)
	c.add_item(_palo(), 1, MeleeWeaponComponent.create_instance_data(100.0))
	c.swap_slots(0, 1)
	_assert(c.slots[0].item_data.id == "palo_de_madera", "Swap: slot 0 ahora es palo")
	_assert(c.slots[1].item_data.id == "bandages", "Swap: slot 1 ahora es bandages")
	c.queue_free()

func _test_swap_empty() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 2)
	c.swap_slots(0, 5)
	_assert(c.slots[0].is_empty(), "Slot 0 vacío después de swap con vacío")
	_assert(c.slots[5].item_data.id == "bandages", "Slot 5 tiene bandages")
	c.queue_free()

func _test_transfer_to() -> void:
	var c1: ItemContainer = _make_container()
	var c2: ItemContainer = _make_container()
	c1.add_item(_bandages(), 3)
	var left: int = c1.transfer_to(c2, 0, 3)
	_assert(left == 0, "Transfer completo: sobrante=0")
	_assert(c1.slots[0].is_empty(), "Origen vacío")
	_assert(c2.slots[0].current_amount == 3, "Destino tiene 3")
	c1.queue_free()
	c2.queue_free()

func _test_transfer_overflow() -> void:
	var c1: ItemContainer = _make_container(5, 5)
	var c2: ItemContainer = _make_container(1, 2)
	c1.add_item(_bandages(), 5)
	var left: int = c1.transfer_to(c2, 0, 5)
	_assert(left == 3, "Transfer a cap=1 max=2: sobrante=3, actual=%d" % left)
	_assert(c1.slots[0].current_amount == 3, "Origen conserva 3 (actual: %d)" % c1.slots[0].current_amount)
	c1.queue_free()
	c2.queue_free()

# --- Inventory tests ---

func _test_hotbar_select() -> void:
	var inv: Inventory = _make_inventory()
	inv.select_hotbar(3)
	_assert(inv.selected_hotbar == 3, "Hotbar selección = 3")
	inv.queue_free()

func _test_hotbar_bounds() -> void:
	var inv: Inventory = _make_inventory()
	inv.select_hotbar(99)
	_assert(inv.selected_hotbar == 0, "Hotbar ignora índice fuera de rango")
	inv.select_hotbar(-1)
	_assert(inv.selected_hotbar == 0, "Hotbar ignora índice negativo")
	inv.queue_free()

func _test_request_use_empty() -> void:
	var inv: Inventory = _make_inventory()
	var result: Array = [false]
	inv.item_use_requested.connect(func(_i: int): result[0] = true)
	inv.request_use_item()
	_assert(result[0] == false, "request_use_item en slot vacío no emite señal")
	inv.queue_free()

# --- ItemUseHandler tests ---

func _test_use_handler_instant() -> void:
	var arr: Array = _make_player_with_handler()
	var player: Node = arr[0]
	var inv: Inventory = arr[1]
	var handler: ItemUseHandler = arr[2]

	inv.add_item(_bandages(), 3)
	inv.select_hotbar(0)

	var result: Array = [false]
	handler.use_completed.connect(func(_i: int): result[0] = true)

	inv.request_use_item()

	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	if comp.use_time == 0.0:
		_assert(result[0], "Handler ejecutó instantáneo (use_time=0)")
		_assert(inv.slots[0].current_amount == 2, "Consumible decrementó: 3→2 (actual: %d)" % inv.slots[0].current_amount)
	else:
		_assert(handler.is_using, "Handler está usando (use_time>0, esperando timer)")

	player.queue_free()

func _test_use_handler_cancel() -> void:
	var arr: Array = _make_player_with_handler()
	var player: Node = arr[0]
	var inv: Inventory = arr[1]
	var handler: ItemUseHandler = arr[2]

	inv.add_item(_bandages(), 3)
	inv.select_hotbar(0)

	var result: Array = [false]
	handler.use_cancelled.connect(func(_i: int): result[0] = true)

	var comp: ComponentBase = ItemRegistry.get_component("bandages")
	if comp.use_time > 0.0:
		inv.request_use_item()
		handler.cancel_use()
		_assert(result[0], "Cancel emitió señal")
		_assert(inv.slots[0].current_amount == 3, "Cancel no consumió el ítem")
	else:
		_assert(true, "bandages use_time=0, skip cancel test")

	player.queue_free()

# --- Otros ---

func _test_clear_all() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 5)
	c.add_item(_palo(), 1, MeleeWeaponComponent.create_instance_data(100.0))
	c.clear_all()
	_assert(c.is_empty(), "clear_all deja el container vacío")
	c.queue_free()

func _test_contains() -> void:
	var c: ItemContainer = _make_container()
	c.add_item(_bandages(), 1)
	_assert(c.contains(_bandages()), "contains bandages = true")
	_assert(not c.contains(_palo()), "contains palo = false")
	c.queue_free()

func _test_instance_data_preserved() -> void:
	var c: ItemContainer = _make_container()
	var inst: Dictionary = MeleeWeaponComponent.create_instance_data(75.0)
	c.add_item(_palo(), 1, inst)
	_assert(c.slots[0].instance_data.has("durability"), "instance_data tiene durability")
	_assert(c.slots[0].instance_data["durability"] == 75.0, "durability = 75.0")
	c.queue_free()
