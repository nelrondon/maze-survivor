using Godot;
using System.Collections.Generic;

public partial class Map : Control
{
	[Export] public Vector2 TabletSize = new Vector2(320, 320);
	[Export] public float BorderThickness = 30.0f;
	[Export] public Color WallColor = new Color(0.15f, 0.15f, 0.2f);
	[Export] public Color PathColor = new Color(0.85f, 0.85f, 0.9f);
	[Export] public Color KeyHolderColor = new Color(1.0f, 0.84f, 0.0f); // Dorado para quien lleva la llave
	[Export] public Color LocalPlayerColor = new Color(1.0f, 0.1f, 0.1f); // Rojo para el jugador local
	[Export] public Color UnexploredColor = new Color(0.02f, 0.02f, 0.05f);
	[Export] public Color DoorMarkerColor = new Color(0.1f, 0.9f, 0.2f); // Verde para la puerta

	private byte[,] _mazeData;
	private bool[,] _exploredData;
	private int _gridWidth;
	private int _gridHeight;
	private float _gridScale;
	
	private Node3D _localPlayer;
	private List<Node3D> _allPlayers = new List<Node3D>();
	private Node3D _doorNode;
	private Vector2I _lastPlayerGridPos = new Vector2I(-1, -1);
	
	private float _blinkTimer = 0f;
	private bool _showRecDot = true;

	public override void _Ready()
	{
		CustomMinimumSize = TabletSize;
		SetAnchorsPreset(LayoutPreset.Center);
		PivotOffset = TabletSize / 2.0f;
		Position = (GetViewportRect().Size - TabletSize) / 2.0f;
		Visible = false;
	}

	public void InitializeMapData(byte[,] mazeData, float gridScale)
	{
		_mazeData = mazeData;
		_gridWidth = mazeData.GetLength(0);
		_gridHeight = mazeData.GetLength(1);
		_gridScale = gridScale;
		
		_exploredData = new bool[_gridWidth, _gridHeight];
		QueueRedraw();
	}

	public void SetLocalPlayer(Node3D player)
	{
		_localPlayer = player;
	}

	public void UpdatePlayersList(List<Node3D> players)
	{
		_allPlayers = players;
	}

	public override void _Process(double delta)
	{
		_blinkTimer += (float)delta;
		if (_blinkTimer >= 0.5f)
		{
			_blinkTimer = 0f;
			_showRecDot = !_showRecDot;
			if (Visible) QueueRedraw();
		}

		if (_localPlayer == null || _mazeData == null) return;

		int playerGridX = Mathf.RoundToInt(_localPlayer.GlobalPosition.X / _gridScale);
		int playerGridZ = Mathf.RoundToInt(_localPlayer.GlobalPosition.Z / _gridScale);
		Vector2I currentGridPos = new Vector2I(playerGridX, playerGridZ);

		if (currentGridPos != _lastPlayerGridPos)
		{
			_lastPlayerGridPos = currentGridPos;
			
			if (playerGridX >= 0 && playerGridX < _gridWidth && playerGridZ >= 0 && playerGridZ < _gridHeight)
			{
				_exploredData[playerGridX, playerGridZ] = true;
			}
			
			if (Visible) QueueRedraw();
		}
	}

