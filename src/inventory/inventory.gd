class_name Inventory extends ItemContainer

const COLUMNS: int = 5
const ROWS: int = 4
const HOTBAR_ROW: int = 0

var selected_hotbar: int = 0

signal item_use_requested(slot_index: int)
signal hotbar_selection_changed(index: int)
@warning_ignore("unused_signal")
signal inventory_opened
@warning_ignore("unused_signal")
signal inventory_closed
signal swap_rejected(index_a: int, index_b: int)

func _init() -> void:
	capacity = COLUMNS * ROWS
	max_stack = 5

func _can_place(_index: int, _item_data: ItemData) -> bool:
	return true

# --- Utilidades ---

func get_slot_index(row: int, col: int) -> int:
	return row * COLUMNS + col

func get_first_valid_slot(item_data: ItemData) -> int:
	for i: int in range(slots.size()):
		if slots[i].is_empty():
			return i
		if slots[i].can_stack(item_data, max_stack):
			return i
	return -1

# --- Swap ---

func swap_slots(index_a: int, index_b: int) -> bool:
	var result: bool = super.swap_slots(index_a, index_b)
	if not result:
		swap_rejected.emit(index_a, index_b)
	return result

# --- Hotbar ---

func select_hotbar(index: int) -> void:
	if index < 0 or index >= COLUMNS:
		return
	selected_hotbar = index
	hotbar_selection_changed.emit(index)

func get_selected_item() -> InventorySlot:
	return slots[selected_hotbar]

func get_hotbar_slots() -> Array[InventorySlot]:
	var hotbar: Array[InventorySlot] = []
	for col: int in range(COLUMNS):
		hotbar.append(slots[col])
	return hotbar

# --- Solicitud de uso ---

func request_use_item() -> void:
	var slot: InventorySlot = slots[selected_hotbar]
	if slot.is_empty():
		return
	item_use_requested.emit(selected_hotbar)
