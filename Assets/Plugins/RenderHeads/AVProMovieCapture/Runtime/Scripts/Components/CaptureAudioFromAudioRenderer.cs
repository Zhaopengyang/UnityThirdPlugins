#if UNITY_2017_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
#endif

#if UNITY_2019_3_OR_NEWER
	#define UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
#endif

#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
using UnityEngine;
#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
#else
using UnityEngine.Collections;
#endif

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Encodes audio directly from AudioRenderer (https://docs.unity3d.com/ScriptReference/AudioRenderer.html)
	/// While capturing, audio playback in Unity becomes muted
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Capture Audio (From AudioRenderer)", 500)]
	public class CaptureAudioFromAudioRenderer : UnityAudioCapture
	{
		[SerializeField] CaptureBase _capture = null;

		private int _unityAudioChannelCount;
		private bool _isRendererRecording;

		public CaptureBase Capture { get { return _capture; } set { _capture = value; } }
		public override int SampleRate { get { return AudioSettings.outputSampleRate; } }
		public override int ChannelCount { get { return _unityAudioChannelCount; } }

		public override void PrepareCapture()
		{
			_unityAudioChannelCount = GetUnityAudioChannelCount();
		}

#if UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
		private NativeArray<float> _audioBuffer;
#endif

		private NativeArray<float> GetAudioBufferOfLength(int length)
		{
#if UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
			if (_audioBuffer.Length < length)
			{
				Debug.Log("Creating new audio buffer with length: " + length);
				if (_audioBuffer.IsCreated)
					_audioBuffer.Dispose();
				_audioBuffer = new NativeArray<float>(length, Allocator.Persistent);
			}
			return _audioBuffer.GetSubArray(0, length);
#else
			return new NativeArray<float>(length, Allocator.TempJob);
#endif
		}

		private void DisposeAudioBuffer(NativeArray<float> buffer)
		{
#if !UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
			buffer.Dispose();
#endif
		}

		public override void StartCapture()
		{
			if (_capture == null)
			{
				Debug.LogWarning("[AVProMovieCapture] CaptureAudioFromAudioRenderer has no Capture source set");
				return;
			}			
			if (!_isRendererRecording)
			{
#if UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
				// Allocate a big buffer for capturing the audio into
				_audioBuffer = new NativeArray<float>(65536, Allocator.Persistent);
#endif
				AudioRenderer.Start();
				_isRendererRecording = true;
			}
			FlushBuffer();
		}

		public override void StopCapture()
		{
			if (_isRendererRecording)
			{
				_isRendererRecording = false;
				AudioRenderer.Stop();
#if UNITY_NATIVEARRAY_GETSUBARRAY_SUPPORT
				_audioBuffer.Dispose();
#endif
			}
		}

		public override void FlushBuffer()
		{
			int sampleFrameCount = AudioRenderer.GetSampleCountForCaptureFrame();
			int sampleCount = sampleFrameCount * _unityAudioChannelCount;
			NativeArray<float> buffer = GetAudioBufferOfLength(sampleCount);
			AudioRenderer.Render(buffer);
			DisposeAudioBuffer(buffer);
		}

		void Update()
		{
			if (_isRendererRecording && _capture != null && _capture.IsCapturing() && !_capture.IsPaused())
			{
				int sampleFrameCount = AudioRenderer.GetSampleCountForCaptureFrame();
				int sampleCount = sampleFrameCount * _unityAudioChannelCount;
				NativeArray<float> buffer = GetAudioBufferOfLength(sampleCount);
				if (AudioRenderer.Render(buffer))
				{
					_capture.EncodeAudio(buffer);
				}
				DisposeAudioBuffer(buffer);
			}
		}
	}
}

#endif // AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE