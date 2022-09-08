using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2014-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Place this component below an AudioSource or AudioListener to capture to WAV file
	/// </summary>	
	[AddComponentMenu("AVPro Movie Capture/Audio/AudioSource To WAV", 701)]
	public class AudioSourceToWav : MonoBehaviour 
	{
		[SerializeField] string _filename = "output.wav";
		private WavWriter _wavWriter;

		void OnEnable()
		{
			string path = Path.Combine(Application.persistentDataPath, _filename);
			Debug.Log("[AVProMovieCapture] Writing WAV to " + path);
			_wavWriter = new WavWriter(path, UnityAudioCapture.GetUnityAudioChannelCount(), AudioSettings.outputSampleRate, WavWriter.SampleFormat.Float32);
		}

		void OnDisable()
		{
			_wavWriter.Dispose();
			_wavWriter = null;
		}

		void OnAudioFilterRead(float[] data, int channels)
		{
			_wavWriter.WriteInterleaved(data);
		}
	}
}