using System.Collections.Generic;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Plays an AudioSource (which should only have a single mono channel) for ambisonic encoding
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Audio/Ambisonic Source", 601)]
	public partial class AmbisonicSource : MonoBehaviour
	{
		[SerializeField] AmbisonicWavWriter _sink = null;

		[Tooltip("Listener is optional but allows positions to be calculated relative to a transform.  This is useful if the listener is not located at 0,0,0.")]
		[SerializeField] Transform _listener = null;

		private Vector3 _position = Vector3.zero;
		private AmbisonicOrder _order;
		private AmbisonicChannelOrder _channelOrder;
		private AmbisonicNormalisation _normalisation;
		private System.IntPtr _sourceInstance = System.IntPtr.Zero;

		private int _activeSampleIndex = 0;
		private float[] _activeSamples = null;
		private Queue<float[]> _fullBuffers = new Queue<float[]>(8);
		private Queue<float[]> _emptyBuffers = new Queue<float[]>(8);

		void OnEnable()
		{
			AudioSource audioSource = this.GetComponent<AudioSource>();
			if (audioSource && audioSource.clip)
			{
				audioSource.PlayOneShot(audioSource.clip, 0f);
			}

			Debug.Assert(_sourceInstance == System.IntPtr.Zero);
			_sourceInstance = NativePlugin.AddAmbisonicSourceInstance(Ambisonic.MaxCoeffs);

			_position = this.transform.position;
			UpdateCoefficients();
			if (_sink)
			{
				_sink.AddSource(this);
			}
		}

		void OnDisable()
		{
			lock (this)
			{
				if (_sink)
				{
					_sink.RemoveSource(this);
				}

				if (_sourceInstance != System.IntPtr.Zero)
				{
					NativePlugin.RemoveAmbisonicSourceInstance(_sourceInstance);
					_sourceInstance = System.IntPtr.Zero;
				}
			}
		}

		internal void Setup(AmbisonicOrder order, AmbisonicChannelOrder channelOrder, AmbisonicNormalisation normalisation, int bufferCount)
		{
			Debug.Assert(bufferCount > 1 && bufferCount < 100);
			lock (this)
			{
				_order = order;
				_channelOrder = channelOrder;
				_normalisation = normalisation;
				int sampleCount = Ambisonic.GetCoeffCount(order) * AudioSettings.outputSampleRate / 10;	// 1/10 second buffer

				_activeSampleIndex = 0;
				_activeSamples = null;
				_fullBuffers.Clear();
				_emptyBuffers.Clear();
				for (int i = 0; i < bufferCount; i++)
				{
					float[] buffer = new float[sampleCount];
					_emptyBuffers.Enqueue(buffer);
				}

				UpdateCoefficients();
			}
		}

		void OnDrawGizmos()
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(_position, 1.2f);
			if (_listener)
			{
				Gizmos.DrawLine(this.transform.position, _listener.position);
			}
		}

		void LateUpdate()
		{
			// Convert position relative to listener
			Vector3 p = this.transform.position;
			if (_listener)
			{
				p = (p - _listener.position);
				//Debug.Log(this.transform.parent.name + ": " + p + " " + _listener.position + " - " + this.transform.position);
			}

			// If the relative audiosource position has changed
			if (p != _position)
			{
				SetListenerRelativePosition(p);
			}
		}
		
		void SetListenerRelativePosition(Vector3 position)
		{
			// Optionally smooth out the motion
			//_position = Vector3.MoveTowards(_position, position, Time.deltaTime * 10f);

			_position = position;
			UpdateCoefficients();
		}

		void UpdateCoefficients()
		{
			Ambisonic.PolarCoord p = new Ambisonic.PolarCoord();
			p.FromCart(_position);

			lock (this)
			{
				float[] normaliseWeights = Ambisonic.GetNormalisationWeights(_normalisation);
				NativePlugin.UpdateAmbisonicWeights(_sourceInstance, p.azimuth, p.elevation, _order, _channelOrder, normaliseWeights);
			}
		}

		void OnAudioFilterRead(float[] samples, int channelCount)
		{
			lock (this)
			{
				int coeffCount = Ambisonic.GetCoeffCount(_order);
				if (_sink != null && coeffCount > 0)
				{
					int samplesOffset = 0;
					// While there are sample to process
					while (samplesOffset < samples.Length)
					{
						// If the pending buffer is full, move it to the full ist
						if (_activeSamples != null && _activeSamples.Length == _activeSampleIndex)
						{
							_fullBuffers.Enqueue(_activeSamples);
							_activeSamples = null;
							_activeSampleIndex = 0;
						}
						// Assign a new pending queue
						if (_activeSamples == null && _emptyBuffers.Count > 0)
						{
							_activeSamples = _emptyBuffers.Dequeue();
						}
						if (_activeSamples == null)
						{
							// Remaining samples are lost!
							break;
						}
						int remainingFrameCount = (samples.Length - samplesOffset) / channelCount;
						int generatedSampleCount = remainingFrameCount * coeffCount;
						int remainingSampleSpace = (_activeSamples.Length - _activeSampleIndex);

						int samplesToProcess = Mathf.Min(remainingSampleSpace, generatedSampleCount);
						// TODO: should we specify Floor/Ceil rounding behaviour?
						int framesToProcess =  samplesToProcess / coeffCount;
						generatedSampleCount = framesToProcess * coeffCount;

						if (framesToProcess > 0)
						{
							NativePlugin.EncodeMonoToAmbisonic(_sourceInstance, samples, samplesOffset, framesToProcess, channelCount, _activeSamples, _activeSampleIndex, _activeSamples.Length, _order);
							_activeSampleIndex += generatedSampleCount;
							samplesOffset += framesToProcess * channelCount;
						}
						else
						{
							Debug.Log(coeffCount + " " + framesToProcess + "   " + remainingSampleSpace + " >>  " + samplesOffset + " /  " + samples.Length);
							break;
						}
					}
				}
			}
		}

		internal void FlushBuffers()
		{
			lock (this)
			{
				_activeSampleIndex = 0;
				foreach (float[] buffer in _fullBuffers)
				{
					_emptyBuffers.Enqueue(buffer);
				}
				if (_activeSamples != null)
				{
					_emptyBuffers.Enqueue(_activeSamples);
					_activeSamples = null;
				}
			}
		}

		internal int GetFullBufferCount()
		{
			return _fullBuffers.Count;
		}

		internal void SendSamplesToSink(bool isAdditive, bool isDraining)
		{
			lock (this)
			{
				float[] samples = null;
				if (_fullBuffers.Count > 0)
				{
					// Send a full buffer
					samples = _fullBuffers.Dequeue();
					_sink.MixSamples(samples, samples.Length, isAdditive);
				}
				else if (isDraining)
				{
					// Send partial of the active buffer
					samples = _activeSamples;
					_sink.MixSamples(_activeSamples, _activeSampleIndex, isAdditive);
					_activeSampleIndex = 0;
					_activeSamples = null;
				}

				if (samples != null)
				{
					_emptyBuffers.Enqueue(samples);
				}
			}
		}
	}
}