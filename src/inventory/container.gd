class_name ItemContainer extends Node

@export var capacity: int = 20
@export var max_stack: int = 5

var slots: Array[InventorySlot] = []

signal changed
signal slot_updated(index: int)

func _ready() -> void:
	for i: int in range(capacity):
		slots.append(InventorySlot.new())

func _can_place(_index: int, _item_data: ItemData) -> bool:
	return true

# --- Acceso directo ---

func get_item(index: int) -> InventorySlot:
	return slots[index]

func set_item(index: int, item_data: ItemData, amount: int = 1, inst_data: Dictionary = {}) -> void:
	if not _can_place(index, item_data):
		return
	slots[index].item_data = item_data
	slots[index].current_amount = amount
	slots[index].instance_data = inst_data
	slot_updated.emit(index)
	changed.emit()

func clear_slot(index: int) -> void:
	slots[index].clear()
	slot_updated.emit(index)
	changed.emit()

func clear_all() -> void:
	for i: int in range(slots.size()):
		slots[i].clear()
	changed.emit()

func is_empty() -> bool:
	for slot: InventorySlot in slots:
		if not slot.is_empty():
			return false
	return true

# --- Búsqueda ---

func first(item_data: ItemData) -> int:
	for i: int in range(slots.size()):
		if not _can_place(i, item_data):
			continue
		if not slots[i].is_empty() and slots[i].item_data.id == item_data.id:
			return i
	return -1

func first_empty_for(item_data: ItemData) -> int:
	for i: int in range(slots.size()):
		if not _can_place(i, item_data):
			continue
		if slots[i].is_empty():
			return i
	return -1

func contains(item_data: ItemData) -> bool:
	return first(item_data) != -1

# --- Agregar / Quitar ---

func add_item(item_data: ItemData, amount: int = 1, inst_data: Dictionary = {}) -> int:
	for i: int in range(slots.size()):
		if amount <= 0:
			break
		if not _can_place(i, item_data):
			continue
		if not slots[i].is_empty() and slots[i].can_stack(item_data, max_stack):
			amount = slots[i].add(amount, max_stack)
	while amount > 0:
		var idx: int = first_empty_for(item_data)
		if idx == -1:
			break
		slots[idx].item_data = item_data
		slots[idx].instance_data = inst_data.duplicate()
		amount = slots[idx].add(amount, max_stack)
	changed.emit()
	return amount

func remove_item(item_data: ItemData, amount: int = 1) -> int:
	for i: int in range(slots.size()):
		if amount <= 0:
			break
		if not slots[i].is_empty() and slots[i].item_data.id == item_data.id:
			amount -= slots[i].remove(amount)
	changed.emit()
	return amount

# --- Swap ---

func swap_slots(index_a: int, index_b: int) -> bool:
	var slot_a: InventorySlot = slots[index_a]
	var slot_b: InventorySlot = slots[index_b]
	if not slot_a.is_empty() and not _can_place(index_b, slot_a.item_data):
		return false
	if not slot_b.is_empty() and not _can_place(index_a, slot_b.item_data):
		return false
	slots[index_a] = slot_b
	slots[index_b] = slot_a
	changed.emit()
	return true

# --- Transferencia entre contenedores ---

func transfer_to(target: ItemContainer, from_index: int, amount: int = 1) -> int:
	var slot: InventorySlot = slots[from_index]
	if slot.is_empty():
		return amount
	var to_transfer: int = mini(amount, slot.current_amount)
	var remaining: int = target.add_item(slot.item_data, to_transfer, slot.instance_data)
	var transferred: int = to_transfer - remaining
	if transferred > 0:
		slot.remove(transferred)
		changed.emit()
	return remaining
