using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Currently only used to handle the process of spawning players and asigning them their authority ID.
public partial class SceneManager : Node
{
	[Export]
	private PackedScene playerScene;

	private SpectatorUI _spectatorUI;
	private int _spectateIndex = 0;
	private Player _currentlySpectatedPlayer = null;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		Multiplayer.PeerDisconnected += OnPeerDisconnected;

		int index = 0;
		var spawnPoints = GetTree().GetNodesInGroup("PlayerSpawnPoints");
		foreach (var item in GameManager.Players.Where(p => !p.IsSpectator))
		{
			Player currentPlayer = playerScene.Instantiate<Player>();
			currentPlayer.Name = item.Id.ToString();
			currentPlayer.SetMultiplayerAuthority(item.Id);
			AddChild(currentPlayer);

			if (spawnPoints.Count > 0)
			{
				int targetIndex = index % spawnPoints.Count;
				foreach (Node3D spawnPoint in spawnPoints)
				{
					if (int.TryParse(spawnPoint.Name, out int spIndex) && spIndex == targetIndex)
					{
						currentPlayer.GlobalPosition = spawnPoint.GlobalPosition;
						break;
					}
				}
			}
			index++;
		}

		// Spectator setup for local peer if joining as spectator
		var localPlayerInfo = GameManager.Players.FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
		if (localPlayerInfo != null && localPlayerInfo.IsSpectator)
		{
			SetupSpectatorMode();
		}
	}

	private void SetupSpectatorMode()
	{
		var specUiScene = ResourceLoader.Load<PackedScene>("res://Multiplayer/SpectatorUI.tscn");
		if (specUiScene != null)
		{
			_spectatorUI = specUiScene.Instantiate<SpectatorUI>();
			AddChild(_spectatorUI);
			_spectatorUI.Connect(SpectatorUI.SignalName.CycleTarget, Callable.From<int>(OnCycleSpectateTarget));
		}

		Input.MouseMode = Input.MouseModeEnum.Visible;
		SetSpectatedTargetIndex(0);
	}

	private void OnCycleSpectateTarget(int direction)
	{
		SetSpectatedTargetIndex(_spectateIndex + direction);
	}

	public void SetSpectatedTargetIndex(int newIndex)
	{
		var activePlayers = GameManager.Players.Where(p => !p.IsSpectator).ToList();

		if (activePlayers.Count == 0)
		{
			if (_currentlySpectatedPlayer != null && IsInstanceValid(_currentlySpectatedPlayer))
			{
				_currentlySpectatedPlayer.SetMeshVisible(true);
				_currentlySpectatedPlayer = null;
			}
			if (_spectatorUI != null)
			{
				_spectatorUI.UpdateSpectateText("", 0);
			}
			return;
		}

		_spectateIndex = (newIndex % activePlayers.Count + activePlayers.Count) % activePlayers.Count;
		var targetInfo = activePlayers[_spectateIndex];
		var targetPlayerNode = GetNodeOrNull<Player>(targetInfo.Id.ToString());

		if (_currentlySpectatedPlayer != null && IsInstanceValid(_currentlySpectatedPlayer) && _currentlySpectatedPlayer != targetPlayerNode)
		{
			_currentlySpectatedPlayer.SetMeshVisible(true);
		}

		if (targetPlayerNode != null && IsInstanceValid(targetPlayerNode))
		{
			var cam = targetPlayerNode.GetCamera();
			if (cam != null)
			{
				cam.Current = true;
			}
			targetPlayerNode.SetMeshVisible(false);
			_currentlySpectatedPlayer = targetPlayerNode;

			if (_spectatorUI != null)
			{
				_spectatorUI.UpdateSpectateText(targetInfo.Name, targetInfo.Id);
			}
		}
	}

	public override void _ExitTree()
	{
		Multiplayer.PeerDisconnected -= OnPeerDisconnected;
	}

	private void OnPeerDisconnected(long id)
	{
		var playerNode = GetNodeOrNull(id.ToString());
		if (playerNode != null)
		{
			playerNode.QueueFree();
			GD.Print($"[SceneManager] Safely removed player node for disconnected peer {id}");
		}

		var localPlayerInfo = GameManager.Players.FirstOrDefault(p => p.Id == Multiplayer.GetUniqueId());
		if (localPlayerInfo != null && localPlayerInfo.IsSpectator)
		{
			CallDeferred(nameof(RefreshSpectatorTarget));
		}
	}

	private void RefreshSpectatorTarget()
	{
		SetSpectatedTargetIndex(_spectateIndex);
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
