using Godot;
using System;

public partial class Bala_3d : Area3D
{
	[Export] public float Velocidad { get; set; } = 20.0f; 
	[Export] public float TiempoVida { get; set; } = 1.5f; 

	public override void _Ready()
	{
		// Conecta el evento nativo usando la sintaxis correcta de C#
		BodyEntered += OnBodyEntered;

		// Temporizador directo para destruir la bala de forma segura
		GetTree().CreateTimer(TiempoVida).Timeout += () => 
		{
			if (IsInstanceValid(this) && !IsQueuedForDeletion()) 
			{
				QueueFree(); 
			}
		};
	}

	public override void _PhysicsProcess(double delta)
	{
		// CORRECCIÓN CRÍTICA: Se eliminó GlobalTranslate para evitar que la bala ignore la rotación
		// Avanza hacia el frente local (Z negativo) transformado al espacio global
		Position += -Transform.Basis.Z * Velocidad * (float)delta;
	}

	private void OnBodyEntered(Node3D body)
	{
		// AJUSTE DE SEGURIDAD: Evita que la bala choque con el jugador o el rifle que la disparó
		if (body is CharacterBody3D || body.Name == "Player" || body is RIFLE)
		{
			return; 
		}

		// Aquí puedes aplicar daño si el cuerpo tiene un método para recibirlo:
		// if (body.HasMethod("RecibirDanio")) body.Call("RecibirDanio", 25);

		QueueFree(); 
	}
}
