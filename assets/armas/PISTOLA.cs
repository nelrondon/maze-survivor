using Godot;
using System;

public partial class PISTOLA : Node3D
{
	[Export] public PackedScene EscenaBala { get; set; } 
	[Export] public float CadenciaDisparo { get; set; } = 0.3f; 
	[Export] public int CapacidadCargador { get; set; } = 10; 
	[Export] public int BalasReserva { get; set; } = 20; 
	[Export] public float TiempoRecarga { get; set; } = 0.7f; 

	[Export] public float Damage { get; set; } = 1.0f;

	private Marker3D _puntaArma; 
	private AnimationPlayer _animador; 
	
	private AudioStreamPlayer3D _reproductorDisparo; 
	private AudioStreamPlayer3D _reproductorRecarga; 

	private Area3D _pickupArea;

	private int _balasActuales;
	private bool _puedeDisparar = true; 
	private bool _recargando = false; 

	private Node3D _portador = null;

	public override void _Ready()
	{
		_balasActuales = CapacidadCargador;
		
		_puntaArma = GetNodeOrNull<Marker3D>("Boca_canon");
		_animador = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
		_reproductorDisparo = GetNodeOrNull<AudioStreamPlayer3D>("SonidoDisparo");
		_reproductorRecarga = GetNodeOrNull<AudioStreamPlayer3D>("SonidoRecarga");

		if (_puntaArma == null)
		{
			GD.PushWarning("AVISO DE PRUEBA: No se encontró 'Boca_canon'. Las balas nacerán en el centro (0,0,0) del arma.");
		}

		_pickupArea = GetNodeOrNull<Area3D>("PickupArea");
		if (_pickupArea != null)
		{
			// _pickupArea.BodyEntered += OnPickupAreaBodyEntered;
			_puedeDisparar = false; 
		}

		if (_animador != null)
		{
			// _animador.AnimationFinished += OnAnimadorAnimationFinished;
		}

		// Sistema de acople automático para el personaje
		if (GetParent() != null && GetParent().Name == "Hand")
		{
			_puedeDisparar = true;
			Node nodoActual = GetParent();
			while (nodoActual != null)
			{
				if (nodoActual is Node3D node3D && node3D.IsInGroup("player"))
				{
					_portador = node3D;
					break;
				}
				nodoActual = nodoActual.GetParent();
			}

			if (_pickupArea != null && IsInstanceValid(_pickupArea))
			{
				_pickupArea.QueueFree(); 
			}
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Input.IsActionJustPressed("recargar") && !_recargando && _balasActuales < CapacidadCargador)
		{
			IniciarRecarga();
			return; 
		}

		if (Input.IsActionPressed("disparar") && _puedeDisparar && !_recargando)
		{
			IntentarDisparar();
		}
	}

	private void IntentarDisparar()
	{
		if (_balasActuales <= 0) 
		{
			IniciarRecarga();
			return; 
		}

		_balasActuales--;
		_puedeDisparar = false; 
		
		GetTree().CreateTimer(CadenciaDisparo).Timeout += () => 
		{
			_puedeDisparar = true;
		};
		
		if (EscenaBala != null)
		{
			Node nuevaBala = EscenaBala.Instantiate();          
			GetTree().Root.AddChild(nuevaBala);

			if (nuevaBala is Node3D bala3D)
			{
				bala3D.GlobalTransform = _puntaArma != null ? _puntaArma.GlobalTransform : GlobalTransform;
			}

			if (nuevaBala.HasMethod("SetDamage"))
			{
				nuevaBala.Set("damage", Damage);
			}
			else
			{
				nuevaBala.SetDeferred("damage", Damage);
			}
			
			nuevaBala.Set("portador", _portador);
		}

		if (_reproductorDisparo != null)
		{
			_reproductorDisparo.Play(); 
		}

		if (_animador != null && _animador.HasAnimation("recoil"))
		{
			_animador.Stop(); 
			_animador.Play("recoil"); 
		}
	}

	private void IniciarRecarga()
	{
		if (BalasReserva <= 0 || _recargando) return;

		_recargando = true;

		if (_reproductorRecarga != null)
		{
			_reproductorRecarga.Play();
		}

		if (_animador != null && _animador.HasAnimation("reload"))
		{
			_animador.Stop(); 
			_animador.Play("reload"); 
		}

		GetTree().CreateTimer(TiempoRecarga).Timeout += TerminarRecarga;
	}

	private void TerminarRecarga()
	{
		int balasNecesarias = CapacidadCargador - _balasActuales;
		int balasATransferir = Mathf.Min(balasNecesarias, BalasReserva);

		_balasActuales += balasATransferir;
		BalasReserva -= balasATransferir;

		_recargando = false;
	}

}
