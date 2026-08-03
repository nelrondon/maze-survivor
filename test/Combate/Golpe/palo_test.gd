extends Node3D

# Daño configurado para el ataque con el palo
@export var damage = 1.0

# Referencia opcional al AnimationPlayer propio (si existe)
@onready var anim = $anim if has_node("anim") else null
# Sonido ejecutado al conectar o realizar un golpe
@onready var Madera_golpe_sonido = $Madera_golpe_sonido
# Área de detección para la recolección en el escenario
@onready var pickup_area = $PickupArea

# Control de disponibilidad para volver a atacar
var can_attack = false

# Enemigos actualmente dentro de la zona de impacto
var enemies_in_range = []

func _ready() -> void:
	if pickup_area and not pickup_area.body_entered.is_connected(_on_pickup_area_body_entered):
		pickup_area.body_entered.connect(_on_pickup_area_body_entered)

# Procesa la solicitud de ataque si hay un AnimationPlayer local
func _process(_delta: float) -> void:
	if anim and Input.is_action_pressed("shoot") and can_attack and not anim.is_playing():
		anim.play("Golpear")
		if Madera_golpe_sonido:
			Madera_golpe_sonido.play()
		can_attack = false
		if not enemies_in_range.is_empty():
			for e in enemies_in_range:
				if e.has_method("hit"):
					e.hit(damage)

# Registra impactos ignorando al propio jugador que sostiene el arma
func _on_hitbox_body_entered(body: Node3D) -> void:
	var owner_player = get_parent()
	while owner_player and not owner_player.is_in_group("player"):
		owner_player = owner_player.get_parent()
	
	if body != owner_player and not enemies_in_range.has(body):
		enemies_in_range.append(body)

func _on_hitbox_body_exited(body: Node3D) -> void:
	if enemies_in_range.has(body):
		enemies_in_range.erase(body)

# Punto de entrada cuando el jugador presiona la tecla de interacción
func interact(user: Node3D) -> void:
	_pickup(user)

func _on_pickup_area_body_entered(body: Node3D) -> void:
	if body.is_in_group("player"):
		_pickup(body)

# Transfiere el arma al jugador y deshabilita temporalmente el área del suelo
func _pickup(body: Node3D) -> void:
	if body.has_method("EquipWeapon"):
		body.EquipWeapon(self)
	else:
		var mano_jugador = body.get_node_or_null("Mano")
		if mano_jugador:
			reparent(mano_jugador)
			position = Vector3.ZERO
			rotation = Vector3.ZERO

	if pickup_area:
		pickup_area.set_deferred("monitoring", false)
		pickup_area.set_deferred("monitorable", false)
	can_attack = true
	if anim and anim.has_animation("equipar"):
		anim.play("equipar")

# Reactiva la detección de recolección cuando el jugador suelta el arma
func on_drop() -> void:
	if pickup_area:
		pickup_area.set_deferred("monitoring", true)
		pickup_area.set_deferred("monitorable", true)

func _on_anim_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Golpear" or anim_name == "equipar":
		can_attack = true
	elif anim_name == "desequipar":
		visible = false
		can_attack = false
