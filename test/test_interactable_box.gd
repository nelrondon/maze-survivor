extends StaticBody3D

func interact(player):
	print("¡[INTERACTION] Objeto interactuado exitosamente por: ", player.name)

func hit(damage):
	print("¡[COMBAT] Objeto recibio golpe de arma! Danio: ", damage)
