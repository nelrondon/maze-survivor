extends SceneTree

func _init():
	var pistol = load("res://assets/armas/Pistola_mejora.tscn").instantiate()
	var rifle = load("res://assets/RIFLE/rifle.tscn").instantiate()
	
	print("Pistol scale: ", pistol.scale, " inner scale: ", pistol.get_node("tokarev").scale)
	print("Rifle scale: ", rifle.scale, " inner scale: ", rifle.get_node("tokarev").scale)
	
	quit()