	public override void _Draw()
	{
		// 1. Marco metálico
		Rect2 outerRect = new Rect2(Vector2.Zero, TabletSize);
		DrawRect(outerRect, new Color(0.7f, 0.72f, 0.75f));
		
		Rect2 innerBezel = new Rect2(new Vector2(4, 4), TabletSize - new Vector2(8, 8));
		DrawRect(innerBezel, new Color(0.35f, 0.37f, 0.4f));

		// 2. Pantalla interna
		Vector2 screenPos = new Vector2(BorderThickness, BorderThickness);
		Vector2 screenSize = TabletSize - (screenPos * 2.0f);
		Rect2 screenRect = new Rect2(screenPos, screenSize);
		
		DrawRect(screenRect, UnexploredColor);

		// 3. Punto "REC"
		if (_showRecDot)
		{
			Vector2 recDotPos = new Vector2(TabletSize.X / 2.0f, BorderThickness / 2.0f);
			DrawCircle(recDotPos, 5.0f, new Color(1f, 0.1f, 0.1f));
		}

		if (_mazeData == null || _localPlayer == null) return;

		float cellWidth = screenRect.Size.X / _gridWidth;
		float cellHeight = screenRect.Size.Y / _gridHeight;

		// 4. Dibujar celdas exploradas
		for (int x = 0; x < _gridWidth; x++)
		{
			for (int z = 0; z < _gridHeight; z++)
			{
				if (_exploredData[x, z])
				{
					Vector2 cellPos = screenRect.Position + new Vector2(x * cellWidth, z * cellHeight);
					Rect2 cellRect = new Rect2(cellPos, new Vector2(cellWidth + 0.4f, cellHeight + 0.4f));
					
					Color colorToDraw = (_mazeData[x, z] == 1) ? WallColor : PathColor;
					DrawRect(cellRect, colorToDraw);
				}
			}
		}

		// Verificar si el jugador local actual tiene la llave
		bool localPlayerHasKey = false;
		if (IsInstanceValid(_localPlayer))
		{
			var keyProp = _localPlayer.Get("HasKey");
			if (keyProp.VariantType != Variant.Type.Nil && (bool)keyProp)
			{
				localPlayerHasKey = true;
			}
		}

		// 5. MARCADOR DE LA PUERTA (ÚNICAMENTE se muestra si el jugador LOCAL tiene la llave)
		if (localPlayerHasKey)
		{
			if (_doorNode == null || !IsInstanceValid(_doorNode))
			{
				_doorNode = GetTree().Root.FindChild("Door", true, false) as Node3D 
						 ?? GetTree().Root.FindChild("DoorScene", true, false) as Node3D;
			}

			if (_doorNode != null && IsInstanceValid(_doorNode))
			{
				float doorScreenX = screenRect.Position.X + ((_doorNode.GlobalPosition.X / _gridScale) * cellWidth);
				float doorScreenY = screenRect.Position.Y + ((_doorNode.GlobalPosition.Z / _gridScale) * cellHeight);

				Vector2 doorPosOnScreen = new Vector2(doorScreenX, doorScreenY);
				doorPosOnScreen.X = Mathf.Clamp(doorPosOnScreen.X, screenRect.Position.X, screenRect.End.X);
				doorPosOnScreen.Y = Mathf.Clamp(doorPosOnScreen.Y, screenRect.Position.Y, screenRect.End.Y);

				float doorRadius = Mathf.Max(3.5f, cellWidth * 1.1f);

				if (_showRecDot)
				{
					DrawCircle(doorPosOnScreen, doorRadius + 1.5f, Colors.White);
				}
				DrawCircle(doorPosOnScreen, doorRadius, DoorMarkerColor);
			}
		}

		// 6. MOSTRAR JUGADORES (Tu jugador local siempre en ROJO, y quien tenga la llave en DORADO)
		foreach (var player in _allPlayers)
		{
			if (!IsInstanceValid(player)) continue;

			bool isLocal = (player == _localPlayer);
			
			bool hasKey = false;
			var keyProperty = player.Get("HasKey");
			if (keyProperty.VariantType != Variant.Type.Nil)
			{
				hasKey = (bool)keyProperty;
			}

			Color playerColor;
			if (isLocal)
			{
				playerColor = LocalPlayerColor; // Siempre rojo para ti
			}
			else if (hasKey)
			{
				playerColor = KeyHolderColor; // Dorado para aliados con la llave
			}
			else
			{
				continue; 
			}

			float pScreenX = screenRect.Position.X + ((player.GlobalPosition.X / _gridScale) * cellWidth);
			float pScreenY = screenRect.Position.Y + ((player.GlobalPosition.Z / _gridScale) * cellHeight);
			
			Vector2 pPosOnScreen = new Vector2(pScreenX, pScreenY);
			pPosOnScreen.X = Mathf.Clamp(pPosOnScreen.X, screenRect.Position.X, screenRect.End.X);
			pPosOnScreen.Y = Mathf.Clamp(pPosOnScreen.Y, screenRect.Position.Y, screenRect.End.Y);

			float pRadius = Mathf.Max(3.0f, cellWidth * 0.9f);

			DrawCircle(pPosOnScreen, pRadius, playerColor);
		}
	}
}
