#if UNITY_5_6_OR_NEWER
#if UNITY_2018_3_OR_NEWER		// The "length" property is only supported from 2018.3
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Controls VideoPlayer updates time during offline captures
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Utils/VideoPlayer Controller", 300)]
	public class VideoPlayerController : MonoBehaviour
	{
		public enum ScanFrequencyMode
		{
			SceneLoad,
			Frame,
		}

		[SerializeField] ScanFrequencyMode _scanFrequency = ScanFrequencyMode.SceneLoad;

		public ScanFrequencyMode ScanFrequency
		{
			get { return _scanFrequency; }
			set { _scanFrequency = value; ResetSceneLoading(); }
		}

		internal class VideoPlayerInstance
		{
			private VideoPlayer	_videoPlayer	= null;
			private bool _isCapturing			= false;
			private bool _isControlling			= false;
			private bool _isSeekPending 		= false;
			private double _videoTime			= 0.0;
			private float _postSeekTimer		= 0f;
			internal VideoPlayerInstance(VideoPlayer videoPlayer)
			{
				_videoPlayer = videoPlayer;
			}

			internal bool Is(VideoPlayer videoPlayer)
			{
				return (_videoPlayer == videoPlayer);
			}

			internal void StartCapture()
			{
				// First capture to touch the playable directors
				if (!_isCapturing)
				{
					// Null check in case director no longer exists
					if (_videoPlayer != null)
					{
						TryTakeControl();
					}
					_isCapturing = true;
				}
			}

			internal bool IsSeekPending()
			{
				float d = (Time.realtimeSinceStartup - _postSeekTimer);
				return (_isSeekPending || (d < 0.2f));
			}

			internal void TryTakeControl()
			{
				if (!_isControlling)
				{
					if (_videoPlayer.isPrepared)
					{
						if (_videoPlayer.isPlaying && _videoPlayer.frame >= 0)
						{
							_videoPlayer.seekCompleted += VideoSeekCompleted;
							_videoPlayer.frameReady += VideoFrameReady;
							_videoPlayer.sendFrameReadyEvents = true;
							_videoPlayer.Pause();
							_isControlling = true;
							_videoTime = _videoPlayer.time;
							//Debug.Log("pause");
							//Debug.Log(_videoPlayer.canSetSkipOnDrop + " " + _videoPlayer.skipOnDrop);
							_videoPlayer.skipOnDrop = true;
							_postSeekTimer = Time.realtimeSinceStartup - 2f;
							Debug.Log("start " + _videoPlayer.frame + " " + _videoTime + " " + (_videoPlayer.time * 1000));
						}
					}
				}
			}

			void VideoFrameReady(VideoPlayer source, long frameIdx)
			{
				Debug.Log("frame " + frameIdx);
				_postSeekTimer = Time.realtimeSinceStartup - 2f;
				_isSeekPending = false;
			}

			void VideoSeekCompleted(VideoPlayer source)
			{
				Debug.Log("seek complete " + source.frame + " " + source.time * 1000);
				_isSeekPending = false;
				_postSeekTimer = Time.realtimeSinceStartup;
			}

			internal void ReleaseControl()
			{
				_isControlling = false;
				_isSeekPending = false;
				_videoPlayer.seekCompleted -= VideoSeekCompleted;
			}

			internal bool Update(float deltaTime)
			{
				bool updated = false;
				if (_isCapturing)
				{
					if (_videoPlayer != null)
					{
						if (!_isControlling)
						{
							TryTakeControl();
						}
						if (_isControlling)
						{
							if (!_videoPlayer.isPrepared)
							{
								ReleaseControl();
							}
							if (_isControlling && !_isSeekPending)
							{
								float delta = Time.realtimeSinceStartup - _postSeekTimer;
								//Debug.Log("post " + _postSeekTimer);
								if (delta > 0.2f)
								{
									_videoTime += deltaTime;
									if (_videoPlayer.isLooping && _videoTime >= _videoPlayer.length)
									{
										_videoTime %= _videoPlayer.length;
									}

									_isSeekPending = true;
									Debug.Log("seek begin " + _videoPlayer.frame + " " + _videoTime + " " + (_videoPlayer.time * 1000) + " " + (deltaTime * 1000));
									_videoPlayer.time = _videoTime;
									//_videoPlayer.frame = 18;

									//Debug.Log("seek begin2 " + _videoPlayer.frame + " " + (_videoPlayer.time * 1000));
									updated = true;
									//_isSeekPending = false;
								}
							}
						}
					}
				}
				return updated;
			}

			internal void StopCapture()
			{
				if (_isCapturing)
				{
					// TODO: what happens to the VideoPlayer when the scene is unloaded?
					if (_videoPlayer != null)
					{
						// We were controlling?
						if (_isControlling)
						{
							// Restore to original state
							_videoPlayer.Play();
						}
					}
					ReleaseControl();
					_isCapturing = false;
				}
			}
		}

		private List<VideoPlayerInstance>	_instances	= new List<VideoPlayerInstance>(8);

		void Awake()
		{
			ResetSceneLoading();
		}

		void Start()
		{
			//StartCapture();
		}

		void OnValidate()
		{
			ResetSceneLoading();
		}

		void Update()
		{
			//UpdateFrame();
		}

		internal void UpdateFrame()
		{
			if (!this.isActiveAndEnabled)
				return;


			if (_scanFrequency == ScanFrequencyMode.Frame)
			{
				ScanForVideoPlayers();
			}




			bool anyUpdates = false;
			foreach (VideoPlayerInstance instance in _instances)
			{
				//if (Input.GetKeyDown(KeyCode.P))
				{
					if (instance.Update(Time.deltaTime))
					{
						anyUpdates = true;
					}
				}
			}

			if (anyUpdates)
			{
				//StartCoroutine(WaitforSeekCompletes());
				//WaitforSeekCompletes2();
				//System.Threading.Thread.Sleep(500);
			}
		}

		public bool CanContinue()
		{
			bool result = true;
			foreach (VideoPlayerInstance instance in _instances)
			{
				if (instance.IsSeekPending())
				{
					result = false;
					break;
				}
			}
			return result;
		}

		internal IEnumerator WaitforSeekCompletes()
		{
			yield return new WaitUntil(() =>
			{
				bool isSeekPending = false;
				foreach (VideoPlayerInstance instance in _instances)
				{
					if (instance.IsSeekPending())
					{
						isSeekPending = true;
						break;
					}
				}
				return !isSeekPending;
			});

			System.Threading.Thread.Sleep(100);
			yield return new WaitForEndOfFrame();
		}
		internal void WaitforSeekCompletes2()
		{
			/*bool isSeekPending = false;
			foreach (VideoPlayerInstance instance in _instances)
			{
				if (instance.IsSeekPending())
				{
					isSeekPending = true;
					break;
				}
			}*/
			//Debug.Log("any pending: " + isSeekPending);
		}

		internal void StartCapture()
		{
			Debug.Log("startcap");
			ScanForVideoPlayers();
			foreach (VideoPlayerInstance instance in _instances)
			{
				instance.StartCapture();
			}
		}

		internal void StopCapture()
		{
			foreach (VideoPlayerInstance instance in _instances)
			{
				instance.StopCapture();
			}
		}

		public void ScanForVideoPlayers()
		{
			Debug.Log("scan");
			// Remove any VideoPlayer instances with deleted (null) VideoPlayers
			for (int i = 0; i < _instances.Count; i++)
			{
				VideoPlayerInstance instance = _instances[i];
				if (instance.Is(null))
				{
					_instances.RemoveAt(i); i--;
				}
			}

			// Find all inactive and active VideoPlayers
			VideoPlayer[] videoPlayers = Resources.FindObjectsOfTypeAll<VideoPlayer>();

			// Create a unique instance for each director
			foreach (VideoPlayer videoPlayer in videoPlayers)
			{
				// Check we don't already have this VideoPlayer
				bool hasVideoPlayer = false;
				foreach (VideoPlayerInstance instance in _instances)
				{
					if (instance.Is(videoPlayer))
					{
						hasVideoPlayer = true;
						break;
					}
				}

				// Add to the list
				if (!hasVideoPlayer)
				{
					_instances.Add(new VideoPlayerInstance(videoPlayer));
					Debug.Log("add");
				}
			}
		}

		void OnDestroy()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			StopCapture();
		}

		void ResetSceneLoading()
		{
			SceneManager.sceneLoaded -= OnSceneLoaded;
			if (_scanFrequency == ScanFrequencyMode.SceneLoad)
			{
				SceneManager.sceneLoaded += OnSceneLoaded;
			}
		}
		
		void OnSceneLoaded(Scene scene, LoadSceneMode mode)
		{
			if (_scanFrequency == ScanFrequencyMode.SceneLoad)
			{
				ScanForVideoPlayers();
			}
		}
	}
}
#endif
#endif