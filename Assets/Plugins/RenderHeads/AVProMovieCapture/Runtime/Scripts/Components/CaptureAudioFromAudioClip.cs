using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Encodes audio directly from an AudioClip
	/// Requires AudioCaptureSource.Manual to be set on the Capture component
	/// Make sure the AudioClip sample rate and channel count matches that of the Capture component
	/// Designed to work in offline capture mode
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Capture Audio (From AudioClip)", 500)]
	public class CaptureAudioFromAudioClip : MonoBehaviour
	{
		[SerializeField] CaptureBase _capture = null;
		[SerializeField] AudioClip _audioClip = null;
		//[SerializeField] bool _loopAudio = false;

		private int _videoOffsetInSamples = 0;
		private int _committedFrames = 0;
		private int _committedSamples = 0;
		private int _lastCommittedSample = -1;

		private float[] _frameBuffer = null;

		void OnEnable()
		{
			_lastCommittedSample = -1;
			if (_audioClip && _capture)
			{
				if (_audioClip.frequency != _capture.ManualAudioSampleRate)
				{
					Debug.LogError(string.Format("[AVProMovieCapture] AudioClip audio frequency {0} doesn't match the encoding frequency {1}", _audioClip.frequency, _capture.ManualAudioSampleRate));
				}
				if (_audioClip.channels != _capture.ManualAudioChannelCount)
				{
					Debug.LogError(string.Format("[AVProMovieCapture] AudioClip audio channelCount {0} doesn't match the encoding channelCount {1}", _audioClip.channels, _capture.ManualAudioChannelCount));
				}
			}
		}

		void Update()
		{
			if (_capture != null && _capture.IsCapturing())
			{
				float[] samples = GetAudioSamplesForFrame();
				if (samples != null)
				{
					_capture.EncodeAudio(samples);
				}
				_committedFrames++;
			}
		}

		private float[] GetAudioSamplesForFrame()
		{
			float[] result = null;
			if (_audioClip != null && _audioClip.samples > 0)
			{
				int sampleCommitSize = (int)(_capture.ManualAudioSampleRate / _capture.FrameRate);
				float videoRenderTime = (float)_committedFrames / _capture.FrameRate;
				float videoTimeInSamples = videoRenderTime * (float)_audioClip.frequency;

				if (_lastCommittedSample < videoTimeInSamples && videoTimeInSamples >= 0)
				{
					int startSampleIndex = (_lastCommittedSample + 1) - _videoOffsetInSamples;
					int endSampleIndex = startSampleIndex + sampleCommitSize - 1;

					int requiredSamples = (_audioClip.channels * sampleCommitSize);
					if (_lastCommittedSample < _audioClip.samples - 1)
					{
						if (endSampleIndex >= _audioClip.samples)
						{
							// We've reached the end of the clip samples, so just grab what remains
							requiredSamples = (_audioClip.channels * (_audioClip.samples - startSampleIndex));
						}

						// Allocate buffer if needed
						if (_frameBuffer == null || _frameBuffer.Length != requiredSamples)
						{
							_frameBuffer = new float[requiredSamples];
						}

						_audioClip.GetData(_frameBuffer, startSampleIndex);

						_committedSamples++;
						_lastCommittedSample = (_committedSamples * sampleCommitSize) - 1;
					}
					else
					{
						// We've reached the end of the audio clip, so just create empty audio data
						requiredSamples = (_audioClip.channels * sampleCommitSize);
						// Allocate buffer if needed
						if (_frameBuffer == null || _frameBuffer.Length != requiredSamples)
						{
							_frameBuffer = new float[requiredSamples];
						}
					}
					result = _frameBuffer;
				}
			}
			return result;
		}
	}
}