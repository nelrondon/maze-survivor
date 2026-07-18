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
		# Intentar buscar al jugador en el grupo 'player' si no se asignó en el inspector
		var players = get_tree().get_nodes_in_group("player")
		if players.size() > 0:
			player = players[0]
			
	if player != null:
		setup_player(player)

func setup_player(p_player: Node) -> void:
	player = p_player
	if player.has_signal("stats_changed"):
		player.connect("stats_changed", _on_stats_changed)
	update_bars(false) # Actualización inicial sin animación

func _on_stats_changed() -> void:
	update_bars(true)

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
