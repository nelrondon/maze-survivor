extends Node3D

# -------------------------
# CONFIGURACIÓN DEL ARMA
# -------------------------
@export var escena_bala: PackedScene
@export var cadencia_disparo: float = 0.75
@export var capacidad_cargador: int = 10
@export var balas_reserva: int = 15
@export var tiempo_recarga: float = 1.0
@export var damage: float = 1.0

# -------------------------
# NODOS
# -------------------------
var _punta_arma: Marker3D
var _animador: AnimationPlayer
var _reproductor_disparo: AudioStreamPlayer3D
var _reproductor_recarga: AudioStreamPlayer3D

var _pickup_area: Area3D
var _hitbox_area: Area3D

# -------------------------
# ESTADOS
# -------------------------
var _balas_actuales: int
var _puede_disparar: bool = true
var _recargando: bool = false

var _can_attack: bool = false # para melee

var _portador: Node3D = null

var _enemigos_en_rango: Array[Node3D] = []

# -------------------------
# READY
# -------------------------
func _ready() -> void:
	_balas_actuales = capacidad_cargador

	_punta_arma = get_node_or_null("Boca_canon")
	_animador = get_node_or_null("AnimationPlayer")
	_reproductor_disparo = get_node_or_null("SonidoDisparo")
	_reproductor_recarga = get_node_or_null("SonidoRecarga")

	_pickup_area = get_node_or_null("PickupArea")
	_hitbox_area = get_node_or_null("Hitbox")

	if _pickup_area != null:
		_pickup_area.body_entered.connect(_on_pickup_area_body_entered)

	if _hitbox_area != null:
		_hitbox_area.body_entered.connect(_on_hitbox_body_entered)
		_hitbox_area.body_exited.connect(_on_hitbox_body_exited)

	if _animador != null:
		_animador.animation_finished.connect(_on_animador_animation_finished)

	# Auto-equip si ya está en la mano
	if get_parent() != null and get_parent().name == "Hand":
		_puede_disparar = true
		_can_attack = true

		var nodo_actual = get_parent()
		while nodo_actual != null:
			if nodo_actual is Node3D and nodo_actual.is_in_group("player"):
				_portador = nodo_actual
				break
			nodo_actual = nodo_actual.get_parent()

		if _pickup_area != null:
			_pickup_area.queue_free()

# -------------------------
# PROCESS
# -------------------------
func _physics_process(_delta: float) -> void:
	# 🔥 CORRECCIÓN: Si el arma está en el suelo, ignora por completo los controles
	if _portador == null: 
		return

	# Recargar
	if Input.is_action_just_pressed("recargar") and not _recargando and _balas_actuales < capacidad_cargador:
		_iniciar_recarga()
		return

	# Disparar
	if Input.is_action_pressed("disparar") and _puede_disparar and not _recargando:
		_intentar_disparar()

	# Golpe melee (opcional)
	if Input.is_action_pressed("shoot") and _can_attack and _animador != null and not _animador.is_playing():
		_animador.play("Golpear")
		_can_attack = false

		for enemigo in _enemigos_en_rango:
			if enemigo.has_method("hit"):
				enemigo.call("hit", damage)

# -------------------------
# DISPARAR
# -------------------------
func _intentar_disparar() -> void:
	if _balas_actuales <= 0:
		_iniciar_recarga()
		return

	_balas_actuales -= 1
	_puede_disparar = false

	get_tree().create_timer(cadencia_disparo).timeout.connect(func(): _puede_disparar = true)

	if escena_bala != null:
		var nueva_bala = escena_bala.instantiate()
		get_tree().root.add_child(nueva_bala)

		if nueva_bala is Node3D:
			nueva_bala.global_transform = _punta_arma.global_transform if _punta_arma != null else global_transform

		nueva_bala.set("damage", damage)
		nueva_bala.set("portador", _portador)

	if _reproductor_disparo != null:
		_reproductor_disparo.play()

	if _animador != null and _animador.has_animation("recoil2"):
		_animador.stop()
		_animador.play("recoil2")

# -------------------------
# RECARGA
# -------------------------
func _iniciar_recarga() -> void:
	if balas_reserva <= 0 or _recargando: 
		return

	_recargando = true

	if _reproductor_recarga != null:
		_reproductor_recarga.play()

	if _animador != null and _animador.has_animation("reload2"):
		_animador.stop()
		_animador.play("reload2")

	get_tree().create_timer(tiempo_recarga).timeout.connect(_terminar_recarga)

func _terminar_recarga() -> void:
	var balas_necesarias = capacidad_cargador - _balas_actuales
	var balas_a_transferir = min(balas_necesarias, balas_reserva)

	_balas_actuales += balas_a_transferir
	balas_reserva -= balas_a_transferir

	_recargando = false

# -------------------------
# HITBOX
# -------------------------
func _on_hitbox_body_entered(body: Node3D) -> void:
	if body.is_in_group("player") and body != _portador and not _enemigos_en_rango.has(body):
		_enemigos_en_rango.append(body)

func _on_hitbox_body_exited(body: Node3D) -> void:
	if _enemigos_en_rango.has(body):
		_enemigos_en_rango.erase(body)

# -------------------------
# PICKUP
# -------------------------
func _on_pickup_area_body_entered(body: Node3D) -> void:
	if not body.is_in_group("player"): 
		return

	var mano_jugador = body.get_node_or_null("Head/Camera3D/Hand")

	if mano_jugador != null:
		_portador = body

		reparent(mano_jugador)

		position = Vector3.ZERO
		rotation = Vector3.ZERO
		scale = Vector3.ONE # Asegura que mantenga su tamaño real al equiparse

		if _pickup_area != null:
			_pickup_area.queue_free()

		_puede_disparar = true
		_can_attack = true

# -------------------------
# ANIMACIONES
# -------------------------
func _on_animador_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Golpear":
		_can_attack = true
	elif anim_name == "Equipar":
		_can_attack = true
	elif anim_name == "Desequipar":
		visible = false
		_can_attack = false
