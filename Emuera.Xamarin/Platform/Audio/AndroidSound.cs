using System;
using Android.Media;
using Android.OS;

namespace MinorShift.Emuera.Runtime.Utils
{
	/// <summary>
	/// Android 音频播放实现，基于 Android.Media.MediaPlayer。
	/// 支持 play/stop/pause/resume/setVolume/setSpeed/GetTotalTime/GetCurrentTime。
	/// 变速使用 PlaybackParams（Android 6+），preservePitch 通过 Pitch 参数控制。
	/// 使用 PrepareAsync 异步准备，避免阻塞 UI 线程。
	/// </summary>
	internal class AndroidSound : Sound
	{
		private MediaPlayer _player;
		private float _volume = 1.0f;
		private float _speed = 1.0f;
		private bool _preservePitch = true;
		private bool _paused;
		private int _repeatCount;
		private int _remainingRepeats;
		private bool _looping;
		private string _currentFile;

		public AndroidSound()
		{
			_player = new MediaPlayer();
			_player.Completion += OnCompletion;
		}

		private void OnCompletion(object sender, EventArgs e)
		{
			if (_looping)
			{
				// Looping=true 由 MediaPlayer 内部处理，无需手动重播
				return;
			}

			if (_remainingRepeats > 1)
			{
				_remainingRepeats--;
				// 手动重播：SeekTo(0) + Start()
				try
				{
					_player.SeekTo(0);
					_player.Start();
				}
				catch
				{
					Playing = false;
					_remainingRepeats = 0;
				}
				return;
			}

			Playing = false;
			_remainingRepeats = 0;
		}

		private void OnPrepared(object sender, EventArgs e)
		{
			_player.Prepared -= OnPrepared;

			// 应用音量和变速
			ApplyVolume();
			ApplySpeed();

			_player.Start();
			Playing = true;
		}

		public override void play(string filename, int repeat = 1)
		{
			stop();

			try
			{
				_player.Reset();
				_player.SetDataSource(filename);
			}
			catch
			{
				Playing = false;
				return;
			}

			_currentFile = filename;
			_repeatCount = repeat;
			_remainingRepeats = repeat;
			_paused = false;

			// 循环模式
			if (repeat == -1)
			{
				_looping = true;
				_player.Looping = true;
			}
			else
			{
				_looping = false;
				_player.Looping = false;
			}

			// 异步准备，不阻塞 UI 线程
			_player.Prepared += OnPrepared;
			_player.PrepareAsync();
		}

		public override void stop()
		{
			if (_player != null)
			{
				try
				{
					_player.Prepared -= OnPrepared;
					if (_player.IsPlaying)
						_player.Stop();
				}
				catch { }
			}
			_paused = false;
			_looping = false;
			_remainingRepeats = 0;
			Playing = false;
		}

		public override void pause()
		{
			if (_player != null && Playing && !_paused)
			{
				_player.Pause();
				_paused = true;
			}
		}

		public override void resume()
		{
			if (_player != null && _paused)
			{
				_player.Start();
				_paused = false;
			}
		}

		public override void close()
		{
			stop();
			if (_player != null)
			{
				_player.Completion -= OnCompletion;
				_player.Prepared -= OnPrepared;
				_player.Release();
				_player = null;
			}
		}

		public override bool isPlaying()
		{
			return Playing && !_paused;
		}

		public override void setVolume(int volume)
		{
			_volume = Math.Clamp(volume, 0, 100) / 100.0f;
			ApplyVolume();
		}

		public override int getVolume()
		{
			return (int)(_volume * 100);
		}

		public override void setSpeed(float speed)
		{
			_speed = Math.Clamp(speed, 0.1f, 10.0f);
			ApplySpeed();
		}

		public override double getSpeed()
		{
			return _speed;
		}

		public override double GetTotalTime()
		{
			if (_player == null) return 0;
			try
			{
				return _player.Duration / 1000.0;
			}
			catch
			{
				return 0;
			}
		}

		public override double GetCurrentTime()
		{
			if (_player == null) return 0;
			try
			{
				return _player.CurrentPosition / 1000.0;
			}
			catch
			{
				return 0;
			}
		}

		public override void SetPreservePitch(bool preserve)
		{
			_preservePitch = preserve;
			ApplySpeed();
		}

		private void ApplyVolume()
		{
			if (_player == null) return;
			try
			{
				_player.SetVolume(_volume, _volume);
			}
			catch { }
		}

		private void ApplySpeed()
		{
			if (_player == null || !_player.IsPlaying) return;

			// PlaybackParams 需要 Android 6.0+ (API 23)
			if (Build.VERSION.SdkInt >= BuildVersionCodes.M)
			{
				try
				{
					var params_ = _player.PlaybackParams ?? new PlaybackParams();
					params_.Speed = _speed;
					if (_preservePitch)
					{
						// 保持音调：设置 pitch 为 1.0（正常音调），速度由 Speed 控制
						params_.Pitch = 1.0f;
					}
					else
					{
						// 不保持音调：pitch 随速度变化
						params_.Pitch = _speed;
					}
					_player.PlaybackParams = params_;
				}
				catch
				{
					// 某些设备不支持 PlaybackParams，静默失败
				}
			}
		}
	}
}
