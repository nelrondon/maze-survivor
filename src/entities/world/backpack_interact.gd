extends Area3D
## Zona de interacción de la mochila.
## El RayCast del Player detecta el CollisionShape del Area3D.
## Reenvía interact() al WorldBackpack padre.

func interact(player: Node) -> void:
	var backpack: WorldBackpack = get_parent() as WorldBackpack
	if backpack != null:
		backpack.interact(player)
