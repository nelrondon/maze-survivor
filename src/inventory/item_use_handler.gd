class_name ItemUseHandler extends Node
## Timer cancelable para usar ítems.
## Coordina: sonido (use_sound), visual (viewmodel.use()), lógica (execute + on_used).

var inventory: Inventory = null
var is_using: bool = false
var using_slot: InventorySlot = null

var _using_slot_index: int = -1
var _using_component: ComponentBase = null
var _use_timer: Timer = null
var _hand_handler: HandHandler = null
var _audio_player: AudioStreamPlayer = null

signal use_started(slot_index: int, use_time: float)
signal use_completed(slot_index: int)
signal use_cancelled(slot_index: int)


func _ready() -> void:
	_use_timer = Timer.new()
	_use_timer.one_shot = true
	_use_timer.timeout.connect(_on_use_timer_done)
	add_child(_use_timer)

	_audio_player = AudioStreamPlayer.new()
	add_child(_audio_player)

	var player: Node = get_parent()
	inventory = player.get_node_or_null("Inventory") as Inventory
	_hand_handler = player.get_node_or_null("HandHandler") as HandHandler

	if inventory == null:
		push_warning("ItemUseHandler: Inventory not found")
		return

	inventory.item_use_requested.connect(_on_use_requested)
	inventory.hotbar_selection_changed.connect(_on_hotbar_changed)
	inventory.inventory_opened.connect(cancel_use)


func _on_use_requested(slot_index: int) -> void:
	print("[DEBUG] ItemUseHandler: use requested for slot ", slot_index)
	if is_using:
		print("[DEBUG] ItemUseHandler: is_using is true, ignoring")
		return

	var slot: InventorySlot = inventory.get_item(slot_index)
	if slot.is_empty():
		print("[DEBUG] ItemUseHandler: slot is empty, ignoring")
		return

	var comp: ComponentBase = ItemRegistry.get_component(slot.item_data.id)
	if comp == null:
		print("[DEBUG] ItemUseHandler: component is null, ignoring")
		return

	var player: Node = get_parent()
	if not comp.can_execute(player):
		print("[DEBUG] ItemUseHandler: component can_execute returned false, ignoring")
		return

	is_using = true
	using_slot = slot
	_using_slot_index = slot_index
	_using_component = comp

	print("[DEBUG] ItemUseHandler: executing use for component ", comp.name)

	# Reproducir sonido de uso
	if slot.item_data.use_sound != null:
		_audio_player.stream = slot.item_data.use_sound
		_audio_player.play()

	# Llamar use() del viewmodel
	if _hand_handler != null:
		var viewmodel: ViewModelBase = _hand_handler.get_current_viewmodel()
		if viewmodel != null:
			viewmodel.use()

	var use_time: float = comp.use_time
	use_started.emit(slot_index, use_time)

	if use_time > 0.0:
		_use_timer.start(use_time)
	else:
		_on_use_timer_done()


func _on_use_timer_done() -> void:
	if not is_using:
		return
	if using_slot == null:
		return

	var player: Node = get_parent()
	if _using_component != null:
		_using_component.execute(player)
		_using_component.on_used(using_slot)
		inventory.changed.emit()

	var idx: int = _using_slot_index
	_clear_use_state()
	use_completed.emit(idx)


func cancel_use() -> void:
	if not is_using:
		return
	_use_timer.stop()
	_audio_player.stop()
	var idx: int = _using_slot_index
	_clear_use_state()
	use_cancelled.emit(idx)


func _on_hotbar_changed(_index: int) -> void:
	cancel_use()


func _clear_use_state() -> void:
	is_using = false
	using_slot = null
	_using_slot_index = -1
	_using_component = null
