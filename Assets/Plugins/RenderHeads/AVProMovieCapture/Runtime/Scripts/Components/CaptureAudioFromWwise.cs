using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Encodes audio directly from Wwise in offline render mode
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Capture Audio (From Wwise)", 500)]
	public class CaptureAudioFromWwise : UnityAudioCapture
	{
		[SerializeField] CaptureBase _capture = null;

		public CaptureBase Capture { get { return _capture; } set { _capture = value; } }

#if AVPRO_MOVIECAPTURE_WWISE_SUPPORT
		private int _audioChannelCount;
		private int _audioSampleRate;
		private ulong _outputDeviceId;
		private bool _isRendererRecording;

		public override int SampleRate { get { return _audioSampleRate; } }
		public override int ChannelCount { get { return _audioChannelCount; } }

		public override void PrepareCapture()
		{
			if (_capture == null)
			{
				Debug.LogWarning("[AVProMovieCapture] CaptureAudioFromWwise has no Capture source set");
				return;
			}
						
			var sampleRate = AkSoundEngine.GetSampleRate();
			var channelConfig = new AkChannelConfig();
			var audioSinkCapabilities = new Ak3DAudioSinkCapabilities();
			AkSoundEngine.GetOutputDeviceConfiguration(_outputDeviceId, channelConfig, audioSinkCapabilities);

			_audioSampleRate = (int)sampleRate;
			_audioChannelCount = (int)channelConfig.uNumChannels;
			_outputDeviceId = AkSoundEngine.GetOutputID(AkSoundEngine.AK_INVALID_UNIQUE_ID, 0);

			#if UNITY_EDITOR
			// Ensure that the editor update does not call AkSoundEngine.RenderAudio().
			AkSoundEngineController.Instance.DisableEditorLateUpdate();
			#endif

			AkSoundEngine.StartDeviceCapture(_outputDeviceId);
			AkSoundEngine.SetOfflineRenderingFrameTime(1f / _capture.FrameRate);
			AkSoundEngine.SetOfflineRendering(true);
		}

		public override void StartCapture()
		{
			if (_capture == null)
			{
				Debug.LogWarning("[AVProMovieCapture] CaptureAudioFromWwise has no Capture source set");
				return;
			}
			if (!_isRendererRecording)
			{
				_isRendererRecording = true;
			}
			FlushBuffer();
		}

		public override void StopCapture()
		{
			if (_isRendererRecording)
			{
				_isRendererRecording = false;
				AkSoundEngine.StopDeviceCapture(_outputDeviceId);

				#if UNITY_EDITOR
				// Bring back editor update calls to AkSoundEngine.RenderAudio().
				AkSoundEngineController.Instance.EnableEditorLateUpdate();
				#endif

				AkSoundEngine.SetOfflineRenderingFrameTime(0f);
				AkSoundEngine.SetOfflineRendering(false);
			}
		}

		public override void FlushBuffer()
		{
			AkSoundEngine.ClearCaptureData();
		}

		void Update()
		{
			if (_isRendererRecording && _capture != null && _capture.IsCapturing() && !_capture.IsPaused())
			{
				var sampleCount = AkSoundEngine.UpdateCaptureSampleCount(_outputDeviceId);				
				if (sampleCount <= 0)
				{
					return;
				}

				var buffer = new float[sampleCount];
				var count = AkSoundEngine.GetCaptureSamples(_outputDeviceId, buffer, (uint)buffer.Length);
				if (count <= 0)
				{
					return;
				}
				_capture.EncodeAudio(buffer);
			}
		}
#else
		void Awake()
		{
			Debug.LogError("[AVProMovieCapture] CaptureAudioFromWise component requires AVPRO_MOVIECAPTURE_WWISE_SUPPORT to be added to Script Define Symbols");
		}
		public override int SampleRate { get { return 0; } }
		public override int ChannelCount { get { return 0; } }
		public override void PrepareCapture() {}
		public override void FlushBuffer() {}
		public override void StartCapture() {}
		public override void StopCapture() {}
#endif // AVPRO_MOVIECAPTURE_WWISE_SUPPORT
	}
}