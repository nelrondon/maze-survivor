class_name TrapBase extends Node3D

## Estructura de nodos esperada dentro de la escena de la trampa (hijos, opcionales
## según el activation_mode que uses):
##   - DetectionArea (Area3D + CollisionShape3D)  -> requerido para AREA_TRIGGER y
##                                                    PRESSURE_PLATE, y también se
##                                                    usa en TIMED_PATTERN para saber
##                                                    si el jugador está encima cuando
##                                                    la trampa se activa.
##   - PlateVisual (Node3D)                        -> opcional, solo si quieres que
##                                                    una placa se hunda visualmente
##                                                    en modo PRESSURE_PLATE.
##
## El jugador debe pertenecer al grupo "player" (body.add_to_group("player")) para
## que la trampa lo detecte.

signal trap_triggered(body: Node3D)
signal trap_activated
signal trap_deactivated

@export var data: TrapData

@export_group("Nodos (opcionales según activation_mode)")
@export var detection_area_path: NodePath = ^"DetectionArea"
@export var plate_visual_path: NodePath = NodePath("")
@export var plate_press_depth: float = 0.05
@export var plate_press_time: float = 0.1

@onready var _detection_area: Area3D = get_node_or_null(detection_area_path)
@onready var _plate_visual: Node3D = get_node_or_null(plate_visual_path) if plate_visual_path != NodePath("") else null

var _pattern_timer: Timer
var _plate_base_y: float = 0.0
var _plate_tween: Tween
var _is_active: bool = true
var _can_trigger: bool = true
var _bodies_inside: Array[Node3D] = []


func _ready() -> void:
	if data == null:
		push_error("TrapBase '%s': falta asignar TrapData en el inspector." % name)
		return

	if _detection_area:
		_detection_area.body_entered.connect(_on_body_entered)
		_detection_area.body_exited.connect(_on_body_exited)

	if _plate_visual:
		_plate_base_y = _plate_visual.position.y

	if data.activation_mode == TrapData.ActivationMode.TIMED_PATTERN:
		_is_active = false
		_setup_timed_pattern()


# ---------------------------------------------------------------------------
# Patrón de tiempo (trampas que se activan/desactivan solas en ciclos)
# ---------------------------------------------------------------------------

func _setup_timed_pattern() -> void:
	_pattern_timer = Timer.new()
	_pattern_timer.one_shot = true
	add_child(_pattern_timer)

	if data.start_delay > 0.0:
		_pattern_timer.wait_time = data.start_delay
		_pattern_timer.timeout.connect(_start_active_phase)
		_pattern_timer.start()
	else:
		_start_active_phase()


func _start_active_phase() -> void:
	_set_active(true)
	_pattern_timer.wait_time = maxf(data.active_time, 0.05)
	_pattern_timer.timeout.connect(_start_inactive_phase, CONNECT_ONE_SHOT)
	_pattern_timer.start()


func _start_inactive_phase() -> void:
	_set_active(false)
	_pattern_timer.wait_time = maxf(data.inactive_time, 0.05)
	_pattern_timer.timeout.connect(_start_active_phase, CONNECT_ONE_SHOT)
	_pattern_timer.start()


func _set_active(value: bool) -> void:
	_is_active = value
	if value:
		_on_activate()
		trap_activated.emit()
		# Si el jugador ya estaba parado sobre la trampa cuando se activa, la
		# dispara igual (por ejemplo pinchos que suben debajo de sus pies).
		for body in _bodies_inside:
			_try_trigger(body)
	else:
		_on_deactivate()
		trap_deactivated.emit()


# ---------------------------------------------------------------------------
# Detección
# ---------------------------------------------------------------------------

func _on_body_entered(body: Node3D) -> void:
	if not body.is_in_group("player"):
		return

	_bodies_inside.append(body)

	match data.activation_mode:
		TrapData.ActivationMode.AREA_TRIGGER:
			_try_trigger(body)
		TrapData.ActivationMode.PRESSURE_PLATE:
			_press_plate()
			_try_trigger(body)
		TrapData.ActivationMode.TIMED_PATTERN:
			if _is_active:
				_try_trigger(body)


func _on_body_exited(body: Node3D) -> void:
	_bodies_inside.erase(body)
	if data.activation_mode == TrapData.ActivationMode.PRESSURE_PLATE:
		_release_plate()
		_on_plate_released(body)


func _try_trigger(body: Node3D) -> void:
	if not _can_trigger:
		return
	_can_trigger = false

	trap_triggered.emit(body)
	_on_trigger(body)

	if data.cooldown > 0.0:
		get_tree().create_timer(data.cooldown).timeout.connect(func() -> void: _can_trigger = true)
	else:
		_can_trigger = true


# ---------------------------------------------------------------------------
# Aplica los Effect configurados en TrapData (mismo patrón que
# ConsumableComponent: daño, veneno, ralentizar, etc. se resuelven ahí).
# ---------------------------------------------------------------------------

func apply_effects(body: Node3D) -> void:
	for effect in data.effects:
		effect.apply(body)


# ---------------------------------------------------------------------------
# Animación de la placa de presión (opcional, genérico para cualquier trampa)
# ---------------------------------------------------------------------------

func _press_plate() -> void:
	if _plate_visual == null:
		return
	if _plate_tween:
		_plate_tween.kill()
	_plate_tween = create_tween()
	_plate_tween.tween_property(_plate_visual, "position:y", _plate_base_y - plate_press_depth, plate_press_time)


func _release_plate() -> void:
	if _plate_visual == null:
		return
	if _plate_tween:
		_plate_tween.kill()
	_plate_tween = create_tween()
	_plate_tween.tween_property(_plate_visual, "position:y", _plate_base_y, plate_press_time)


# ---------------------------------------------------------------------------
# Métodos virtuales: cada trampa concreta (SpikeTrap, ArrowTrap, CageTrap...)
# sobreescribe lo que necesite.
# ---------------------------------------------------------------------------

## Se llama cada vez que la trampa "dispara" contra un cuerpo (según cooldown).
func _on_trigger(_body: Node3D) -> void:
	pass

## Se llama cuando una trampa TIMED_PATTERN entra en su fase activa.
func _on_activate() -> void:
	pass

## Se llama cuando una trampa TIMED_PATTERN entra en su fase inactiva.
func _on_deactivate() -> void:
	pass

## Se llama cuando el jugador sale de una placa de presión.
func _on_plate_released(_body: Node3D) -> void:
	pass
