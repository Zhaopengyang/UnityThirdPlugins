using UnityEngine;
using System;
using System.Text;
using System.Runtime.InteropServices;
#if UNITY_IOS || UNITY_TVOS || ENABLE_IL2CPP
using AOT;
#endif

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public partial class NativePlugin
	{
		public const string ScriptVersion = "5.0.2";
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
		public const string ExpectedPluginVersion = "5.0.0";
#elif UNITY_ANDROID && !UNITY_EDITOR
		public const string ExpectedPluginVersion = "5.0.2";
#else
		public const string ExpectedPluginVersion = "5.0.0";
#endif
	}

	public enum NoneAutoCustom
	{
		None,
		Auto,
		Custom,
	}

	public enum AudioCaptureSource
	{
		None = 0,
		Unity = 1,
		Microphone = 2,
		Manual = 3,
		Wwise = 4,
		UnityAudioMixer = 5,
	}

	public enum StereoPacking
	{
		None,
		TopBottom,
		LeftRight,
	}

	public enum SphericalVideoLayout
	{
		None,
		Equirectangular360,
		Equirectangular180,
		//TODO: Cubemap32
	}

	public enum StopMode
	{
		None,
		FramesEncoded,
		SecondsEncoded,
		SecondsElapsed,
	}

	public enum StartTriggerMode
	{
		Manual,
		OnStart,
	}

	public enum StartDelayMode
	{
		None,
		RealSeconds,
		GameSeconds,
		Manual,
	}

	public enum ImageSequenceFormat
	{
		PNG,
		JPEG,		// Apple and Android platforms only
		TIFF,		// Apple platforms only
		HEIF,		// Apple platforms only
	}

	public enum OutputTarget
	{
		VideoFile,
		ImageSequence,
		NamedPipe,
	}

	public partial class NativePlugin
	{
#if UNITY_IOS && !UNITY_EDITOR
		const string PluginName = "__Internal";
#elif UNITY_ANDROID && !UNITY_EDITOR
		const string PluginName = "AVProMovieCaptureNative";
#else
		const string PluginName = "AVProMovieCapture";
#endif

		public enum Platform
		{
			Unknown = -2,
			Current = -1,
			First = 0,

			Windows = 0,
			macOS = 1,
			iOS = 2,
			Android = 3,

			Count = 4,
		}

		public static string[] PlatformNames = { "Windows", "macOS", "iOS", "Android" };

		// The Apple platforms have a fixed set of known codecs
		public static readonly string[] VideoCodecNamesMacOS = { "H264", "HEVC", "MJPEG", "ProRes 4:2:2", "ProRes 4:4:4:4" };
		public static readonly string[] AudioCodecNamesMacOS = { "AAC", "FLAC", "Apple Lossless", "Linear PCM", "Uncompresssed" };
		public static readonly string[] VideoCodecNamesIOS = { "H264", "HEVC", "MJPEG" };
		public static readonly string[] AudioCodecNamesIOS = { "AAC", "FLAC", "Apple Lossless", "Linear PCM", "Uncompresssed" };
		public static readonly string[] VideoCodecNamesAndroid = { "H264", "HEVC"/*, "VP8", "VP9"*/ };
		public static readonly string[] AudioCodecNamesAndroid = { "AAC"/*, "OPUS", "FLAC"*/ };

		public enum PixelFormat
		{
			RGBA32,
			BGRA32,				// Note: This is the native format for Unity textures with red and blue swapped.
			YCbCr422_YUY2,
			YCbCr422_UYVY,
			YCbCr422_HDYC,
		}

		public const int MaxRenderWidth = 16384;
		public const int MaxRenderHeight = 16384;

#region RenderEventFunctions
		// Used by GL.IssuePluginEvent
		private const int PluginID = 0xFA30000;

		public enum PluginEvent
		{
			CaptureFrameBuffer = 0,
			FreeResources = 1,
			Setup = 2,
		}

		private static System.IntPtr _renderEventFunction = System.IntPtr.Zero;
		private static System.IntPtr _freeEventFunction = System.IntPtr.Zero;

		public static void RenderThreadEvent(PluginEvent renderEvent, int handle)
		{
			switch (renderEvent)
			{
				case PluginEvent.CaptureFrameBuffer:
					GL.IssuePluginEvent(RenderCaptureEventFunction, PluginID | (int)renderEvent | handle);
					break;
				case PluginEvent.FreeResources:
					GL.IssuePluginEvent(RenderFreeEventFunction, PluginID | (int)renderEvent);
					break;
				case PluginEvent.Setup:
				#if UNITY_ANDROID && !UNITY_EDITOR
					if (RenderSetupEventFunction != System.IntPtr.Zero)
					{
						GL.IssuePluginEvent(RenderSetupEventFunction, PluginID | (int)renderEvent | handle);
					}
				#endif
					break;
			}
		}

		private static System.IntPtr RenderCaptureEventFunction
		{
			get
			{
				if (_renderEventFunction == System.IntPtr.Zero)
				{
					_renderEventFunction = GetRenderEventFunc();
				}
				Debug.Assert(_renderEventFunction != System.IntPtr.Zero);
				return _renderEventFunction;
			}
		}

		private static System.IntPtr RenderFreeEventFunction
		{
			get
			{
				if (_freeEventFunction == System.IntPtr.Zero)
				{
					_freeEventFunction = GetFreeResourcesEventFunc();
				}
				Debug.Assert(_freeEventFunction != System.IntPtr.Zero);
				return _freeEventFunction;
			}
		}

		[DllImport(PluginName, EntryPoint="AVPMC_GetRenderEventFunc")]
		private static extern System.IntPtr GetRenderEventFunc();

		[DllImport(PluginName, EntryPoint="AVPMC_GetFreeResourcesEventFunc")]
		private static extern System.IntPtr GetFreeResourcesEventFunc();

#if UNITY_ANDROID && !UNITY_EDITOR
		private static System.IntPtr _setupEventFunction = System.IntPtr.Zero;

		private static System.IntPtr RenderSetupEventFunction
		{
			get
			{
				if (_setupEventFunction == System.IntPtr.Zero)
				{
					_setupEventFunction = GetSetupEventFunc();
				}
				return _setupEventFunction;
			}
		}

		[DllImport(PluginName, EntryPoint="AVPMC_GetSetupEventFunc")]
		private static extern System.IntPtr GetSetupEventFunc();
#endif

#endregion

#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
		internal class Logger
		{
			private enum LogFlag : int {
				Error	= 1 << 0,
				Warning	= 1 << 1,
				Info	= 1 << 2,
				Debug	= 1 << 3,
				Verbose	= 1 << 4,
			};

			private enum LogLevel : int
			{
				Off		= 0,
				Error	= LogFlag.Error,
				Warning	= Error | LogFlag.Warning,
				Info	= Warning | LogFlag.Info,
				Debug	= Info | LogFlag.Debug,
				Verbose	= Debug | LogFlag.Verbose,
				All		= -1,
			};

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
			[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#endif
			private delegate void DebugLogCallbackDelegate(LogLevel level, [In, MarshalAs(UnmanagedType.LPWStr)] string str);

#if UNITY_IOS || UNITY_TVOS || ENABLE_IL2CPP
			[MonoPInvokeCallback(typeof(DebugLogCallbackDelegate))]
#endif
			private static void DebugLogCallback(LogLevel level, string str)
			{
				if (level == LogLevel.Error)
				{
					Debug.LogError(str);
				}
				else if (level == LogLevel.Warning)
				{
					Debug.LogWarning(str);
				}
				else
				{
					Debug.Log(str);
				}
			}

			private DebugLogCallbackDelegate _callbackDelegate = new DebugLogCallbackDelegate(DebugLogCallback);

			internal Logger()
			{
				IntPtr func = Marshal.GetFunctionPointerForDelegate(_callbackDelegate);
				NativePlugin.SetLogFunction(func);
			}

			~Logger()
			{
				NativePlugin.SetLogFunction(IntPtr.Zero);
			}
		}

		internal static Logger _logger;

		private static void SetupDebugLogCallback()
		{
			_logger = new Logger();
		}

#if !UNITY_EDITOR_OSX && UNITY_IOS
		[DllImport(PluginName, EntryPoint="AVPMC_PluginBootstrap")]
		public static extern void MCPluginBootstrap();
#endif

		static NativePlugin()
		{
			#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
				SetupDebugLogCallback();
			#endif
			#if !UNITY_EDITOR_OSX && UNITY_IOS
				NativePlugin.MCPluginBootstrap();
			#endif
		}
#endif

		//////////////////////////////////////////////////////////////////////////
		// Global Init/Deinit

		[DllImport(PluginName, EntryPoint="AVPMC_Init")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool Init();

		[DllImport(PluginName, EntryPoint="AVPMC_Deinit")]
		public static extern void Deinit();

		[Flags]
		public enum MicrophoneRecordingOptions: int
		{
			None             = 0,
			MixWithOthers    = 1,
			DefaultToSpeaker = 2,
		}

		[DllImport(PluginName, EntryPoint="AVPMC_SetMicrophoneRecordingHint")]
		public static extern void SetMicrophoneRecordingHint([MarshalAs(UnmanagedType.U1)] bool enabled, MicrophoneRecordingOptions options = MicrophoneRecordingOptions.None);

		public static string GetPluginVersionString()
		{
			return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(GetPluginVersion());
		}

		[DllImport(PluginName, EntryPoint="AVPMC_IsTrialVersion")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsTrialVersion();

		public static bool IsBasicEdition()
		{
			string version = NativePlugin.GetPluginVersionString();
			return version.IndexOf("basic", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		//////////////////////////////////////////////////////////////////////////
		// Video Codecs

		[DllImport(PluginName, EntryPoint="AVPMC_GetVideoCodecCount")]
		public static extern int GetVideoCodecCount();

		[DllImport(PluginName, EntryPoint="AVPMC_IsConfigureVideoCodecSupported")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsConfigureVideoCodecSupported(int codecIndex);

		[DllImport(PluginName, EntryPoint="AVPMC_GetVideoCodecMediaApi")]
		public static extern MediaApi GetVideoCodecMediaApi(int codecIndex);

		[DllImport(PluginName, EntryPoint="AVPMC_ConfigureVideoCodec")]
		public static extern void ConfigureVideoCodec(int codecIndex);

		public static string GetVideoCodecName(int codecIndex)
		{
			string result = "Invalid";
			StringBuilder nameBuffer = new StringBuilder(256);
			if (GetVideoCodecName(codecIndex, nameBuffer, nameBuffer.Capacity))
			{
				result = nameBuffer.ToString();
			}
			return result;
		}

		//////////////////////////////////////////////////////////////////////////
		// Audio Codecs

		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioCodecCount")]
		public static extern int GetAudioCodecCount();

		[DllImport(PluginName, EntryPoint="AVPMC_IsConfigureAudioCodecSupported")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsConfigureAudioCodecSupported(int codecIndex);

		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioCodecMediaApi")]
		public static extern MediaApi GetAudioCodecMediaApi(int codecIndex);

		[DllImport(PluginName, EntryPoint="AVPMC_ConfigureAudioCodec")]
		public static extern void ConfigureAudioCodec(int codecIndex);

		public static string GetAudioCodecName(int codecIndex)
		{
			string result = "Invalid";
			StringBuilder nameBuffer = new StringBuilder(256);
			if (GetAudioCodecName(codecIndex, nameBuffer, nameBuffer.Capacity))
			{
				result = nameBuffer.ToString();
			}
			return result;
		}

		//////////////////////////////////////////////////////////////////////////
		// Audio Devices

		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioInputDeviceCount")]
		public static extern int GetAudioInputDeviceCount();

		public static string GetAudioInputDeviceName(int index)
		{
			string result = "Invalid";
			StringBuilder nameBuffer = new StringBuilder(256);
			if (GetAudioInputDeviceName(index, nameBuffer, nameBuffer.Capacity))
			{
				result = nameBuffer.ToString();
			}
			return result;
		}

#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioInputDeviceMediaApi")]
		public static extern MediaApi GetAudioInputDeviceMediaApi(int index);
#else
		public static MediaApi GetAudioInputDeviceMediaApi(int index)
		{
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
			return MediaApi.AVFoundation;
#elif UNITY_ANDROID && !UNITY_EDITOR
			return MediaApi.MediaCodec;
#else
			return MediaApi.Unknown;
#endif
		}
#endif

		//////////////////////////////////////////////////////////////////////////
		// Container Files

		public static string[] GetContainerFileExtensions(int videoCodecIndex, int audioCodecIndex = -1)
		{
			string[] result = new string[0];
			StringBuilder extensionsBuffer = new StringBuilder(256);
			if (GetContainerFileExtensions(videoCodecIndex, audioCodecIndex, extensionsBuffer, extensionsBuffer.Capacity))
			{
				result = extensionsBuffer.ToString().Split(new char[] {','});
			}
			return result;
		}

		//////////////////////////////////////////////////////////////////////////
		// Create the Recorder
		[DllImport(PluginName, EntryPoint="AVPMC_CreateRecorderVideo")]
		public static extern int CreateRecorderVideo([MarshalAs(UnmanagedType.LPWStr)] string filename, uint width, uint height, float frameRate, int format,
												[MarshalAs(UnmanagedType.U1)] bool isRealTime, [MarshalAs(UnmanagedType.U1)] bool isTopDown, int videoCodecIndex,
												AudioCaptureSource audioSource, int audioSampleRate, int audioChannelCount, int audioInputDeviceIndex, int audioCodecIndex,
												[MarshalAs(UnmanagedType.U1)] bool forceGpuFlush, VideoEncoderHints hints);
		[DllImport(PluginName, EntryPoint="AVPMC_CreateRecorderImages")]
		public static extern int CreateRecorderImages([MarshalAs(UnmanagedType.LPWStr)] string filename, uint width, uint height, float frameRate, int format,
												[MarshalAs(UnmanagedType.U1)] bool isRealTime, [MarshalAs(UnmanagedType.U1)] bool isTopDown, int imageFormatType, [MarshalAs(UnmanagedType.U1)] bool forceGpuFlush, int startFrame, ImageEncoderHints hints);

		[DllImport(PluginName, EntryPoint="AVPMC_CreateRecorderPipe")]
		public static extern int CreateRecorderPipe([MarshalAs(UnmanagedType.LPWStr)] string filename, uint width, uint height, float frameRate, int format,
												[MarshalAs(UnmanagedType.U1)] bool isTopDown, [MarshalAs(UnmanagedType.U1)] bool supportAlpha, [MarshalAs(UnmanagedType.U1)] bool forceGpuFlush);

		//////////////////////////////////////////////////////////////////////////
		// Update recorder

		[DllImport(PluginName, EntryPoint="AVPMC_Start")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool Start(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_IsNewFrameDue")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsNewFrameDue(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_SetEncodedFrameLimit")]
		public static extern int SetEncodedFrameLimit(int handle, uint encodedFrameLimit);

		[DllImport(PluginName, EntryPoint="AVPMC_EncodeFrame")]
		public static extern void EncodeFrame(int handle, System.IntPtr data);

		[DllImport(PluginName, EntryPoint="AVPMC_EncodeAudio")]
		public static extern void EncodeAudio(int handle, System.IntPtr data, uint length);

		[DllImport(PluginName, EntryPoint="AVPMC_EncodeFrameWithAudio")]
		public static extern void EncodeFrameWithAudio(int handle, System.IntPtr videoData, System.IntPtr audioData, uint audioLength);

		[DllImport(PluginName, EntryPoint="AVPMC_Pause")]
		public static extern void Pause(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_Stop")]
		public static extern void Stop(int handle, [MarshalAs(UnmanagedType.U1)] bool skipPendingFrames);

		[DllImport(PluginName, EntryPoint="AVPMC_IsFileWritingComplete")]
		[return: MarshalAs(UnmanagedType.U1)]
		public static extern bool IsFileWritingComplete(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_SetTexturePointer")]
		public static extern void SetTexturePointer(int handle, System.IntPtr texture);

#if false

		[DllImport(PluginName, EntryPoint="AVPMC_SetColourBuffer")]
		public static extern void SetColourBuffer(int handle, System.IntPtr buffer);

#endif

		//////////////////////////////////////////////////////////////////////////
		// Destroy recorder

		[DllImport(PluginName, EntryPoint="AVPMC_FreeRecorder")]
		public static extern void FreeRecorder(int handle);

		//////////////////////////////////////////////////////////////////////////
		// Debugging

		[DllImport(PluginName, EntryPoint="AVPMC_GetNumDroppedFrames")]
		public static extern uint GetNumDroppedFrames(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_GetNumDroppedEncoderFrames")]
		public static extern uint GetNumDroppedEncoderFrames(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_GetNumEncodedFrames")]
		public static extern uint GetNumEncodedFrames(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_GetEncodedSeconds")]
		public static extern uint GetEncodedSeconds(int handle);

		[DllImport(PluginName, EntryPoint="AVPMC_GetFileSize")]
		public static extern uint GetFileSize(int handle);

		//////////////////////////////////////////////////////////////////////////
		// Private internal functions

		[DllImport(PluginName, EntryPoint="AVPMC_GetPluginVersion")]
		private static extern System.IntPtr GetPluginVersion();

		[DllImport(PluginName, EntryPoint="AVPMC_GetVideoCodecName")]
		[return: MarshalAs(UnmanagedType.U1)]
		private static extern bool GetVideoCodecName(int index, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int nameBufferLength);

		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioCodecName")]
		[return: MarshalAs(UnmanagedType.U1)]
		private static extern bool GetAudioCodecName(int index, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int nameBufferLength);

		[DllImport(PluginName, EntryPoint="AVPMC_GetAudioInputDeviceName")]
		[return: MarshalAs(UnmanagedType.U1)]
		private static extern bool GetAudioInputDeviceName(int index, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int nameBufferLength);

		[DllImport(PluginName, EntryPoint="AVPMC_GetContainerFileExtensions")]
		[return: MarshalAs(UnmanagedType.U1)]
		private static extern bool GetContainerFileExtensions(int videoCodecIndex, int audioCodecIndex, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder extensions, int extensionsBufferLength);

		//////////////////////////////////////////////////////////////////////////
		// Logging

		[DllImport(PluginName, EntryPoint="AVPMC_SetLogFunction")]
		public static extern void SetLogFunction(System.IntPtr fn);

		//////////////////////////////////////////////////////////////////////////
		// Error reporting

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void ErrorHandlerDelegate(int handle, int domain, int code, [In, MarshalAs(UnmanagedType.LPWStr)] string message);

		[DllImport(PluginName, EntryPoint="AVPMC_SetErrorHandler")]
		public static extern void SetErrorHandler(int handle, System.IntPtr handler);

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR)

		//////////////////////////////////////////////////////////////////////////
		// Audio capture authorisation

		[DllImport(PluginName, EntryPoint = "AVPMC_AudioCaptureDeviceAuthorisationStatus")]
		public static extern int AudioCaptureDeviceAuthorisationStatus();

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void RequestAudioCaptureDeviceAuthorisationDelegate(int authorisation);

		[DllImport(PluginName, EntryPoint = "AVPMC_RequestAudioCaptureDeviceAuthorisation")]
		public static extern void RequestAudioCaptureDeviceAuthorisation(System.IntPtr callback);

		//////////////////////////////////////////////////////////////////////////
		// Photo library authorisation

		[DllImport(PluginName, EntryPoint="AVPMC_PhotoLibraryAuthorisationStatus")]
		public static extern int PhotoLibraryAuthorisationStatus(int level);

		[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
		public delegate void RequestPhotoLibraryAuthorisationDelegate(int authorisation);

		[DllImport(PluginName, EntryPoint = "AVPMC_RequestPhotoLibraryAuthorisation")]
		public static extern void RequestPhotoLibraryAuthorisation(int level, System.IntPtr callback);

#endif
	}
}