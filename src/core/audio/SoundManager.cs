using Godot;
using System;

public partial class SoundManager : Node
{
	public static SoundManager Instance { get; private set; }

	[Export] public AudioStream MenuMusic { get; set; }
	[Export] public AudioStream GameplayMusic { get; set; }
	[Export] public float FadeDuration { get; set; } = 2.0f;
	[Export] public float TargetVolumeDb { get; set; } = 0.0f;

	private AudioStreamPlayer _playerA;
	private AudioStreamPlayer _playerB;
	private AudioStreamPlayer _activePlayer;
	private AudioStreamPlayer _inactivePlayer;

	private Tween _fadeTween;
	private AudioStream _currentStream;

	public override void _Ready()
	{
		if (Instance != null && Instance != this)
		{
			QueueFree();
			return;
		}

		Instance = this;
		ProcessMode = ProcessModeEnum.Always;

		_playerA = new AudioStreamPlayer { Name = "MusicA", Bus = "Master", VolumeDb = -80f };
		_playerB = new AudioStreamPlayer { Name = "MusicB", Bus = "Master", VolumeDb = -80f };
		AddChild(_playerA);
		AddChild(_playerB);

		_activePlayer = _playerA;
		_inactivePlayer = _playerB;

		if (MenuMusic == null && ResourceLoader.Exists("res://assets/audio/music/mz_menu_theme.mp3"))
		{
			MenuMusic = ResourceLoader.Load<AudioStream>("res://assets/audio/music/mz_menu_theme.mp3");
		}

		if (GameplayMusic == null && ResourceLoader.Exists("res://assets/audio/music/mz_game_theme.mp3"))
		{
			GameplayMusic = ResourceLoader.Load<AudioStream>("res://assets/audio/music/mz_game_theme.mp3");
		}

		GetTree().NodeAdded += OnTreeChanged;
		GetTree().NodeRemoved += OnTreeChanged;

		CallDeferred(nameof(CheckSceneAndPlayMusic));
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			GetTree().NodeAdded -= OnTreeChanged;
			GetTree().NodeRemoved -= OnTreeChanged;
			Instance = null;
		}
	}

	private void OnTreeChanged(Node node)
	{
		if (node != null && node.GetParent() == GetTree().Root && node != this)
		{
			CallDeferred(nameof(CheckSceneAndPlayMusic));
		}
	}

	private void CheckSceneAndPlayMusic()
	{
		var root = GetTree().Root;
		bool isGameplayActive = false;

		foreach (Node child in root.GetChildren())
		{
			if (child == this) continue;

			string name = child.Name.ToString().ToLower();
			string path = child.SceneFilePath != null ? child.SceneFilePath.ToLower() : "";

			if (name.Contains("activegamescene") || name.Contains("maze") || 
				path.Contains("maze.tscn") || path.Contains("scenemanager.tscn"))
			{
				isGameplayActive = true;
				break;
			}
		}

		if (isGameplayActive)
		{
			PlayMusic(GameplayMusic);
		}
		else
		{
			PlayMusic(MenuMusic);
		}
	}

	public void PlayMusic(AudioStream stream, float customFadeDuration = -1f)
	{
		if (stream == null) return;
		if (_currentStream == stream && _activePlayer.Playing) return;

		_currentStream = stream;
		float duration = customFadeDuration > 0 ? customFadeDuration : FadeDuration;

		if (_fadeTween != null && _fadeTween.IsValid())
		{
			_fadeTween.Kill();
		}

		_fadeTween = CreateTween();
		_fadeTween.SetParallel(true);

		if (!_activePlayer.Playing)
		{
			_activePlayer.Stream = stream;
			_activePlayer.VolumeDb = -80f;
			_activePlayer.Play();

			_fadeTween.TweenProperty(_activePlayer, "volume_db", TargetVolumeDb, duration)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);
		}
		else
		{
			_inactivePlayer.Stream = stream;
			_inactivePlayer.VolumeDb = -80f;
			_inactivePlayer.Play();

			_fadeTween.TweenProperty(_activePlayer, "volume_db", -80f, duration)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.In);

			_fadeTween.TweenProperty(_inactivePlayer, "volume_db", TargetVolumeDb, duration)
				.SetTrans(Tween.TransitionType.Sine)
				.SetEase(Tween.EaseType.Out);

			var oldActive = _activePlayer;
			_activePlayer = _inactivePlayer;
			_inactivePlayer = oldActive;

			_fadeTween.Chain().TweenCallback(Callable.From(() =>
			{
				oldActive.Stop();
			}));
		}
	}

	public void StopMusic(float customFadeDuration = -1f)
	{
		float duration = customFadeDuration > 0 ? customFadeDuration : FadeDuration;

		if (_fadeTween != null && _fadeTween.IsValid())
		{
			_fadeTween.Kill();
		}

		_fadeTween = CreateTween();
		_fadeTween.SetParallel(true);

		if (_activePlayer.Playing)
		{
			_fadeTween.TweenProperty(_activePlayer, "volume_db", -80f, duration);
			_fadeTween.Chain().TweenCallback(Callable.From(() => _activePlayer.Stop()));
		}

		if (_inactivePlayer.Playing)
		{
			_fadeTween.TweenProperty(_inactivePlayer, "volume_db", -80f, duration);
			_fadeTween.Chain().TweenCallback(Callable.From(() => _inactivePlayer.Stop()));
		}

		_currentStream = null;
	}
}
