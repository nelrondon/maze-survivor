extends Node3D
#definimos el daño que hará el arma
#Esta variable aparece en el inspector de Godot y está como 5.0 su valor
@export var damage = 1.0

#Referencia directa al nodo hijo anim (AnimationPlayer)
@onready var anim = $anim
#Referencia del nodo que reproduce el sonido del golpe del arma
@onready var Madera_golpe_sonido = $Madera_golpe_sonido

#Para determinar si el jugador ataca o no
var can_attack = false

#Para almacenar una lista de enemigos que entran en el
#area de alcance del arma
var enemies_in_range = []

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

#Esta es la funcion de control de estado, sirve para que el jugador pueda volver a atacar
func _on_anim_animation_finished(anim_name: StringName) -> void:
	if anim_name == "Golpear":
		can_attack = true
	elif anim_name == "equipar":
		can_attack = true
	elif anim_name == "desequipar":
		visible = false
		can_attack = false
