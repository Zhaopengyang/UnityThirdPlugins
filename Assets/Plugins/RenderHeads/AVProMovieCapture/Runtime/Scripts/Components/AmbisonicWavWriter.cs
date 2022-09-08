using UnityEngine;
using System.Collections.Generic;
using System.IO;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Combine multiple AmbisonicSource streams into a single ambisonic mix
	/// We had to use an overcomplicated buffer queue system because the with the audio thread
	/// we have no idea which frame we are on, and so no reliable way to know when to write 
	/// or accumulate the audio into the mix.
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Ambisonic WAV Writer", 601)]
	public class AmbisonicWavWriter : MonoBehaviour
	{
		[SerializeField] CaptureBase _capture = null;
		[SerializeField] AmbisonicOrder _order = AmbisonicOrder.Third;
		[SerializeField] AmbisonicFormat _format = AmbisonicFormat.ACN_SN3D;
		[SerializeField] string _filename = "output.wav";
		[SerializeField, Range(4, 32)] int _bufferCount = 16;

		private float[] _outSamples = null;
		private WavWriter _wavWriter = null;
		private List<AmbisonicSource> _sources = new List<AmbisonicSource>(8);
		private int _pendingSampleCount = 0;

		public AmbisonicOrder Order { get { return _order; } }
		public AmbisonicFormat Format { get { return _format; } }

		internal void AddSource(AmbisonicSource source)
		{
			lock (_sources)
			{
				SetupSource(source);
				_sources.Add(source);
			}
		}

		internal void RemoveSource(AmbisonicSource source)
		{
			lock (_sources)
			{
				_sources.Remove(source);
			}
		}
		
		void OnDisable()
		{
			StopCapture();
		}

		void SetupSource(AmbisonicSource source)
		{
			source.Setup(_order, Ambisonic.GetChannelOrder(_format), Ambisonic.GetNormalisation(_format), _bufferCount);
			source.FlushBuffers();
		}

		void ToggleCapturing(bool isCapturing)
		{
			if (isCapturing && !IsCapturing())
			{
				StartCapture();
			}
			else if (!isCapturing && IsCapturing())
			{
				StopCapture();
			}
		}

		void StartCapture()
		{
			#if UNITY_EDITOR
			if (UnityEditor.EditorApplication.isPaused) return;
			#endif

			Debug.Assert(_outSamples == null);
			Debug.Assert(_wavWriter == null);

			_pendingSampleCount = 0;
			int coeffCount = Ambisonic.GetCoeffCount(_order);
			Debug.Assert(coeffCount == 4 || coeffCount == 9 || coeffCount == 16);

			lock (this)
			{
				foreach (AmbisonicSource source in _sources)
				{
					SetupSource(source);
				}

				string path = Path.Combine(Application.persistentDataPath, _filename);
				if (_capture)
				{
					path = _capture.LastFilePath + ".wav";
				}
				Debug.Log("[AVProMovieCapture] Writing Ambisonic WAV to " + path);
				_wavWriter = new WavWriter(path, coeffCount, AudioSettings.outputSampleRate, WavWriter.SampleFormat.Float32);

				_outSamples = new float[coeffCount * AudioSettings.outputSampleRate * 1];	// 1 second buffer
			}
		}

		void StopCapture()
		{
			if (_wavWriter != null)
			{
				lock (_wavWriter)
				{
					FlushWavWriter();
					_wavWriter.Dispose();
					_wavWriter = null;
					_outSamples = null;
				}
			}
		}

		bool IsCapturing()
		{
			return (_wavWriter != null && _outSamples != null);
		}

		void LateUpdate()
		{
			ToggleCapturing(_capture != null && _capture.IsCapturing());

			ProcessSources(isDraining:false);
		}

		void ProcessSources(bool isDraining)
		{
			if (!IsCapturing() || _capture.IsPaused()) return;

			lock(this)
			{
				if (_sources.Count > 0)
				{
					// Find the minimum number of full buffers across all sources
					int minBuffers = int.MaxValue;
					for (int i = 0; i < _sources.Count; i++)
					{
						int bufferCount = _sources[i].GetFullBufferCount();
						minBuffers = Mathf.Min(bufferCount, minBuffers);
					}

					// Process the minimum number of full buffers
					for (int j = 0; j < minBuffers; j++)
					{
						for (int i = 0; i < _sources.Count; i++)
						{
							// TODO: fix this draining - it doesn't take into account cases where some of the sources
							// still have a full buffer, but other's don't.
							_sources[i].SendSamplesToSink(i != 0, isDraining);
						}
						FlushWavWriter();
					}
				}
			}
		}

		internal void MixSamples(float[] samples, int sampleCount, bool addSamples)
		{
			Debug.Assert(sampleCount < _outSamples.Length);

			if (sampleCount < _outSamples.Length)
			{
				if (!addSamples)
				{
					_pendingSampleCount = sampleCount;
					System.Buffer.BlockCopy(samples, 0, _outSamples, 0, sampleCount * sizeof(float));
				}
				else
				{
					// Accumulate samples into the mix
					for (int i = 0; i < sampleCount; i++)
					{
						_outSamples[i] += samples[i];
					}
				}
			}
			else
			{
				Debug.LogError("too many samples");
			}
		}

		void FlushWavWriter()
		{
			if (_pendingSampleCount > 0)
			{
				_wavWriter.WriteInterleaved(_outSamples, _pendingSampleCount);
				_pendingSampleCount = 0;
			}
		}
	}
}