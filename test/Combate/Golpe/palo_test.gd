extends Node3D

#definimos el daño que hará el arma
#Esta variable aparece en el inspector de Godot y está como 5.0 su valor
@export var damage = 1.0

#Referencia directa al nodo hijo anim (AnimationPlayer)
@onready var anim = $anim
#Referencia del nodo que reproduce el sonido del golpe del arma
@onready var Madera_golpe_sonido = $Madera_golpe_sonido
#Referencia al nuevo nodo Area3D para recoger el arma del suelo
@onready var pickup_area = $PickupArea

#Para determinar si el jugador ataca o no
var can_attack = false

#Para almacenar una lista de enemigos que entran en el
#area de alcance del arma
var enemies_in_range = []

# Esta función de inicio conecta la señal del área de recogida automáticamente al empezar
func _ready() -> void:
	if pickup_area:
		pickup_area.body_entered.connect(_on_pickup_area_body_entered)

#Esta funcion se encarga de revisar constantemente si se quiere atacar
func _process(_delta: float) -> void:
	#Detecta si se presiona el boton de atacar o shoot (click izquierdo(
	if Input.is_action_pressed("shoot") and can_attack and not anim.is_playing():
		anim.play("Golpear")
		Madera_golpe_sonido.play()
		can_attack = false
		if not enemies_in_range.is_empty():
			for e in enemies_in_range:
				e.hit(damage)

#Estas son funciones para detectar la colision entre el arma y los personajes
func _on_hitbox_body_entered(body: Node3D) -> void:
	#Reemplazar "player" por el nombre del nodo real de los personajes, le puse ese nombre de forma provisional
	if body.is_in_group("player") and  not enemies_in_range.has(body):
		enemies_in_range.append(body)

func _on_hitbox_body_exited(body: Node3D) -> void:
	if enemies_in_range.has(body):
		enemies_in_range.erase(body)

# Esta función maneja la lógica cuando el jugador pasa por encima del arma para recogerla
func _on_pickup_area_body_entered(body: Node3D) -> void:
	# Comprobamos si es el jugador quien entra al área de recogida
	if body.is_in_group("player"):
		# Busca el nodo de la mano del jugador para acoplar el arma.
		# Quien programe el personaje del jugador solo debe cambiar "Mano" por el nombre real de su nodo de agarre.
		var mano_jugador = body.get_node_or_null("Mano")
		
		if mano_jugador:
			# Desengancha el arma de su posición en el suelo y la vuelve hija de la mano del jugador
			reparent(mano_jugador)
			
			# Resetea la posición y rotación relativas para que encaje perfectamente en la mano
			position = Vector3.ZERO
			rotation = Vector3.ZERO
			
			# Borra el área de recogida de la memoria ya que el arma ya ha sido equipada
			pickup_area.queue_free()
			
			# Habilita el estado para que el jugador ya pueda empezar a atacar con ella
			can_attack = true

#Esta es la funcion de control de estado, sirve para que el jugador pueda volver a atacar
func _on_anim_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Golpear":
		can_attack = true
	elif anim_name == "equipar":
		can_attack = true
	elif anim_name == "desequipar":
		visible = false
		can_attack = false
