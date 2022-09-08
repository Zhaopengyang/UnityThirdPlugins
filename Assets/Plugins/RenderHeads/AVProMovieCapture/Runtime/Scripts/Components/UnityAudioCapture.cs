using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Base class for capturing audio from Unity
	/// Two classes derive from this:
	/// 1) CaptureAudioFromAudioListener - used for real-time capture 
	/// 2) CaptureAudioFromAudioRenderer - used for offline rendering
	/// </summary>
	public abstract class UnityAudioCapture : MonoBehaviour
	{
		public virtual int OverflowCount
		{
			get { return 0; }
		}

		public abstract int SampleRate
		{
			get;
		}

		public abstract int ChannelCount
		{
			get;
		}

		public abstract void PrepareCapture();
		public abstract void StartCapture();
		public abstract void StopCapture();
		public abstract void FlushBuffer();

		public virtual System.IntPtr ReadData(out int length) { length = 0; return System.IntPtr.Zero; }

		public static int GetUnityAudioChannelCount()
		{
			int result = GetChannelCount(AudioSettings.driverCapabilities);
			if (
#if !UNITY_2019_2_OR_NEWER
				AudioSettings.speakerMode != AudioSpeakerMode.Raw &&
#endif
				AudioSettings.speakerMode < AudioSettings.driverCapabilities)
			{
				result = GetChannelCount(AudioSettings.speakerMode);
			}
			return result;
		}

		private static int GetChannelCount(AudioSpeakerMode mode)
		{
			int result = 0;
			switch (mode)
			{
#if !UNITY_2019_2_OR_NEWER
				case AudioSpeakerMode.Raw:
					break;
#endif
				case AudioSpeakerMode.Mono:
					result = 1;
					break;
				case AudioSpeakerMode.Stereo:
					result = 2;
					break;
				case AudioSpeakerMode.Quad:
					result = 4;
					break;
				case AudioSpeakerMode.Surround:
					result = 5;
					break;
				case AudioSpeakerMode.Mode5point1:
					result = 6;
					break;
				case AudioSpeakerMode.Mode7point1:
					result = 8;
					break;
				case AudioSpeakerMode.Prologic:
					result = 2;
					break;
			}
			return result;
		}
	}
}