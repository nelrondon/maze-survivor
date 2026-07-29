class_name PlayerHUD extends CanvasLayer

@export var player: Node
@export var max_bar_width: float = 140.0

@onready var hp_fill: ColorRect = $Frame/Margin/VBox/HPContainer/Background/Fill
@onready var stamina_fill: ColorRect = $Frame/Margin/VBox/StaminaContainer/Background/Fill
@onready var hunger_fill: ColorRect = $Frame/Margin/VBox/HungerContainer/Background/Fill

@onready var hp_label: Label = $Frame/Margin/VBox/HPContainer/Label
@onready var stamina_label: Label = $Frame/Margin/VBox/StaminaContainer/Label
@onready var hunger_label: Label = $Frame/Margin/VBox/HungerContainer/Label
@onready var effects_label: Label = $Frame/Margin/VBox/EffectsLabel

var _hp_tween: Tween
var _stamina_tween: Tween
var _hunger_tween: Tween

func _ready() -> void:
	if player == null:
		# 1. Si el HUD es hijo directo del jugador en player.tscn
		if get_parent() != null and (get_parent().is_in_group("player") or get_parent().has_signal("stats_changed")):
			player = get_parent()
		else:
			# 2. Respaldo: Buscar en el grupo 'player'
			var players = get_tree().get_nodes_in_group("player")
			if players.size() > 0:
				player = players[0]

	# En multijugador, si el jugador no es la autoridad local de este cliente, ocultamos el HUD
	if player != null and player.has_method("IsMultiplayerAuthority"):
		if not player.IsMultiplayerAuthority():
			hide()
			return

	if player != null:
		setup_player(player)

func setup_player(p_player: Node) -> void:
	player = p_player
	if player != null and player.has_signal("stats_changed"):
		if not player.is_connected("stats_changed", _on_stats_changed):
			player.connect("stats_changed", _on_stats_changed)

	var status_mgr = _get_status_manager()
	if status_mgr != null:
		if status_mgr.has_signal("status_added") and not status_mgr.is_connected("status_added", _on_status_event):
			status_mgr.connect("status_added", _on_status_event)
		if status_mgr.has_signal("status_removed") and not status_mgr.is_connected("status_removed", _on_status_event):
			status_mgr.connect("status_removed", _on_status_event)
		if status_mgr.has_signal("status_updated") and not status_mgr.is_connected("status_updated", _on_status_event):
			status_mgr.connect("status_updated", _on_status_event)

	update_bars(false) # Actualización inicial sin animación

func _process(_delta: float) -> void:
	if effects_label and player != null and player.has_method("get_active_effects_text"):
		effects_label.text = "EFECTOS:\n" + player.get_active_effects_text()

func _on_stats_changed() -> void:
	update_bars(true)

func _on_status_event(_arg1 = null, _arg2 = null, _arg3 = null) -> void:
	if effects_label and player != null and player.has_method("get_active_effects_text"):
		effects_label.text = "EFECTOS:\n" + player.get_active_effects_text()

func _get_status_manager() -> Node:
	if player == null:
		return null
	if player.has_node("StatusManager"):
		return player.get_node("StatusManager")
	return null

func update_bars(animate: bool = true) -> void:
	if player == null:
		return

	# Obtenemos valores usando los getters expuestos en Player.Stats.cs o Stats.Type
	var hp: float = _get_player_stat(0, 100.0)
	var max_hp: float = _get_player_max_stat(0, 100.0)
	
	var stamina: float = _get_player_stat(1, 100.0)
	var max_stamina: float = _get_player_max_stat(1, 100.0)
	
	var hunger: float = _get_player_stat(2, 100.0)
	var max_hunger: float = _get_player_max_stat(2, 100.0)

	_update_bar(hp_fill, hp, max_hp, "hp_tween", animate)
	_update_bar(stamina_fill, stamina, max_stamina, "stamina_tween", animate)
	_update_bar(hunger_fill, hunger, max_hunger, "hunger_tween", animate)

	if hp_label: hp_label.text = "VIDA: %d / %d" % [int(hp), int(max_hp)]
	if stamina_label: stamina_label.text = "ESTAMINA: %d / %d" % [int(stamina), int(max_stamina)]
	if hunger_label: hunger_label.text = "HAMBRE: %d / %d" % [int(hunger), int(max_hunger)]

	if effects_label and player.has_method("get_active_effects_text"):
		effects_label.text = "EFECTOS:\n" + player.get_active_effects_text()

func _update_bar(fill_rect: ColorRect, current: float, max_val: float, tween_slot: String, animate: bool) -> void:
	if fill_rect == null:
		return
		
	var parent_bg = fill_rect.get_parent()
	var bar_max_w: float = max_bar_width
	if parent_bg and parent_bg is Control and parent_bg.size.x > 0:
		bar_max_w = parent_bg.size.x
		
	var ratio: float = clamp(current / max(max_val, 1.0), 0.0, 1.0)
	var target_width: float = ratio * bar_max_w

	var current_tween: Tween = get("_" + tween_slot)
	if current_tween and current_tween.is_running():
		current_tween.kill()

	if animate:
		var new_tween = create_tween().set_parallel(true)
		new_tween.tween_property(fill_rect, "size:x", target_width, 0.25).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
		new_tween.tween_property(fill_rect, "custom_minimum_size:x", target_width, 0.25).set_trans(Tween.TRANS_SINE).set_ease(Tween.EASE_OUT)
		set("_" + tween_slot, new_tween)
	else:
		fill_rect.size.x = target_width
		fill_rect.custom_minimum_size.x = target_width

func _get_player_stat(stat_index: int, default_val: float) -> float:
	if player.has_method("get_stat"):
		return player.get_stat(stat_index)
	elif player.has_method("GetStat"):
		return player.GetStat(stat_index)
	return default_val

func _get_player_max_stat(stat_index: int, default_val: float) -> float:
	if player.has_method("get_max_stat"):
		return player.get_max_stat(stat_index)
	elif player.has_method("GetMaxStat"):
		return player.GetMaxStat(stat_index)
	return default_val
