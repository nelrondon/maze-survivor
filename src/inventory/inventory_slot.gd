class_name InventorySlot extends Resource

var item_data: ItemData = null
var current_amount: int = 0
var instance_data: Dictionary = {}

func is_empty() -> bool:
	return item_data == null

func can_stack(other_data: ItemData, max_stack: int) -> bool:
	if is_empty():
		return true
	return item_data.id == other_data.id and item_data.stackable and current_amount < max_stack

func add(amount: int, max_stack: int) -> int:
	var to_add: int = mini(amount, max_stack - current_amount)
	current_amount += to_add
	return amount - to_add   ## retorna sobrante

func remove(amount: int = 1) -> int:
	var to_remove: int = mini(amount, current_amount)
	current_amount -= to_remove
	if current_amount <= 0:
		clear()
	return to_remove   ## retorna cuántos se quitaron

func clear() -> void:
	item_data = null
	current_amount = 0
	instance_data = {}
