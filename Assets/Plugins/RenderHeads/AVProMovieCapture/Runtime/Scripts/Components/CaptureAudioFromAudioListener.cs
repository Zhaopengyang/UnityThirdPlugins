using UnityEngine;
using System.Collections;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Captures audio from an AudioListener and adds it to a buffer
	/// The AVPro Movie Capture plugin pulls the data from this buffer and flushes it periodically
	/// Add this component below the main AudioLister to capture all audio, or add it below
	/// an AudioSource component to only capture that source
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Capture Audio (From AudioListener)", 500)]
	[RequireComponent(typeof(AudioListener))]
	public class CaptureAudioFromAudioListener : UnityAudioCapture
	{
		[SerializeField] bool _debugLogging = false;
		[SerializeField] bool _muteAudio = false;

		private const int BufferSize = 16;
		private float[] _buffer = new float[0];
		private float[] _readBuffer = new float[0];
		private int _bufferIndex;
		private GCHandle _bufferHandle;
		private int _numChannels;

		private int _overflowCount;
		private object _lockObject = new object();

		public float[] Buffer { get { return _readBuffer; } }
		public int BufferLength { get { return _bufferIndex; } }
		public System.IntPtr BufferPtr { get { return _bufferHandle.AddrOfPinnedObject(); } }

		public override int OverflowCount {	get { return _overflowCount; } }
		public override int SampleRate { get { return AudioSettings.outputSampleRate; } }
		public override int ChannelCount { get { return _numChannels; } }

		public override void PrepareCapture()
		{
			int bufferLength = 0;
			int numBuffers = 0;
			AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);

			_numChannels = GetUnityAudioChannelCount();
			if (_debugLogging)
			{
				Debug.Log(string.Format("[UnityAudioCapture] SampleRate: {0}hz SpeakerMode: {1} BestDriverMode: {2} (DSP using {3} buffers of {4} bytes using {5} channels)", AudioSettings.outputSampleRate, AudioSettings.speakerMode.ToString(), AudioSettings.driverCapabilities.ToString(), numBuffers, bufferLength, _numChannels));
			}

			_buffer = new float[bufferLength * _numChannels * numBuffers * BufferSize];
			_readBuffer = new float[bufferLength * _numChannels * numBuffers * BufferSize];
			_bufferIndex = 0;
			_bufferHandle = GCHandle.Alloc(_readBuffer, GCHandleType.Pinned);
			_overflowCount = 0;
		}

		public override void StartCapture()
		{
			FlushBuffer();
		}

		public override void StopCapture()
		{
			lock (_lockObject)
			{
				_bufferIndex = 0;
				if (_bufferHandle.IsAllocated)
					_bufferHandle.Free();
				_readBuffer = _buffer = null;
			}
			_numChannels = 0;
		}

		public override System.IntPtr ReadData(out int length)
		{
			lock (_lockObject)
			{
				System.Array.Copy(_buffer, 0, _readBuffer, 0, _bufferIndex);
				length = _bufferIndex;
				_bufferIndex = 0;
			}
			return _bufferHandle.AddrOfPinnedObject();
		}

		public override void FlushBuffer()
		{
			lock (_lockObject)
			{
				_bufferIndex = 0;
				_overflowCount = 0;
			}
		}

		private void OnAudioFilterRead(float[] data, int channels)
		{
			if (_buffer != null && _buffer.Length > 0)
			{
				// TODO: use double buffering
				lock (_lockObject)
				{
					int length = Mathf.Min(data.Length, _buffer.Length - _bufferIndex);

					//System.Array.Copy(data, 0, _buffer, _bufferIndex, length);

					if (!_muteAudio)
					{
						for (int i = 0; i < length; i++)
						{
							_buffer[i + _bufferIndex] = data[i];
						}
					}
					else
					{
						for (int i = 0; i < length; i++)
						{
							_buffer[i + _bufferIndex] = data[i];
							data[i] = 0f;
						}
					}
					_bufferIndex += length;

					if (length < data.Length)
					{
						_overflowCount++;
						Debug.LogWarning("[AVProMovieCapture] Audio buffer overflow, may cause sync issues.  Disable this component if not recording Unity audio.");
					}
				}
			}
		}
	}
}