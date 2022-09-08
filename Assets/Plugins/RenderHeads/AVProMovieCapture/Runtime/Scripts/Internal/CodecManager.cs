using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public enum MediaApi
	{
		Unknown = -1,
		AVFoundation = 0,
		MediaFoundation = 1,
		DirectShow = 2,
		MediaCodec = 3,			// Android
	}

	public enum CodecType
	{
		Video,
		Audio,
	}

	public class Codec : IMediaApiItem
	{
		private CodecType _codecType;
		private int _index;
		private string _name;
		private bool _hasConfigWindow;
		private MediaApi _api;

		public CodecType CodecType { get { return _codecType; } }
		public int Index { get { return _index; } }
		public string Name { get { return _name; } }
		public MediaApi MediaApi { get { return _api; } }
		public bool HasConfigwindow { get { return _hasConfigWindow; } }

		public void ShowConfigWindow()
		{
			if (_hasConfigWindow)
			{
				if (_codecType == CodecType.Video)
				{
					NativePlugin.ConfigureVideoCodec(_index);
				}
				else if (_codecType == CodecType.Audio)
				{
					NativePlugin.ConfigureAudioCodec(_index);
				}
			}
		}

		internal Codec(CodecType codecType, int index, string name, MediaApi api, bool hasConfigWindow = false)
		{
			_codecType = codecType;
			_index = index;
			_name = name;
			_api = api;
			_hasConfigWindow = hasConfigWindow;
		}
	}

	public class CodecList :  IEnumerable
	{
		internal CodecList(Codec[] codecs)
		{
			_codecs = codecs;
		}

		public Codec FindCodec(string name, MediaApi mediaApi = MediaApi.Unknown)
		{
			Codec result = null;
			foreach (Codec codec in _codecs)
			{
				if (codec.Name == name)
				{
					if (mediaApi == MediaApi.Unknown || mediaApi == codec.MediaApi)
					{
						result = codec;
						break;
					}
				}
			}
			return result;
		}

		public Codec GetFirstWithMediaApi(MediaApi api)
		{
			Codec result = null;
			foreach (Codec codec in _codecs)
			{
				if (codec.MediaApi == api)
				{
					result = codec;
					break;
				}
			}
			return result;
		}

		public IEnumerator GetEnumerator()
		{
			return _codecs.GetEnumerator();
		}

		public Codec[] Codecs { get { return _codecs; } }
		public int Count { get{ return _codecs.Length; } }

		private Codec[] _codecs = new Codec[0];
	}

	public static class CodecManager
	{
		public static Codec FindCodec(CodecType codecType, string name)
		{
			CheckInit();
			Codec result = null;
			CodecList codecs = GetCodecs(codecType);
			result = codecs.FindCodec(name);
			return result;
		}

		public static int GetCodecCount(CodecType codecType)
		{
			CheckInit();
			return GetCodecs(codecType).Count;
		}

		private static void CheckInit()
		{
			if (!_isEnumerated)
			{
				if (NativePlugin.Init())
				{
					EnumerateCodecs();
				}
			}
		}

		private static CodecList GetCodecs(CodecType codecType)
		{
			CodecList result = null;
			switch (codecType)
			{
				case CodecType.Video:
					result = _videoCodecs;
					break;
				case CodecType.Audio:
					result = _audioCodecs;
					break;
			}
			return result;
		}		

		private static void EnumerateCodecs()
		{
			{
				Codec[] videoCodecs = new Codec[NativePlugin.GetVideoCodecCount()];
				for (int i = 0; i < videoCodecs.Length; i++)
				{
					videoCodecs[i] = new Codec(CodecType.Video, i, NativePlugin.GetVideoCodecName(i), NativePlugin.GetVideoCodecMediaApi(i), NativePlugin.IsConfigureVideoCodecSupported(i));
				}
				_videoCodecs = new CodecList(videoCodecs);
			}
			{
				Codec[] audioCodecs = new Codec[NativePlugin.GetAudioCodecCount()];
				for (int i = 0; i < audioCodecs.Length; i++)
				{
					audioCodecs[i] = new Codec(CodecType.Audio, i, NativePlugin.GetAudioCodecName(i), NativePlugin.GetAudioCodecMediaApi(i), NativePlugin.IsConfigureAudioCodecSupported(i));
				}
				_audioCodecs = new CodecList(audioCodecs);
			}

			_isEnumerated = true;
		}

		public static CodecList VideoCodecs { get { CheckInit(); return _videoCodecs; } }
		public static CodecList AudioCodecs { get { CheckInit(); return _audioCodecs; } }

		private static bool _isEnumerated = false;

		private static CodecList _videoCodecs = new CodecList(new Codec[0]);
		private static CodecList _audioCodecs = new CodecList(new Codec[0]);
	}
}