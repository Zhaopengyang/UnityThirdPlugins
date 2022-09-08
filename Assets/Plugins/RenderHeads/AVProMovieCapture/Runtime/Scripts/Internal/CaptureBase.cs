#if UNITY_2017_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
#endif
#if UNITY_5_6_OR_NEWER && UNITY_2018_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
#endif
#if UNITY_2017_1_OR_NEWER
	#define AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
#endif
#if UNITY_2019_2_OR_NEWER
	#define AVPRO_MOVIECAPTURE_CAPTUREDELTA_SUPPORT
#endif
#if UNITY_2018_1_OR_NEWER
	#define UNITY_NATIVEARRAY_UNSAFE_SUPPORT
#endif
#if ENABLE_IL2CPP
using AOT;
#endif
#if UNITY_2018_1_OR_NEWER
using Unity.Collections;
#else
using UnityEngine.Collections;
#endif
using UnityEngine;
using System.IO;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Live stats about an active capture session
	/// </summary>
	public class CaptureStats
	{
		public float FPS { get { return _fps; } }
		public float FramesTotal { get { return _frameTotal; } }

		public uint NumDroppedFrames { get { return _numDroppedFrames; } internal set { _numDroppedFrames = value; } }
		public uint NumDroppedEncoderFrames { get { return _numDroppedEncoderFrames; } internal set { _numDroppedEncoderFrames = value; } }
		public uint NumEncodedFrames { get { return _numEncodedFrames; } internal set { _numEncodedFrames = value; } }
		public uint TotalEncodedSeconds { get { return _totalEncodedSeconds; } internal set { _totalEncodedSeconds = value; } }

		public AudioCaptureSource AudioCaptureSource { get { return _audioCaptureSource; } internal set { _audioCaptureSource = value; } }
		public int UnityAudioSampleRate { get { return _unityAudioSampleRate; } internal set { _unityAudioSampleRate = value; } }
		public int UnityAudioChannelCount { get { return _unityAudioChannelCount; } internal set { _unityAudioChannelCount = value; } }

		// Frame stats
		private uint _numDroppedFrames = 0;
		private uint _numDroppedEncoderFrames = 0;
		private uint _numEncodedFrames = 0;
		private uint _totalEncodedSeconds = 0;

		// Audio
		private AudioCaptureSource _audioCaptureSource = AudioCaptureSource.None;
		private int _unityAudioSampleRate = -1;
		private int _unityAudioChannelCount = -1;

		// Capture rate
		private float _fps = 0f;
		private int _frameTotal = 0;
		private int _frameCount = 0;
		private float _startFrameTime = 0f;

		internal void ResetFPS()
		{
			_frameCount = 0;
			_frameTotal = 0;
			_fps = 0.0f;
			_startFrameTime = 0.0f;
		}

		internal void UpdateFPS()
		{
			_frameCount++;
			_frameTotal++;

			float timeNow = Time.realtimeSinceStartup;
			float timeDelta = timeNow - _startFrameTime;
			if (timeDelta >= 1.0f)
			{
				_fps = (float)_frameCount / timeDelta;
				_frameCount = 0;
				_startFrameTime = timeNow;
			}
		}
	}

	[System.Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class VideoEncoderHints
	{
		public VideoEncoderHints()
		{
			SetDefaults();
		}

		public void SetDefaults()
		{
			averageBitrate = 0;
			maximumBitrate = 0;
			quality = 1.0f;
			keyframeInterval = 0;
			allowFastStartStreamingPostProcess = true;
			supportTransparency = false;
			useHardwareEncoding = true;
			injectStereoPacking = NoneAutoCustom.Auto;
			stereoPacking = StereoPacking.None;
			injectSphericalVideoLayout = NoneAutoCustom.Auto;
			sphericalVideoLayout = SphericalVideoLayout.None;
			enableFragmentedWriting = false;
			movieFragmentInterval = 120;	// Default to two minutes
			colourSpace = ColourSpace.Unknown;
			sourceWidth = 0;
			sourceHeight = 0;
			androidNoCaptureRotation = false;
		}

		internal void Validate()
		{
			quality = Mathf.Clamp(quality, 0f, 1f);
		}

		[Tooltip("Average number of bits per second for the resulting video. Zero uses the codec defaults.")]
		public uint averageBitrate;
		[Tooltip("Maximum number of bits per second for the resulting video. Zero uses the codec defaults.")]
		public uint maximumBitrate;
		[Range(0f, 1f)] public float quality;
		[Tooltip("How often a keyframe is inserted.  Zero uses the codec defaults.")]
		public uint keyframeInterval;

		// Only for MOV / MP4 files
		[Tooltip("Move the 'moov' atom in the video file from the end to the start of the file to make streaming start fast.  Also known as 'Fast Start' in some encoders")]
		[MarshalAs(UnmanagedType.U1)]
		public bool allowFastStartStreamingPostProcess;

		// Currently only for HEVC and ProRes 4444 on macOS/iOS, and supported DirectShow codecs (eg Lagarith/Uncompressed) on Windows
		[Tooltip("Hints to the encoder to use the alpha channel for transparency if possible")]
		[MarshalAs(UnmanagedType.U1)]
		public bool supportTransparency;

		// Windows only (on macOS/iOS hardware is always used if available)
		[MarshalAs(UnmanagedType.U1)]
		public bool useHardwareEncoding;

		// Only for MP4 files on Windows
		[Tooltip("Inject atoms to define stereo video mode")]
		public NoneAutoCustom injectStereoPacking;

		[Tooltip("Inject atoms to define stereo video mode")]
		public StereoPacking stereoPacking;

		// Only for MP4 files on Windows
		[Tooltip("Inject atoms to define spherical video layout")]
		public NoneAutoCustom injectSphericalVideoLayout;

		[Tooltip("Inject atoms to define spherical video layout")]
		public SphericalVideoLayout sphericalVideoLayout;

		// Support fragmented writing of mov/mp4 on macOS/iOS
		[Tooltip("Enable fragmented writing support for QuickTime (mov, mp4) files")]
		[MarshalAs(UnmanagedType.U1)]
		public bool enableFragmentedWriting;

		// Not sure of sensible uppoer/lower bounds for this
		[Tooltip("The interval at which to write movie fragments in seconds")]
		[Range(0f, 300f)]
		public double movieFragmentInterval;

		public enum ColourSpace : int { Unknown = -1, Gamma = 0, Linear = 1 };
		public ColourSpace colourSpace;

		// The width and height of the source
		public int sourceWidth;
		public int sourceHeight;

		public bool androidNoCaptureRotation;
	}

	[System.Serializable]
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public class ImageEncoderHints
	{
		public ImageEncoderHints()
		{
			SetDefaults();
		}

		public void SetDefaults()
		{
			quality = 0.85f;
			supportTransparency = false;
			colourSpace = ColourSpace.Unknown;
			sourceWidth = 0;
			sourceHeight = 0;
		}

		internal void Validate()
		{
			quality = Mathf.Clamp(quality, 0f, 1f);
		}

		// Currently only affects JPG and HEIF formats (macOS only)
		[Range(0f, 1f)] public float quality;

		// Currently only for PNG
		[Tooltip("Hints to the encoder to use the alpha channel for transparency if possible")]
		[MarshalAs(UnmanagedType.U1)]
		public bool supportTransparency;

		public enum ColourSpace : int { Unknown = -1, Gamma = 0, Linear = 1 };
		public ColourSpace colourSpace;

		// The width and height of the source
		public int sourceWidth;
		public int sourceHeight;
	}

	[System.Serializable]
	public class EncoderHints
	{
		public EncoderHints()
		{
			SetDefaults();
		}

		public void SetDefaults()
		{
			videoHints = new VideoEncoderHints();
			imageHints = new ImageEncoderHints();
		}

		public VideoEncoderHints videoHints;
		public ImageEncoderHints imageHints;
	}

	/// <summary>
	/// Base class wrapping common capture functionality
	/// </summary>
	public partial class CaptureBase : MonoBehaviour
	{
		private const string DocEditionsURL = "https://www.renderheads.com/content/docs/AVProMovieCapture/articles/download.html#editions";

		public enum Resolution
		{
			POW2_8192x8192,
			POW2_8192x4096,
			POW2_4096x4096,
			POW2_4096x2048,
			POW2_2048x4096,
			UHD_3840x2160,
			UHD_3840x2048,
			UHD_3840x1920,
			UHD_2560x1440,
			POW2_2048x2048,
			POW2_2048x1024,
			HD_1920x1080,
			HD_1280x720,
			SD_1024x768,
			SD_800x600,
			SD_800x450,
			SD_640x480,
			SD_640x360,
			SD_320x240,
			Original,
			Custom,
		}

		public enum CubemapDepth
		{
			Depth_24 = 24,
			Depth_16 = 16,
			Depth_Zero = 0,
		}

		public enum CubemapResolution
		{
			POW2_8192 = 8192,
			POW2_4096 = 4096,
			POW2_2048 = 2048,
			POW2_1024 = 1024,
			POW2_512 = 512,
			POW2_256 = 256,
		}

		public enum AntiAliasingLevel
		{
			UseCurrent,
			ForceNone,
			ForceSample2,
			ForceSample4,
			ForceSample8,
		}

		public enum DownScale
		{
			Original = 1,
			Half = 2,
			Quarter = 4,
			Eighth = 8,
			Sixteenth = 16,
			Custom = 100,
		}

		public enum OutputPath
		{
			RelativeToProject,
			RelativeToPeristentData,
			Absolute,
			RelativeToDesktop,
			RelativeToPictures,
			RelativeToVideos,
			PhotoLibrary,
			RelativeToTemporaryCachePath
		}

		public enum FrameUpdateMode
		{
			Automatic,
			Manual,
		}

		/*public enum OutputExtension
		{
			AVI,
			MP4,
			PNG,
			Custom = 100,
		}*/

#if false
		[System.Serializable]
		public class WindowsPostCaptureSettings
		{
			[SerializeField]
			[Tooltip("Move the 'moov' atom in the MP4 file from the end to the start of the file to make streaming start fast.  Also called 'Fast Start' in some encoders")]
			public bool writeFastStartStreamingForMp4 = true;
		}

		[System.Serializable]
		public class PostCaptureSettings
		{
			[SerializeField]
			[Tooltip("Move the 'moov' atom in the MP4 file from the end to the start of the file to make streaming start fast.  Also called 'Fast Start' in some encoders")]
			public WindowsPostCaptureSettings windows = new WindowsPostCaptureSettings();
		}

		[SerializeField] PostCaptureSettings _postCaptureSettings = new PostCaptureSettings();
#endif

		[SerializeField] EncoderHints _encoderHintsWindows = new EncoderHints();
		[SerializeField] EncoderHints _encoderHintsMacOS = new EncoderHints();
		[SerializeField] EncoderHints _encoderHintsIOS = new EncoderHints();
		[SerializeField] EncoderHints _encoderHintsAndroid = new EncoderHints();

		// General options

		[SerializeField] KeyCode _captureKey = KeyCode.None;
		[SerializeField] bool _isRealTime = true;
		[SerializeField] bool _persistAcrossSceneLoads = false;

		// Start options

		[SerializeField] StartTriggerMode _startTrigger = StartTriggerMode.Manual;
		[SerializeField] StartDelayMode _startDelay = StartDelayMode.None;
		[SerializeField] float _startDelaySeconds = 0f;

		// Stop options

		[SerializeField] StopMode _stopMode = StopMode.None;
		// TODO: add option to pause instead of stop?
		[SerializeField] int _stopFrames = 0;
		[SerializeField] float _stopSeconds = 0f;

		// Video options

		public static readonly string[] DefaultVideoCodecPriorityWindows = { 	"H264",
																				"HEVC",
																				"Lagarith Lossless Codec",
																				"Uncompressed",
																				"x264vfw - H.264/MPEG-4 AVC codec",
																				"Xvid MPEG-4 Codec" };

		public static readonly string[] DefaultVideoCodecPriorityMacOS =  {		"H264",
																				"HEVC",
																				"Apple ProRes 422",
																				"Apple ProRes 4444" };

		public static readonly string[] DefaultVideoCodecPriorityAndroid =  {	"H264",
																				"HEVC"/*,
																				"VP8",
																				"VP9"*/ };

		public static readonly string[] DefaultAudioCodecPriorityWindows = {	"AAC",
																				"FLAC",
																				"Uncompressed" };

		public static readonly string[] DefaultAudioCodecPriorityMacOS =  {	"AAC",
																			"FLAC",
																			"Apple Lossless",
																			"Linear PCM",
																			"Uncompresssed" };

		public static readonly string[] DefaultAudioCodecPriorityIOS =  {	"AAC",
																			"FLAC",
																			"Apple Lossless",
																			"Linear PCM",
																			"Uncompresssed" };

		public static readonly string[] DefaultAudioCodecPriorityAndroid =  {"AAC"/*,
																			"FLAC",
																			"OPUS"*/};

		public static readonly string[] DefaultAudioCaptureDevicePriorityWindow = { "Microphone (Realtek Audio)", "Stereo Mix", "What U Hear", "What You Hear", "Waveout Mix", "Mixed Output" };
		public static readonly string[] DefaultAudioCaptureDevicePriorityMacOS = { };
		public static readonly string[] DefaultAudioCaptureDevicePriorityIOS = { };
		public static readonly string[] DefaultAudioCaptureDevicePriorityAndroid = { };

		[SerializeField] string[] _videoCodecPriorityWindows = DefaultVideoCodecPriorityWindows;
		[SerializeField] string[] _videoCodecPriorityMacOS = DefaultVideoCodecPriorityMacOS;
		#pragma warning disable 0414		// "is assigned but its value is never used"
		[SerializeField] string[] _videoCodecPriorityAndroid = DefaultVideoCodecPriorityAndroid;
		#pragma warning restore 0414

		[SerializeField] string[] _audioCodecPriorityWindows = DefaultAudioCodecPriorityWindows;
		[SerializeField] string[] _audioCodecPriorityMacOS = DefaultAudioCodecPriorityMacOS;
		#pragma warning disable 0414		// "is assigned but its value is never used"
		[SerializeField] string[] _audioCodecPriorityAndroid = DefaultAudioCodecPriorityAndroid;
		#pragma warning restore 0414

		[SerializeField] float _frameRate = 30f;

		[Tooltip("Timelapse scale makes the frame capture run at a fraction of the target frame rate.  Default value is 1")]
		[SerializeField] int _timelapseScale = 1;
		[Tooltip("Manual update mode requires user to call FrameUpdate() each time a frame is ready")]
		[SerializeField] FrameUpdateMode _frameUpdateMode = FrameUpdateMode.Automatic;

		[SerializeField] DownScale _downScale = DownScale.Original;
		[SerializeField] Vector2 _maxVideoSize = Vector2.zero;

		#pragma warning disable 414
		[SerializeField, Range(-1, 128)] int _forceVideoCodecIndexWindows = -1;
		[SerializeField, Range(-1, 128)] int _forceVideoCodecIndexMacOS = 0;
		[SerializeField, Range(0, 128)] int _forceVideoCodecIndexIOS = 0;
		[SerializeField, Range(0, 128)] int _forceVideoCodecIndexAndroid = 0;
		[SerializeField, Range(-1, 128)] int _forceAudioCodecIndexWindows = -1;
		[SerializeField, Range(-1, 128)] int _forceAudioCodecIndexMacOS = 0;
		[SerializeField, Range(0, 128)] int _forceAudioCodecIndexIOS = 0;
		[SerializeField, Range(0, 128)] int _forceAudioCodecIndexAndroid = -1;
		#pragma warning restore 414
		[SerializeField] bool _flipVertically = false;

		[Tooltip("Flushing the GPU during each capture results in less latency, but can slow down rendering performance for complex scenes.")]
		[SerializeField] bool _forceGpuFlush = false;

		[Tooltip("This option can help issues where skinning is used, or other animation/rendering effects that only complete later in the frame.")]
		[SerializeField] protected bool _useWaitForEndOfFrame = true;

		[Tooltip("Portrait captures may be rotated 90° to better utilise the encoder, check this to disable the rotation at the risk of not being able to capture the full vertical resolution.")]
		[SerializeField] bool _androidNoCaptureRotation = false;

		[Tooltip("Log the start and stop of the capture.  Disable this for less garbage generation.")]
		[SerializeField] bool _logCaptureStartStop = true;

		// Audio options

		[SerializeField] AudioCaptureSource _audioCaptureSource = AudioCaptureSource.None;
		[SerializeField] UnityAudioCapture _unityAudioCapture = null;
		[SerializeField, Range(0, 32)] int _forceAudioInputDeviceIndex = 0;
		[SerializeField, Range(8000, 96000)] int _manualAudioSampleRate = 48000;
		[SerializeField, Range(1, 8)] int _manualAudioChannelCount = 2;

		// Output options

		[SerializeField] protected OutputTarget _outputTarget = OutputTarget.VideoFile;

		public OutputTarget OutputTarget
		{
			get { return _outputTarget; }
			set { _outputTarget = value; }
		}

		public const OutputPath DefaultOutputFolderType = OutputPath.RelativeToProject;
		private const string DefaultOutputFolderPath = "Captures";

		[SerializeField] OutputPath _outputFolderType = DefaultOutputFolderType;
		[SerializeField] string _outputFolderPath = DefaultOutputFolderPath;
		[SerializeField] string _filenamePrefix = "MovieCapture";
		[SerializeField] bool _appendFilenameTimestamp = true;
		[SerializeField] bool _allowManualFileExtension = false;
		[SerializeField] string _filenameExtension = "mp4";
		[SerializeField] string _namedPipePath = @"\\.\pipe\test_pipe";

		public OutputPath OutputFolder
		{
			get { return _outputFolderType; }
			set { _outputFolderType = value; }
		}
		public string OutputFolderPath
		{
			get { return _outputFolderPath; }
			set { _outputFolderPath = value; }
		}
		public string FilenamePrefix
		{
			get { return _filenamePrefix; }
			set { _filenamePrefix = value; }
		}
		public bool AppendFilenameTimestamp
		{
			get { return _appendFilenameTimestamp; }
			set { _appendFilenameTimestamp = value; }
		}
		public bool AllowManualFileExtension
		{
			get { return _allowManualFileExtension; }
			set { _allowManualFileExtension = value; }
		}
		public string FilenameExtension
		{
			get { return _filenameExtension; }
			set { _filenameExtension = value; }
		}
		public string NamedPipePath
		{
			get { return _namedPipePath; }
			set { _namedPipePath = value; }
		}

		[SerializeField] int _imageSequenceStartFrame = 0;
		[SerializeField, Range(2, 12)] int _imageSequenceZeroDigits = 6;
		#pragma warning disable 414
		[SerializeField] ImageSequenceFormat _imageSequenceFormatWindows = ImageSequenceFormat.PNG;
		[SerializeField] ImageSequenceFormat _imageSequenceFormatMacOS = ImageSequenceFormat.PNG;
		[SerializeField] ImageSequenceFormat _imageSequenceFormatIOS = ImageSequenceFormat.PNG;
		[SerializeField] ImageSequenceFormat _imageSequenceFormatAndroid = ImageSequenceFormat.PNG;
		#pragma warning restore 414

		public int ImageSequenceStartFrame
		{
			get { return _imageSequenceStartFrame; }
			set { _imageSequenceStartFrame = value; }
		}
		public int ImageSequenceZeroDigits
		{
			get { return _imageSequenceZeroDigits; }
			set { _imageSequenceZeroDigits = Mathf.Clamp(_imageSequenceZeroDigits, 2, 12); }
		}

		// Camera specific options

		[SerializeField] protected Resolution _renderResolution = Resolution.Original;
		[SerializeField] protected Vector2 _renderSize = Vector2.one;
		[SerializeField] protected int _renderAntiAliasing = -1;

		// Motion blur options

		[SerializeField] protected bool _useMotionBlur = false;
		[SerializeField, Range(0, 64)] protected int _motionBlurSamples = 16;
		[SerializeField] protected Camera[] _motionBlurCameras = null;
		[SerializeField] protected MotionBlur _motionBlur;

		public bool UseMotionBlur
		{
			get { return _useMotionBlur; }
			set { _useMotionBlur = value; }
		}
		public int MotionBlurSamples
		{
			get { return _motionBlurSamples; }
			set { _motionBlurSamples = (int)Mathf.Clamp((float)value, 0f, 64f); }
		}
		public Camera[] MotionBlurCameras
		{
			get { return _motionBlurCameras; }
			set { _motionBlurCameras = value; }
		}
		public MotionBlur MotionBlur
		{
			get { return _motionBlur; }
			set { _motionBlur = value; }
		}

		// Performance options

		[SerializeField] bool _allowVSyncDisable = true;
		[SerializeField] protected bool _supportTextureRecreate = false;

		// Other options

		[SerializeField] int _minimumDiskSpaceMB = -1;

#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
		[SerializeField] TimelineController _timelineController = null;
#endif
#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
		[SerializeField] VideoPlayerController _videoPlayerController = null;
#endif

		//public bool _allowFrameRateChange = true;

		protected Texture2D _texture;
		protected int _handle = -1;
		protected int _sourceWidth, _sourceHeight;
		protected int _targetWidth, _targetHeight;
		protected bool _capturing = false;
		protected bool _paused = false;
		protected string _filePath;
		protected string _finalFilePath;
		protected FileInfo _fileInfo;
		protected NativePlugin.PixelFormat _pixelFormat = NativePlugin.PixelFormat.YCbCr422_YUY2;
		private Codec _selectedVideoCodec = null;
		private Codec _selectedAudioCodec = null;
		private Device _selectedAudioInputDevice = null;
		private int _oldVSyncCount = 0;
		//private int _oldTargetFrameRate = -1;
		private float _oldFixedDeltaTime = 0f;
		protected bool _isTopDown = true;
		protected bool _isDirectX11 = false;
		private bool _queuedStartCapture = false;
		private bool _queuedStopCapture = false;
		private float _captureStartTime = 0f;
		private float _capturePrePauseTotalTime = 0f;
		private float _timeSinceLastFrame = 0f;
		protected YieldInstruction _waitForEndOfFrame;
		private long _freeDiskSpaceMB;

		private float _startDelayTimer;
		private bool _startPaused;
		private System.Action<FileWritingHandler> _beginFinalFileWritingAction;
		private System.Action<FileWritingHandler> _completedFileWritingAction;
		private List<FileWritingHandler> _pendingFileWrites = new List<FileWritingHandler>(4);

		private static HashSet<string> _activeFilePaths = new HashSet<string>();
		public static HashSet<string> ActiveFilePaths
		{
			get { return _activeFilePaths; }
		}

		public string LastFilePath
		{
			get { return _filePath; }
		}

		// Register for notification of when the final file writing begins
		public System.Action<FileWritingHandler> BeginFinalFileWritingAction
		{
			get { return _beginFinalFileWritingAction; }
			set { _beginFinalFileWritingAction = value; }
		}
		
		// Register for notification of when the final file writing completes
		public System.Action<FileWritingHandler> CompletedFileWritingAction
		{ 
			get { return _completedFileWritingAction; }
			set { _completedFileWritingAction = value; }
		}

		// Stats
		private CaptureStats _stats = new CaptureStats();

		private static bool _isInitialised = false;
		private static bool _isApplicationQuiting = false;

		public Resolution CameraRenderResolution
		{
			get { return _renderResolution; }
			set { _renderResolution = value; }
		}
		public Vector2 CameraRenderCustomResolution
		{
			get { return _renderSize; }
			set { _renderSize = value; }
		}

		public int CameraRenderAntiAliasing
		{
			get { return _renderAntiAliasing; }
			set { _renderAntiAliasing = value; }
		}

		public bool IsRealTime
		{
			get { return _isRealTime; }
			set { _isRealTime = value; }
		}

		public bool PersistAcrossSceneLoads
		{
			get { return _persistAcrossSceneLoads; }
			set { _persistAcrossSceneLoads = value; }
		}

		public AudioCaptureSource AudioCaptureSource
		{
			get { return _audioCaptureSource; }
			set { _audioCaptureSource = value; }
		}

		public int ManualAudioSampleRate
		{
			get { return _manualAudioSampleRate; }
			set { _manualAudioSampleRate = value; }
		}

		public int ManualAudioChannelCount
		{
			get { return _manualAudioChannelCount; }
			set { _manualAudioChannelCount = value; }
		}

		public UnityAudioCapture UnityAudioCapture
		{
			get { return _unityAudioCapture; }
			set { _unityAudioCapture = value; }
		}

		public int ForceAudioInputDeviceIndex
		{
			get { return _forceAudioInputDeviceIndex; }
			set { _forceAudioInputDeviceIndex = value; SelectAudioInputDevice(); }
		}

		public float FrameRate
		{
			get { return _frameRate; }
			set { _frameRate = Mathf.Clamp(value, 0.01f, 240f); }
		}

		public StartTriggerMode StartTrigger
		{
			get { return _startTrigger; }
			set { _startTrigger = value; }
		}

		public StartDelayMode StartDelay
		{
			get { return _startDelay; }
			set { _startDelay = value; }
		}

		public float StartDelaySeconds
		{
			get { return _startDelaySeconds; }
			set { _startDelaySeconds =  Mathf.Max(0f, value); }
		}

		public StopMode StopMode
		{
			get { return _stopMode; }
			set { _stopMode = value; }
		}

		public int StopAfterFramesElapsed
		{
			get { return _stopFrames; }
			set { _stopFrames = Mathf.Max(0, value); }
		}

		public float StopAfterSecondsElapsed
		{
			get { return _stopSeconds; }
			set { _stopSeconds = Mathf.Max(0f, value); }
		}

		public CaptureStats CaptureStats
		{
			get { return _stats; }
		}

		public string[] VideoCodecPriorityWindows
		{
			get { return _videoCodecPriorityWindows; }
			set { _videoCodecPriorityWindows = value; SelectVideoCodec(false); }
		}

		public string[] VideoCodecPriorityMacOS
		{
			get { return _videoCodecPriorityMacOS; }
			set { _videoCodecPriorityMacOS = value; SelectVideoCodec(false); }
		}

		public string[] AudioCodecPriorityWindows
		{
			get { return _audioCodecPriorityWindows; }
			set { _audioCodecPriorityWindows = value; SelectAudioCodec(); }
		}

		public string[] AudioCodecPriorityMacOS
		{
			get { return _audioCodecPriorityMacOS; }
			set { _audioCodecPriorityMacOS = value; SelectAudioCodec(); }
		}

		public int TimelapseScale
		{
			get { return _timelapseScale; }
			set { _timelapseScale = value; }
		}

		public FrameUpdateMode FrameUpdate
		{
			get { return _frameUpdateMode; }
			set { _frameUpdateMode = value; }
		}

		public DownScale ResolutionDownScale
		{
			get { return _downScale; }
			set { _downScale = value; }
		}

		public Vector2 ResolutionDownscaleCustom
		{
			get { return _maxVideoSize; }
			set { _maxVideoSize = value; }
		}

		public bool FlipVertically
		{
			get { return _flipVertically; }
			set { _flipVertically = value; }
		}

		public bool UseWaitForEndOfFrame
		{
			get { return _useWaitForEndOfFrame; }
			set { _useWaitForEndOfFrame = value; }
		}

		public bool LogCaptureStartStop
		{
			get { return _logCaptureStartStop; }
			set { _logCaptureStartStop = value; }
		}

#if false
		public PostCaptureSettings PostCapture
		{
			get { return _postCaptureSettings; }
		}
#endif

		public bool AllowOfflineVSyncDisable
		{
			get { return _allowVSyncDisable; }
			set { _allowVSyncDisable = value; }
		}

		public bool SupportTextureRecreate
		{
			get { return _supportTextureRecreate; }
			set { _supportTextureRecreate = value; }
		}

		#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
		public TimelineController TimelineController
		{
			get { return _timelineController; }
			set { _timelineController = value; }
		}
		#endif

		#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
		public VideoPlayerController VideoPlayerController
		{
			get { return _videoPlayerController; }
			set { _videoPlayerController = value; }
		}
		#endif

		public Codec SelectedVideoCodec
		{
			get { return _selectedVideoCodec; }
		}

		public Codec SelectedAudioCodec
		{
			get { return _selectedAudioCodec; }
		}

		public Device SelectedAudioInputDevice
		{
			get { return _selectedAudioInputDevice; }
		}

		public int NativeForceVideoCodecIndex
		{
			#if UNITY_EDITOR
				#if UNITY_EDITOR_WIN
					get { return _forceVideoCodecIndexWindows; }
					set { _forceVideoCodecIndexWindows = value; }
				#elif UNITY_EDITOR_OSX
					get { return _forceVideoCodecIndexMacOS; }
					set { _forceVideoCodecIndexMacOS = value; }
				#else
					get { return -1; }
					set { }
				#endif
			#else
				#if UNITY_STANDALONE_WIN
					get { return _forceVideoCodecIndexWindows; }
					set { _forceVideoCodecIndexWindows = value; }
				#elif UNITY_STANDALONE_OSX
					get { return _forceVideoCodecIndexMacOS; }
					set { _forceVideoCodecIndexMacOS = value; }
				#elif UNITY_IOS
					get { return _forceVideoCodecIndexIOS; }
					set { _forceVideoCodecIndexIOS = value; }
				#elif UNITY_ANDROID
					get { return _forceVideoCodecIndexAndroid; }
					set { _forceVideoCodecIndexAndroid = value; }
				#else
					get { return -1; }
					set { }
				#endif
			#endif
		}

		public int NativeForceAudioCodecIndex
		{
			#if UNITY_EDITOR
				#if UNITY_EDITOR_WIN
					get { return _forceAudioCodecIndexWindows; }
					set { _forceAudioCodecIndexWindows = value; }
				#elif UNITY_EDITOR_OSX
					get { return _forceAudioCodecIndexMacOS; }
					set { _forceAudioCodecIndexMacOS = value; }
				#else
					get { return -1; }
					set { }
				#endif
			#else
				#if UNITY_STANDALONE_WIN
					get { return _forceAudioCodecIndexWindows; }
					set { _forceAudioCodecIndexWindows = value; }
				#elif UNITY_STANDALONE_OSX
					get { return _forceAudioCodecIndexMacOS; }
					set { _forceAudioCodecIndexMacOS = value; }
				#elif UNITY_IOS
					get { return _forceAudioCodecIndexIOS; }
					set { _forceAudioCodecIndexIOS = value; }
				#elif UNITY_ANDROID
					get { return _forceAudioCodecIndexAndroid; }
					set { _forceAudioCodecIndexAndroid = value; }
				#else
					get { return -1; }
					set { }
				#endif
			#endif
		}

		public ImageSequenceFormat NativeImageSequenceFormat
		{
			#if UNITY_EDITOR
				#if UNITY_EDITOR_WIN
					get { return _imageSequenceFormatWindows; }
					set { _imageSequenceFormatWindows = value; }
				#elif UNITY_EDITOR_OSX
					get { return _imageSequenceFormatMacOS; }
					set { _imageSequenceFormatMacOS = value; }
				#else
					get { return ImageSequenceFormat.PNG; }
					set { }
				#endif
			#else
				#if UNITY_STANDALONE_WIN
					get { return _imageSequenceFormatWindows; }
					set { _imageSequenceFormatWindows = value; }
				#elif UNITY_STANDALONE_OSX
					get { return _imageSequenceFormatMacOS; }
					set { _imageSequenceFormatMacOS = value; }
				#elif UNITY_IOS
					get { return _imageSequenceFormatIOS; }
					set { _imageSequenceFormatIOS = value; }
				#elif UNITY_ANDROID
					get { return _imageSequenceFormatAndroid; }
					set { _imageSequenceFormatAndroid = value; }
				#else
					get { return ImageSequenceFormat.PNG; }
					set { }
				#endif
			#endif
		}

		protected static NativePlugin.Platform GetCurrentPlatform()
		{
			NativePlugin.Platform result = NativePlugin.Platform.Unknown;
			#if UNITY_EDITOR
				#if UNITY_EDITOR_WIN
					result = NativePlugin.Platform.Windows;
				#elif UNITY_EDITOR_OSX
					result = NativePlugin.Platform.macOS;
				#endif
			#else
				#if UNITY_STANDALONE_WIN
					result = NativePlugin.Platform.Windows;
				#elif UNITY_STANDALONE_OSX
					result = NativePlugin.Platform.macOS;
				#elif UNITY_IOS
					result = NativePlugin.Platform.iOS;
				#elif UNITY_ANDROID
					result = NativePlugin.Platform.Android;
				#endif
			#endif
			return result;
		}

		public EncoderHints GetEncoderHints(NativePlugin.Platform platform = NativePlugin.Platform.Current)
		{
			EncoderHints result = null;

			if (platform == NativePlugin.Platform.Current)
			{
				platform = GetCurrentPlatform();
			}
			switch (platform)
			{
				case NativePlugin.Platform.Windows:
					result = _encoderHintsWindows;
					break;
				case NativePlugin.Platform.macOS:
					result = _encoderHintsMacOS;
					break;
				case NativePlugin.Platform.iOS:
					result = _encoderHintsIOS;
					break;
				case NativePlugin.Platform.Android:
					result = _encoderHintsAndroid;
					break;
			}
			return result;
		}

		public void SetEncoderHints(EncoderHints hints, NativePlugin.Platform platform = NativePlugin.Platform.Current)
		{
			if (platform == NativePlugin.Platform.Current)
			{
				platform = GetCurrentPlatform();
			}
			switch (platform)
			{
				case NativePlugin.Platform.Windows:
					_encoderHintsWindows = hints;
					break;
				case NativePlugin.Platform.macOS:
					_encoderHintsMacOS = hints;
					break;
				case NativePlugin.Platform.iOS:
					_encoderHintsIOS = hints;
					break;
				case NativePlugin.Platform.Android:
					_encoderHintsAndroid = hints;
					break;
			}
		}


#if UNITY_ANDROID && !UNITY_EDITOR
		protected static AndroidJavaObject s_ActivityContext = null;
		protected static AndroidJavaObject s_Interface = null;
#endif

		protected virtual void Awake()
		{
			if (!_isInitialised)
			{
#if UNITY_ANDROID && !UNITY_EDITOR
				// Get the activity context
				if (s_ActivityContext == null)
				{
					AndroidJavaClass activityClass = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
					if (activityClass != null)
					{
						s_ActivityContext = activityClass.GetStatic<AndroidJavaObject>("currentActivity");
					}
				}

				s_Interface = new AndroidJavaObject("com.renderheads.AVPro.MovieCapture.Manager");
				s_Interface.Call("SetContext", s_ActivityContext);
#endif

				try
				{
					string pluginVersionString = NativePlugin.GetPluginVersionString();

					// Check that the plugin version number is not too old
					if (!pluginVersionString.StartsWith(NativePlugin.ExpectedPluginVersion))
					{
						Debug.LogWarning("[AVProMovieCapture] Plugin version number " + pluginVersionString + " doesn't match the expected version number " + NativePlugin.ExpectedPluginVersion + ".  It looks like the plugin didn't upgrade correctly.  To resolve this please restart Unity and try to upgrade the package again.");
					}

		#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
			#if !UNITY_2017_1_OR_NEWER
					if (SystemInfo.graphicsDeviceVersion.StartsWith("Metal"))
					{
						Debug.LogError("[AVProMovieCapture] Metal is not supported below Unity 2017, please switch to OpenGLCore in Player Settings.");
						return;
					}
			#endif
		#elif !UNITY_EDITOR && UNITY_IOS && !UNITY_2017_1_OR_NEWER
					if (Application.isPlaying)
					{
						Debug.LogError("[AVProMovieCapture] iOS is not supported below Unity 2017.");
						return;
					}
		#endif
					if (NativePlugin.Init())
					{
						Debug.Log("[AVProMovieCapture] Init version: " + NativePlugin.ScriptVersion + " (plugin v" + pluginVersionString +") with GPU " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceVersion + " OS: " + SystemInfo.operatingSystem);
						_isInitialised = true;
					}
					else
					{
						Debug.LogError("[AVProMovieCapture] Failed to initialise plugin on version: " + NativePlugin.ScriptVersion + " (plugin v" + pluginVersionString + ") with GPU " + SystemInfo.graphicsDeviceName + " " + SystemInfo.graphicsDeviceVersion + " OS: " + SystemInfo.operatingSystem);
					}
				}
				catch (DllNotFoundException e)
				{
					string missingDllMessage = string.Empty;
					missingDllMessage = "Unity couldn't find the plugin DLL. Please select the native plugin files in 'Plugins/RenderHeads/AVProMovieCapture/Runtime/Plugins' folder and select the correct platform in the Inspector.";
					Debug.LogError("[AVProMovieCapture] " + missingDllMessage);
	#if UNITY_EDITOR
					UnityEditor.EditorUtility.DisplayDialog("Plugin files not found", missingDllMessage, "Ok");
	#endif
					throw e;
				}
			}

			_isDirectX11 = SystemInfo.graphicsDeviceVersion.StartsWith("Direct3D 11");

			SelectVideoCodec();
			SelectAudioCodec();
			SelectAudioInputDevice();

			if (_persistAcrossSceneLoads)
			{
				GameObject.DontDestroyOnLoad(this.gameObject);
			}
		}

		static CaptureBase()
		{
			#if UNITY_EDITOR
			SetupEditorPlayPauseSupport();
			#endif
		}

		public virtual void Start()
		{
			Application.runInBackground = true;
			_waitForEndOfFrame = new WaitForEndOfFrame();

			if (_startTrigger == StartTriggerMode.OnStart)
			{
				StartCapture();
			}
		}

		// Select the best codec based on criteria
		private static bool SelectCodec(ref Codec codec, CodecList codecList, int forceCodecIndex, string[] codecPriorityList, MediaApi matchMediaApi, bool allowFallbackToFirstCodec, bool logFallbackWarning)
		{
			codec = null;

			// The user has specified their own codec index
			if (forceCodecIndex >= 0)
			{
				if (forceCodecIndex < codecList.Count)
				{
					codec = codecList.Codecs[forceCodecIndex];
				}
			}
			else
			{
				// The user has specified an ordered list of codec name to search for
				if (codecPriorityList != null && codecPriorityList.Length > 0)
				{
					foreach (string codecName in codecPriorityList)
					{
						codec = codecList.FindCodec(codecName.Trim(), matchMediaApi);
						if (codec != null)
						{
							break;
						}
					}
				}
			}

			// If the found codec doesn't match the required MediaApi, set it to null
			if (codec != null && matchMediaApi != MediaApi.Unknown)
			{
				if (codec.MediaApi != matchMediaApi)
				{
					codec = null;
				}
			}

			// Fallback to the first codec
			if (codec == null && allowFallbackToFirstCodec)
			{
				if (codecList.Count > 0)
				{
					if (matchMediaApi != MediaApi.Unknown)
					{
						codec = codecList.GetFirstWithMediaApi(matchMediaApi);
					}
					else
					{
						codec = codecList.Codecs[0];
					}
					if (logFallbackWarning)
					{
						Debug.LogWarning("[AVProMovieCapture] Codec not found. Using the first codec available.");
					}
				}
			}

			return (codec != null);
		}

		public Codec SelectVideoCodec(bool isStartingCapture = false)
		{
			_selectedVideoCodec = null;
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			SelectCodec(ref _selectedVideoCodec, CodecManager.VideoCodecs, NativeForceVideoCodecIndex, _videoCodecPriorityWindows, MediaApi.Unknown, true, isStartingCapture);
#elif UNITY_EDITOR_OSX || (!UNITY_EDITOR && UNITY_STANDALONE_OSX)
			SelectCodec(ref _selectedVideoCodec, CodecManager.VideoCodecs, NativeForceVideoCodecIndex, _videoCodecPriorityMacOS, MediaApi.Unknown, true, isStartingCapture);
#elif !UNITY_EDITOR && UNITY_IOS
			SelectCodec(ref _selectedVideoCodec, CodecManager.VideoCodecs, NativeForceVideoCodecIndex, null, MediaApi.Unknown, true, isStartingCapture);
#elif !UNITY_EDITOR && UNITY_ANDROID
			SelectCodec(ref _selectedVideoCodec, CodecManager.VideoCodecs, NativeForceVideoCodecIndex, _videoCodecPriorityAndroid, MediaApi.Unknown, true, isStartingCapture);
#endif

			if (isStartingCapture && _selectedVideoCodec == null)
			{
				Debug.LogError("[AVProMovieCapture] Failed to select a suitable video codec");
			}
			return _selectedVideoCodec;
		}

		public Codec SelectAudioCodec()
		{
			_selectedAudioCodec = null;
			if (_audioCaptureSource != AudioCaptureSource.None)
			{
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
				// Audio codec selection requires a video codec to be selected first on Windows
				if (_selectedVideoCodec != null)
				{
					SelectCodec(ref _selectedAudioCodec, CodecManager.AudioCodecs, NativeForceAudioCodecIndex, _audioCodecPriorityWindows, _selectedVideoCodec.MediaApi, true, false);
				}
#elif UNITY_EDITOR_OSX || (!UNITY_EDITOR && UNITY_STANDALONE_OSX)
				SelectCodec(ref _selectedAudioCodec, CodecManager.AudioCodecs, NativeForceAudioCodecIndex, _audioCodecPriorityMacOS, MediaApi.Unknown, true, false);
#elif !UNITY_EDITOR && UNITY_IOS
				SelectCodec(ref _selectedAudioCodec, CodecManager.AudioCodecs, NativeForceAudioCodecIndex, null, MediaApi.Unknown, true, false);
#elif !UNITY_EDITOR && UNITY_ANDROID
				SelectCodec(ref _selectedAudioCodec, CodecManager.AudioCodecs, NativeForceAudioCodecIndex, _audioCodecPriorityAndroid, MediaApi.Unknown, true, false);
#endif

				if (_selectedAudioCodec == null)
				{
					//Debug.LogError("[AVProMovieCapture] Failed to select a suitable audio codec");
				}
			}
			return _selectedAudioCodec;
		}

		public Device SelectAudioInputDevice()
		{
			_selectedAudioInputDevice = null;
			if (_audioCaptureSource == AudioCaptureSource.Microphone)
			{
				// Audio input device selection requires a video codec to be selected first
				if (_selectedVideoCodec != null)
				{
					if (_forceAudioInputDeviceIndex >= 0 && _forceAudioInputDeviceIndex < DeviceManager.AudioInputDevices.Count)
					{
						_selectedAudioInputDevice = DeviceManager.AudioInputDevices.Devices[_forceAudioInputDeviceIndex];
					}

					// If the found codec doesn't match the required MediaApi, set it to null
					if (_selectedAudioInputDevice != null && _selectedAudioInputDevice.MediaApi != _selectedVideoCodec.MediaApi)
					{
						_selectedAudioInputDevice = null;
					}

					// Fallback to the first device
					if (_selectedAudioInputDevice == null)
					{
						if (DeviceManager.AudioInputDevices.Count > 0)
						{
							_selectedAudioInputDevice = DeviceManager.AudioInputDevices.GetFirstWithMediaApi(_selectedVideoCodec.MediaApi);
						}
					}
				}
			}
			return _selectedAudioInputDevice;
		}

		public static Vector2 GetRecordingResolution(int width, int height, DownScale downscale, Vector2 maxVideoSize)
		{
			int targetWidth = width;
			int targetHeight = height;
			if (downscale != DownScale.Custom)
			{
				targetWidth /= (int)downscale;
				targetHeight /= (int)downscale;
			}
			else
			{
				if (maxVideoSize.x >= 1.0f && maxVideoSize.y >= 1.0f)
				{
					targetWidth = Mathf.FloorToInt(maxVideoSize.x);
					targetHeight = Mathf.FloorToInt(maxVideoSize.y);
				}
			}

			// Some codecs like Lagarith in YUY2 mode need size to be multiple of 4
			targetWidth = NextMultipleOf4(targetWidth);
			targetHeight = NextMultipleOf4(targetHeight);

			return new Vector2(targetWidth, targetHeight);
		}

		public void SelectRecordingResolution(int width, int height)
		{
			_sourceWidth = width;
			_sourceHeight = height;
			_targetWidth = width;
			_targetHeight = height;
			if (_downScale != DownScale.Custom)
			{
				_targetWidth /= (int)_downScale;
				_targetHeight /= (int)_downScale;
			}
			else
			{
				if (_maxVideoSize.x >= 1.0f && _maxVideoSize.y >= 1.0f)
				{
					_targetWidth = Mathf.FloorToInt(_maxVideoSize.x);
					_targetHeight = Mathf.FloorToInt(_maxVideoSize.y);
				}
			}

			// Some codecs like Lagarith in YUY2 mode need size to be multiple of 4
			_targetWidth = NextMultipleOf4(_targetWidth);
			_targetHeight = NextMultipleOf4(_targetHeight);
		}

		public virtual void OnDestroy()
		{
			_waitForEndOfFrame = null;
			StopCapture(true, true);
			FreePendingFileWrites();

			// Make sure there are no other capture instances running and then deinitialise the plugin
			if (_isApplicationQuiting && _isInitialised)
			{
				// TODO: would it be faster to just look for _pendingFileWrites?
				bool anyCapturesRunning = false;
#if UNITY_EDITOR
				// In editor we have to search hidden objects as well, as the editor window components are created hidden
				CaptureBase[] captures = (CaptureBase[])Resources.FindObjectsOfTypeAll(typeof(CaptureBase));
#else
				CaptureBase[] captures = (CaptureBase[])Component.FindObjectsOfType(typeof(CaptureBase));
#endif
				foreach (CaptureBase capture in captures)
				{
					if (capture != null && capture.IsCapturing())
					{
						anyCapturesRunning = true;
						break;
					}
				}
				if (!anyCapturesRunning)
				{
					NativePlugin.Deinit();
					_isInitialised = false;
				}
			}
		}

		private void FreePendingFileWrites()
		{
			foreach (FileWritingHandler handler in _pendingFileWrites)
			{
				handler.Dispose();
			}
			_pendingFileWrites.Clear();

		}

		private void OnApplicationQuit()
		{
			_isApplicationQuiting = true;
		}

		private void OnApplicationPause(bool paused)
		{
#if UNITY_IOS && !UNITY_EDITOR
			if (paused && IsCapturing())
			{
				Debug.LogWarning("Application is being paused, stopping capture...");
				StopCapture();
			}
#endif
		}

		protected void EncodeTexture(Texture2D texture)
		{
			Color32[] bytes = texture.GetPixels32();
			GCHandle _frameHandle = GCHandle.Alloc(bytes, GCHandleType.Pinned);

			EncodePointer(_frameHandle.AddrOfPinnedObject());

			if (_frameHandle.IsAllocated)
			{
				_frameHandle.Free();
			}
		}

		protected bool IsUsingUnityAudioComponent()
		{
			if (_outputTarget == OutputTarget.VideoFile && _unityAudioCapture != null)
			{
				if (_audioCaptureSource == AudioCaptureSource.Unity
			#if !AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
					&& _isRealTime
			#endif
					)
				{
					return true;
				}
				#if AVPRO_MOVIECAPTURE_WWISE_SUPPORT
				else if (_audioCaptureSource == AudioCaptureSource.Wwise && !_isRealTime)
				{
					return true;
				}
				#endif
			}
			return false;
		}

		protected bool IsUsingMotionBlur()
		{
			return (_useMotionBlur && !_isRealTime && _motionBlur != null);
		}

		public virtual void EncodePointer(System.IntPtr ptr)
		{
			if (!IsUsingUnityAudioComponent())
			{
				NativePlugin.EncodeFrame(_handle, ptr);
			}
			else
			{
				int audioDataLength = 0;
				System.IntPtr audioDataPtr = _unityAudioCapture.ReadData(out audioDataLength);
				if (audioDataLength > 0)
				{
					NativePlugin.EncodeFrameWithAudio(_handle, ptr, audioDataPtr, (uint)audioDataLength);
				}
				else
				{
					NativePlugin.EncodeFrame(_handle, ptr);
				}
			}
		}

		public bool IsPrepared()
		{
			return (_handle >= 0);
		}

		public bool IsCapturing()
		{
			return _capturing;
		}

		public bool IsPaused()
		{
			return _paused;
		}

		public int GetRecordingWidth()
		{
			return _targetWidth;
		}

		public int GetRecordingHeight()
		{
			return _targetHeight;
		}

		protected virtual string GenerateTimestampedFilename(string filenamePrefix, string filenameExtension)
		{
			// TimeSpan span = (DateTime.Now - DateTime.Now.Date);
			// string filename = string.Format("{0}-{1}-{2}-{3}-{4}s-{5}x{6}", filenamePrefix, DateTime.Now.Year, DateTime.Now.Month.ToString("D2"), DateTime.Now.Day.ToString("D2"), ((int)(span.TotalSeconds)).ToString(), _targetWidth, _targetHeight);
			// [MOZ] Use actual time in place of seconds
			string dateTime = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
			string filename = string.Format("{0}_{1}_{2}x{3}", filenamePrefix, dateTime, _targetWidth, _targetHeight);
			// [MOZ] File extension is now optional
			if (!string.IsNullOrEmpty(filenameExtension))
			{
				filename = filename + "." + filenameExtension;
			}
			return filename;
		}

#if UNITY_ANDROID && !UNITY_EDITOR
		private static string GetAndroidExternalDCIMStoragePath()
		{
			if( Application.platform != RuntimePlatform.Android )
			{
				return Application.persistentDataPath;
			}

			var jClass = new AndroidJavaClass("android.os.Environment");
			var dcimPath = jClass.CallStatic<AndroidJavaObject>("getExternalStoragePublicDirectory", jClass.GetStatic<string>("DIRECTORY_DCIM")).Call<string>("getAbsolutePath");
			return dcimPath;
		}
#endif

		private static string GetFolder(OutputPath outputPathType, string path)
		{
			string folder = string.Empty;

#if UNITY_IOS && !UNITY_EDITOR
			// iOS only supports a very limited subset of OutputPath so fix up and warn the user
			switch (outputPathType)
			{
				case OutputPath.RelativeToPeristentData:
				case OutputPath.PhotoLibrary:
					// These are fine
					break;
				case OutputPath.RelativeToProject:
				case OutputPath.Absolute:
				case OutputPath.RelativeToDesktop:
				case OutputPath.RelativeToVideos:
				case OutputPath.RelativeToPictures:
				case OutputPath.RelativeToTemporaryCachePath:
					// These are unsupported
				default:
					Debug.LogWarning(string.Format("[AVProMovieCapture] 'OutputPath.{0}' is not supported on iOS, defaulting to 'OutputPath.RelativeToPeristentData'", outputPathType));
					outputPathType = OutputPath.RelativeToPeristentData;
					break;
			}
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
			// Android only supports a very limited subset of OutputPath so fix up and warn the user
			switch (outputPathType)
			{
				case OutputPath.RelativeToPeristentData:
				case OutputPath.RelativeToVideos:
				case OutputPath.Absolute:
				case OutputPath.RelativeToTemporaryCachePath:
					// These are fine
					break;
				case OutputPath.RelativeToProject:
				case OutputPath.RelativeToDesktop:
				case OutputPath.RelativeToPictures:
				case OutputPath.PhotoLibrary:
					// These are unsupported
				default:
					Debug.LogWarning(string.Format("[AVProMovieCapture] 'OutputPath.{0}' is not supported on Android, defaulting to 'OutputPath.RelativeToPeristentData'", outputPathType));
					outputPathType = OutputPath.RelativeToPeristentData;
					break;
			}
#endif
#if UNITY_EDITOR
			// Photo Library is unavailable in the editor
			if (outputPathType == OutputPath.PhotoLibrary)
			{
				Debug.LogWarning("[AVProMovieCapture] 'OutputPath.PhotoLibrary' is not available in the Unity Editor, defaulting to 'OutputPath.RelativeToProject'");
				outputPathType = OutputPath.RelativeToProject;
			}
#endif

			switch (outputPathType)
			{
				case OutputPath.RelativeToProject:
					#if UNITY_STANDALONE_OSX
					// For standalone macOS builds this puts the path at the same level as the application bundle
					folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../.."));
					#else
					folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, ".."));
					#endif
					break;
				case OutputPath.RelativeToPeristentData:
					folder = System.IO.Path.GetFullPath(Application.persistentDataPath);
					break;
				case OutputPath.Absolute:
					break;
				case OutputPath.RelativeToDesktop:
					folder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
					break;
				case OutputPath.RelativeToPictures:
					folder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyPictures);
					break;
				case OutputPath.RelativeToVideos:
					#if UNITY_ANDROID && !UNITY_EDITOR
						folder = GetAndroidExternalDCIMStoragePath();
					#else
						#if NET_4_6
							folder = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyVideos);
						#else
							folder = System.Environment.GetFolderPath((System.Environment.SpecialFolder)14);    // Older Mono doesn't have MyVideos defined - but still works!
						#endif
					#endif
					break;
				case OutputPath.PhotoLibrary:
					// use avpmc-photolibrary as the scheme
					folder = "avpmc-photolibrary:///";	// Three slashes are good as we don't need the host component
					break;
				case OutputPath.RelativeToTemporaryCachePath:
					folder = System.IO.Path.GetFullPath(Application.temporaryCachePath);
					break;
			}

			return System.IO.Path.Combine(folder, path);
		}

		private static string GenerateFilePath(OutputPath outputPathType, string path, string filename)
		{
			// Resolve folder
			string fileFolder = GetFolder(outputPathType, path);

			// Combine path and filename
			return System.IO.Path.Combine(fileFolder, filename);
		}

		protected static bool HasExtension(string path, string extension)
		{
			return path.ToLower().EndsWith(extension, StringComparison.OrdinalIgnoreCase);
		}

		protected void GenerateFilename()
		{
			string filename = string.Empty;
			if (_outputTarget == OutputTarget.VideoFile)
			{
				if (!_allowManualFileExtension)
				{
					if (_selectedVideoCodec == null)
					{
						SelectVideoCodec();
						SelectAudioCodec();
					}
					int videoCodec = (_selectedVideoCodec != null) ? _selectedVideoCodec.Index : -1;
					int audioCodec = (_selectedAudioCodec != null) ? _selectedAudioCodec.Index : -1;
					string[] extensions = NativePlugin.GetContainerFileExtensions(videoCodec, audioCodec);
					if (extensions != null && extensions.Length > 0)
					{
						_filenameExtension = extensions[0];
					}
				}
				if (_appendFilenameTimestamp)
				{
					filename = GenerateTimestampedFilename(_filenamePrefix, _filenameExtension);
				}
				else
				{
					filename = _filenamePrefix + "." + _filenameExtension;
				}
			}
			else if (_outputTarget == OutputTarget.ImageSequence)
			{
				// [MOZ] Made the enclosing folder uniquely named, easier for extraction on iOS and simplifies scripts for processing the frames
				string fileExtension = Utils.GetImageFileExtension(NativeImageSequenceFormat);
				filename = GenerateTimestampedFilename(_filenamePrefix, null) + "/frame" + string.Format("-%0{0}d.{1}", _imageSequenceZeroDigits, fileExtension);
			}
			else if (_outputTarget == OutputTarget.NamedPipe)
			{
				_filePath = _namedPipePath;
			}

			if (_outputTarget == OutputTarget.VideoFile ||
				_outputTarget == OutputTarget.ImageSequence)
			{
				OutputPath outputFolderType = _outputFolderType;
				string outputFolderPath = _outputFolderPath;
				_finalFilePath = null;

#if UNITY_ANDROID && !UNITY_EDITOR
				if( _outputFolderType != OutputPath.RelativeToPeristentData )
				{
					// Where do we want to write the final file to?
					_finalFilePath = GenerateFilePath(_outputFolderType, _outputFolderPath, filename);

					// Capture to path relative to the project
					outputFolderType = OutputPath.RelativeToPeristentData;
					outputFolderPath = "Captures";

					// Create target final directory if it doesn't exist
					String finalDirectory = Path.GetDirectoryName(_finalFilePath);
					Debug.Log("[AVProMovieCapture]: finalDirectory = " + finalDirectory);
					if (!string.IsNullOrEmpty(finalDirectory) && !Directory.Exists(finalDirectory))
					{
						Directory.CreateDirectory(finalDirectory);
					}
				}
#endif
				_filePath = GenerateFilePath(outputFolderType, outputFolderPath, filename);

				// Check to see if this filename is already in use
				if (ActiveFilePaths.Contains(_filePath))
				{
					// It is, strip the extension
					string extension = Path.GetExtension(_filePath);
					string name = Path.GetFileNameWithoutExtension(_filePath);
					string path = Path.GetDirectoryName(_filePath);
					string newPath = null;
					int i = 2;
					do
					{
						const string fmt = "{0} {1}";
						string newName = String.Format(fmt, name, i++);
						newPath = Path.Combine(path, newName);
						newPath = Path.ChangeExtension(newPath, extension);
					}
					while (ActiveFilePaths.Contains(newPath));
					_filePath = newPath;
				}

				ActiveFilePaths.Add(_filePath);

#if !UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS)
				if (_outputFolderType != OutputPath.PhotoLibrary)
#endif
				{
					// Create target directory if it doesn't exist
					String directory = Path.GetDirectoryName(_filePath);
					if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					{
						Directory.CreateDirectory(directory);
					}
				}
			}
		}

		public UnityAudioCapture FindOrCreateUnityAudioCapture(bool logWarnings)
		{
			UnityAudioCapture result = null;

			if (_audioCaptureSource == AudioCaptureSource.Unity)
			{
				Type audioCaptureType = null;
				if (_isRealTime)
				{
					audioCaptureType = typeof(CaptureAudioFromAudioListener);
				}
				else
				{
					#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
					audioCaptureType = typeof(CaptureAudioFromAudioRenderer);
					#endif
				}

				if (audioCaptureType != null)
				{
					// Try to find an existing matching component locally
					result = (UnityAudioCapture)this.GetComponent(audioCaptureType);
					if (result == null)
					{
						// Try to find an existing matching component globally
						result = (UnityAudioCapture)GameObject.FindObjectOfType(audioCaptureType);
					}

					// No existing component was found, so create one
					if (result == null)
					{
						// Find a suitable gameobject to add the component to
						GameObject parentGameObject = null;
						if (_isRealTime)
						{
							// Find an AudioListener to attach the UnityAudioCapture component to
							AudioListener audioListener = this.GetComponent<AudioListener>();
							if (audioListener == null)
							{
								audioListener = GameObject.FindObjectOfType<AudioListener>();
							}
							parentGameObject = audioListener.gameObject;
						}
						else
						{
							parentGameObject = this.gameObject;
						}

						// Create the component
						if (_isRealTime)
						{
							if (parentGameObject != null)
							{
								result = (UnityAudioCapture)parentGameObject.AddComponent(audioCaptureType);
								if (logWarnings)
								{
									Debug.LogWarning("[AVProMovieCapture] Capturing audio from Unity without an UnityAudioCapture assigned so we had to create one manually (very slow).  Consider adding a UnityAudioCapture component to your scene and assigned it to this MovieCapture component.");
								}
							}
							else
							{
								if (logWarnings)
								{
									Debug.LogWarning("[AVProMovieCapture] No AudioListener found in scene.  Unable to capture audio from Unity.");
								}
							}
						}
						else
						{
							#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
							result = (UnityAudioCapture)parentGameObject.AddComponent(audioCaptureType);
							((CaptureAudioFromAudioRenderer)result).Capture = this;
							if (logWarnings)
							{
								Debug.LogWarning("[AVProMovieCapture] Capturing audio from Unity without an UnityAudioCapture assigned so we had to create one manually (very slow).  Consider adding a UnityAudioCapture component to your scene and assigned it to this MovieCapture component.");
							}
							#endif
						}
					}
					else
					{
						if (logWarnings)
						{
							Debug.LogWarning("[AVProMovieCapture] Capturing audio from Unity without an UnityAudioCapture assigned so we had to search for one manually (very slow)");
						}
					}
				}
			}
#if AVPRO_MOVIECAPTURE_WWISE_SUPPORT
			else if (_audioCaptureSource == AudioCaptureSource.Wwise)
			{
				Type audioCaptureType = null;
				if (!_isRealTime)
				{
					audioCaptureType = typeof(CaptureAudioFromWwise);
				}
				if (audioCaptureType != null)
				{
					// Try to find an existing matching component locally
					result = (UnityAudioCapture)this.GetComponent(audioCaptureType);
					if (result == null)
					{
						// Try to find an existing matching component globally
						result = (UnityAudioCapture)GameObject.FindObjectOfType(audioCaptureType);
					}

					// No existing component was found, so create one
					if (result == null)
					{
						result = (UnityAudioCapture)this.gameObject.AddComponent(audioCaptureType);
					}

					if (result)
					{
						((CaptureAudioFromWwise)result).Capture = this;
					}
				}
			}
#endif // AVPRO_MOVIECAPTURE_WWISE_SUPPORT

			return result;
		}

		private bool ValidateEditionFeatures()
		{
			bool canContinueCapture = true;
			if (NativePlugin.IsBasicEdition())
			{
				string issues = string.Empty;

				// Abortable issues
				if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
				{
					issues += "• D3D12 is not supported, please switch to D3D11.  Aborting capture.\n";
					canContinueCapture = false;
				}
				if (this is CaptureFromCamera || this is CaptureFromTexture || this is CaptureFromCamera360 || this is CaptureFromCamera360ODS)
				{
					issues += "• Only CaptureFromScreen component is supported.  Aborting capture.\n";
					canContinueCapture = false;
				}

				// Continuable issues
				if (canContinueCapture)
				{
					if (SelectedVideoCodec != null && SelectedVideoCodec.Name.IndexOf("H264") < 0)
					{
						issues += "• Only H264 video codec supported.  Switching to H264\n";
						NativeForceVideoCodecIndex = 0;
						SelectVideoCodec(false);
					}
					if (SelectedAudioCodec != null && SelectedAudioCodec.Name.IndexOf("AAC") < 0)
					{
						issues += "• Only AAC audio codec supported.  Switching to AAC\n";
						NativeForceAudioCodecIndex = 0;
						SelectAudioCodec();
					}
					if (!IsRealTime)
					{
						issues += "• Non-realtime captures are not supported.  Switching to realtime capture mode.\n";
						IsRealTime = true;
					}
					if (OutputTarget != OutputTarget.VideoFile)
					{
						issues += "• Only output to video file is supported.  Switching to video file output.\n";
						OutputTarget = OutputTarget.VideoFile;
						FilenameExtension = "mp4";
						GenerateFilename();
					}
					if (AudioCaptureSource != AudioCaptureSource.None && AudioCaptureSource != AudioCaptureSource.Unity)
					{
						issues += "• Audio source '" + AudioCaptureSource + "' not supported.  Disabling audio capture.\n";
						AudioCaptureSource = AudioCaptureSource.None;
					}
					if (FrameRate != 30f)
					{
						issues += "• Frame rate '" + FrameRate + "' not supported.  Switching to 30 FPS.\n";
						FrameRate = 30f;
					}
					if (GetEncoderHints().videoHints.supportTransparency)
					{
						issues += "• Transparent capture not supported.  Disabling transparent capture\n";
						GetEncoderHints().videoHints.supportTransparency = false;
					}
				}

				// Log/Display issues
				if (!string.IsNullOrEmpty(issues))
				{
					string message = "Limitations of Basic Edition reached:\n" + issues + "Please upgrade to use these feature, or visit '" + DocEditionsURL + "' for more information.";
					if (canContinueCapture)
					{
						Debug.LogWarning("[AVProMovieCapture] " + message);
					}
					else
					{
						Debug.LogError("[AVProMovieCapture] " + message);
					}

					#if UNITY_EDITOR
					message = "Limitations of Basic Edition reached:\n\n" + issues + "\nPlease upgrade to use these feature.  View documention for more information.";
					if (!UnityEditor.EditorUtility.DisplayDialog("AVPro Movie Capture", message, "Ok", "More Info"))
					{
						Application.OpenURL(DocEditionsURL);
					}
					#endif
				}
			}
			return canContinueCapture;
		}

		public virtual bool PrepareCapture()
		{
			if (!ValidateEditionFeatures())
			{
				return false;
			}

			// Delete file if it already exists
			if (_outputTarget == OutputTarget.VideoFile && File.Exists(_filePath))
			{
				File.Delete(_filePath);
			}

			_stats = new CaptureStats();

#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			if (_minimumDiskSpaceMB > 0 && _outputTarget == OutputTarget.VideoFile)
			{
				ulong freespace = 0;
				if (Utils.DriveFreeBytes(System.IO.Path.GetPathRoot(_filePath), out freespace))
				{
					_freeDiskSpaceMB = (long)(freespace / (1024 * 1024));
				}

				if (!IsEnoughDiskSpace())
				{
					Debug.LogError("[AVProMovieCapture] Not enough free space to start capture.  Stopping capture.");
					return false;
				}
			}
#endif

			if (_isRealTime)
			{
				/*if (_allowFrameRateChange)
				{
					_oldTargetFrameRate = Application.targetFrameRate;
					Application.targetFrameRate = (int)_frameRate;
				}*/
			}
			else
			{
				// Disable vsync
				#if !UNITY_EDITOR && (UNITY_IOS || UNITY_ANDROID)
					if (_allowVSyncDisable)
					{
						// iOS and Android do not support disabling vsync so use _oldVsyncCount to store the current target framerate.
						_oldVSyncCount = Application.targetFrameRate;
						// We want to runs as fast as possible.
						Application.targetFrameRate = 300;
					}
				#else
					if (_allowVSyncDisable && !Screen.fullScreen && QualitySettings.vSyncCount > 0)
					{
						_oldVSyncCount = QualitySettings.vSyncCount;
						QualitySettings.vSyncCount = 0;
					}
				#endif

				if (_useMotionBlur && _motionBlurSamples > 1)
				{
					#if AVPRO_MOVIECAPTURE_CAPTUREDELTA_SUPPORT
					Time.captureDeltaTime = 1f / (_motionBlurSamples * _frameRate);
					#else
					Time.captureFramerate = (int)(_motionBlurSamples * _frameRate);
					#endif

					// FromTexture and FromCamera360 captures don't require a camera for rendering, so set up the motion blur component differently
					if (this is CaptureFromTexture || this is CaptureFromCamera360 || this is CaptureFromCamera360ODS)
					{
						if (_motionBlur == null)
						{
							_motionBlur = this.GetComponent<MotionBlur>();
						}
						if (_motionBlur == null)
						{
							_motionBlur = this.gameObject.AddComponent<MotionBlur>();
						}
						if (_motionBlur != null)
						{
							_motionBlur.NumSamples = _motionBlurSamples;
							_motionBlur.SetTargetSize(_targetWidth, _targetHeight);
							_motionBlur.enabled = false;
						}
					}
					// FromCamera and FromScreen use this path
					else if (_motionBlurCameras.Length > 0)
					{
						// Setup the motion blur filters where cameras are used
						foreach (Camera camera in _motionBlurCameras)
						{
							MotionBlur mb = camera.GetComponent<MotionBlur>();
							if (mb == null)
							{
								mb = camera.gameObject.AddComponent<MotionBlur>();
							}
							if (mb != null)
							{
								mb.NumSamples = _motionBlurSamples;
								mb.enabled = true;
								_motionBlur = mb;
							}
						}
					}
				}
				else
				{
					#if AVPRO_MOVIECAPTURE_CAPTUREDELTA_SUPPORT
					Time.captureDeltaTime = 1f / _frameRate;
					#else
					Time.captureFramerate = (int)_frameRate;
					#endif
				}

				// Change physics update speed
				_oldFixedDeltaTime = Time.fixedDeltaTime;
				#if AVPRO_MOVIECAPTURE_CAPTUREDELTA_SUPPORT
				Time.fixedDeltaTime = Time.captureDeltaTime;
				#else
				Time.fixedDeltaTime = 1.0f / Time.captureFramerate;
				#endif
			}

			// Resolve desired audio source
			_stats.AudioCaptureSource = AudioCaptureSource.None;
			if (_audioCaptureSource != AudioCaptureSource.None && _outputTarget == OutputTarget.VideoFile)
			{
				if (_audioCaptureSource == AudioCaptureSource.Microphone && _isRealTime)
				{
					if (_selectedAudioInputDevice != null)
					{
						_stats.AudioCaptureSource = AudioCaptureSource.Microphone;
					}
					else
					{
						Debug.LogWarning("[AVProMovieCapture] No microphone found");
					}
				}
				else if (_audioCaptureSource == AudioCaptureSource.Unity || _audioCaptureSource == AudioCaptureSource.Wwise)
				{
					// If there is already a capture component, make sure it's the right one otherwise remove it
					if (_unityAudioCapture != null)
					{
						bool removeComponent = false;
						if (_audioCaptureSource == AudioCaptureSource.Unity)
						{
							if (_isRealTime)
							{
								removeComponent = !(_unityAudioCapture is CaptureAudioFromAudioListener);
							}
							else
							{
								removeComponent = (_unityAudioCapture is CaptureAudioFromAudioListener);
							}
						}
						else if (_audioCaptureSource == AudioCaptureSource.Wwise)
						{
							#if !AVPRO_MOVIECAPTURE_WWISE_SUPPORT
							removeComponent = true;
							#else
							if (_isRealTime)
							{
								removeComponent = true;
							}
							else
							{
								removeComponent = !(_unityAudioCapture is CaptureAudioFromWwise);
							}
							#endif
						}
						if (removeComponent)
						{
							Destroy(_unityAudioCapture);
							_unityAudioCapture = null;
						}
					}

					// We if try to capture audio from Unity but there isn't an UnityAudioCapture component set
					if (_unityAudioCapture == null)
					{
						_unityAudioCapture = FindOrCreateUnityAudioCapture(true);
					}
					if (_unityAudioCapture != null)
					{
						_unityAudioCapture.PrepareCapture();
						_stats.UnityAudioSampleRate = _unityAudioCapture.SampleRate;
						_stats.UnityAudioChannelCount = _unityAudioCapture.ChannelCount;
						_stats.AudioCaptureSource = _audioCaptureSource;
					}
					else
					{
						Debug.LogWarning("[AVProMovieCapture] Unable to create AudioCapture component in mode " + _audioCaptureSource.ToString());
					}
				}
				else if (_audioCaptureSource == AudioCaptureSource.UnityAudioMixer)
				{
					_stats.UnityAudioSampleRate = AudioSettings.outputSampleRate;
					_stats.UnityAudioChannelCount = UnityAudioCapture.GetUnityAudioChannelCount();
					_stats.AudioCaptureSource = _audioCaptureSource;
				}
				else if (_audioCaptureSource == AudioCaptureSource.Manual)
				{
					_stats.UnityAudioSampleRate = _manualAudioSampleRate;
					_stats.UnityAudioChannelCount = _manualAudioChannelCount;
					_stats.AudioCaptureSource = AudioCaptureSource.Manual;
				}
			}

			if (_selectedVideoCodec == null) return false;

			string info = string.Empty;
			if (_logCaptureStartStop)
			{
				info = string.Format("{0}x{1} @ {2}fps [{3}]", _targetWidth, _targetHeight, _frameRate.ToString("F2"), _pixelFormat.ToString());
				if (_outputTarget == OutputTarget.VideoFile)
				{
					info += string.Format(" vcodec:'{0}'", _selectedVideoCodec.Name);
					if (_stats.AudioCaptureSource != AudioCaptureSource.None)
					{
						if (_audioCaptureSource == AudioCaptureSource.Microphone && _selectedAudioInputDevice != null)
						{
							info += string.Format(" audio source:'{0}'", _selectedAudioInputDevice.Name);
						}
						else if (_audioCaptureSource == AudioCaptureSource.Unity && _unityAudioCapture != null)
						{
							info += string.Format(" audio source:'Unity' {0}hz {1} channels", _stats.UnityAudioSampleRate, _stats.UnityAudioChannelCount);
						}
						else if (_audioCaptureSource == AudioCaptureSource.UnityAudioMixer)
						{
							info += string.Format(" audio source:'AudioMixer' {0}hz {1} channels", _stats.UnityAudioSampleRate, _stats.UnityAudioChannelCount);
						}
						else if (_audioCaptureSource == AudioCaptureSource.Manual)
						{
							info += string.Format(" audio source:'Manual' {0}hz {1} channels", _stats.UnityAudioSampleRate, _stats.UnityAudioChannelCount);
						}
						else if (_audioCaptureSource == AudioCaptureSource.Wwise && _unityAudioCapture != null)
						{
							info += string.Format(" audio source:'Wwise' {0}hz {1} channels", _stats.UnityAudioSampleRate, _stats.UnityAudioChannelCount);
						}
						if (_selectedAudioCodec != null)
						{
							info += string.Format(" acodec:'{0}'", _selectedAudioCodec.Name);
						}
					}

					info += string.Format(" to file: '{0}'", _filePath);
				}
				else if (_outputTarget == OutputTarget.ImageSequence)
				{
					info += string.Format(" to file: '{0}'", _filePath);
				}
				else if (_outputTarget == OutputTarget.NamedPipe)
				{
					info += string.Format(" to pipe: '{0}'", _filePath);
				}
			}

			// If the user has overriden the vertical flip
			if (_flipVertically)
			{
				_isTopDown = !_isTopDown;
			}

			if (_outputTarget == OutputTarget.VideoFile)
			{
				if (_logCaptureStartStop)
				{
					Debug.Log("[AVProMovieCapture] Start File Capture: " + info);
				}
				bool useRealtimeClock = (_isRealTime && _timelapseScale <= 1);
				AudioCaptureSource audioCaptureSource = _stats.AudioCaptureSource;
				if (audioCaptureSource == AudioCaptureSource.Wwise) { audioCaptureSource = AudioCaptureSource.Unity; }	// This is a mild hack until we rebuild the plugins

				VideoEncoderHints hints = GetEncoderHints().videoHints;
				hints.colourSpace = (VideoEncoderHints.ColourSpace)QualitySettings.activeColorSpace;
				hints.sourceWidth = _sourceWidth;
				hints.sourceHeight = _sourceHeight;

				// Android only
				hints.androidNoCaptureRotation = _androidNoCaptureRotation;

				_handle = NativePlugin.CreateRecorderVideo(
					_filePath,
					(uint)_targetWidth,
					(uint)_targetHeight,
					_frameRate,
					(int)_pixelFormat,
					useRealtimeClock,
					_isTopDown,
					_selectedVideoCodec.Index,
					audioCaptureSource,
					_stats.UnityAudioSampleRate,
					_stats.UnityAudioChannelCount,
					(_selectedAudioInputDevice != null) ? _selectedAudioInputDevice.Index : -1,
					(_selectedAudioCodec != null) ? _selectedAudioCodec.Index : -1,
					_forceGpuFlush,
					hints);
			}
			else if (_outputTarget == OutputTarget.ImageSequence)
			{
				if (_logCaptureStartStop)
				{
					Debug.Log("[AVProMovieCapture] Start Images Capture: " + info);
				}
				bool useRealtimeClock = (_isRealTime && _timelapseScale <= 1);

				ImageEncoderHints hints = GetEncoderHints().imageHints;
				hints.colourSpace = (ImageEncoderHints.ColourSpace)QualitySettings.activeColorSpace;
				hints.sourceWidth = _sourceWidth;
				hints.sourceHeight = _sourceHeight;

				_handle = NativePlugin.CreateRecorderImages(
					_filePath,
					(uint)_targetWidth,
					(uint)_targetHeight,
					_frameRate,
					(int)_pixelFormat,
					useRealtimeClock,
					_isTopDown,
					(int)NativeImageSequenceFormat,
					_forceGpuFlush,
					_imageSequenceStartFrame,
					hints);
			}
			else if (_outputTarget == OutputTarget.NamedPipe)
			{
				if (_logCaptureStartStop)
				{
					Debug.Log("[AVProMovieCapture] Start Pipe Capture: " + info);
				}
				_handle = NativePlugin.CreateRecorderPipe(_filePath, (uint)_targetWidth, (uint)_targetHeight, _frameRate,
																	 (int)_pixelFormat, _isTopDown, GetEncoderHints().videoHints.supportTransparency, _forceGpuFlush);
			}

			if (_handle >= 0)
			{
				RenderThreadEvent(NativePlugin.PluginEvent.Setup);
			}
			else
			{
				Debug.LogError("[AVProMovieCapture] Failed to create recorder");

				// Try to give a reason why it failed
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
				if (_selectedVideoCodec.MediaApi == MediaApi.MediaFoundation)
				{
					if (!HasExtension(_filePath, ".mp4"))
					{
						Debug.LogError("[AVProMovieCapture] When using a MediaFoundation codec the MP4 extension must be used");
					}

					// MF H.264 encoder has a limit of Level 5.2 which is 9,437,184 luma pixels
					// but we've seen it fail slightly below this limit, so we test against 9360000
					// to offer a useful potential error message
					if (((_targetWidth * _targetHeight) >= 9360000) && _selectedVideoCodec.Name.Contains("H264"))
					{
						Debug.LogError("[AVProMovieCapture] Resolution is possibly too high for the MF H.264 codec");
					}
				}
				else if (_selectedVideoCodec.MediaApi == MediaApi.DirectShow)
				{
					if (HasExtension(_filePath, ".mp4") && _selectedVideoCodec.Name.Contains("Uncompressed"))
					{
						Debug.LogError("[AVProMovieCapture] Uncompressed video codec not supported with MP4 extension, use AVI instead for uncompressed");
					}
				}
#endif

				StopCapture();
			}

// (mac|i)OS only for now
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
			SetupErrorHandler();
#endif
			return (_handle >= 0);
		}

#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))

		static Dictionary<int, CaptureBase> _HandleToCaptureMap = new Dictionary<int, CaptureBase>();

		private void SetupErrorHandler()
		{
			NativePlugin.ErrorHandlerDelegate errorHandlerDelegate = new NativePlugin.ErrorHandlerDelegate(ErrorHandler);
			System.IntPtr func = Marshal.GetFunctionPointerForDelegate(errorHandlerDelegate);
			NativePlugin.SetErrorHandler(_handle, func);
			_HandleToCaptureMap.Add(_handle, this);
		}

		private void CleanupErrorHandler()
		{
			_HandleToCaptureMap.Remove(_handle);
		}

#if ENABLE_IL2CPP
		[MonoPInvokeCallback(typeof(NativePlugin.ErrorHandlerDelegate))]
#endif
		private static void ErrorHandler(int handle, int domain, int code, string message)
		{
			CaptureBase capture;
			if (_HandleToCaptureMap.TryGetValue(handle, out capture))
			{
				capture.ActualErrorHandler(domain, code, message);
			}
		}

		private void ActualErrorHandler(int domain, int code, string message) {
			if (_capturing)
			{
				CancelCapture();
				Debug.LogError("Capture cancelled");
			}
			Debug.LogErrorFormat("Error: domain: {0}, code: {1}, message: {2}", domain, code, message);
		}
#endif

		public void QueueStartCapture()
		{
			_queuedStartCapture = true;
			_stats = new CaptureStats();
		}

		public bool IsStartCaptureQueued()
		{
			return _queuedStartCapture;
		}

		protected void UpdateInjectionOptions(StereoPacking stereoPacking, SphericalVideoLayout sphericalVideoLayout)
		{
			VideoEncoderHints videoHints = GetEncoderHints().videoHints;
			if (videoHints.injectStereoPacking == NoneAutoCustom.Auto) { videoHints.stereoPacking = stereoPacking; }
			if (videoHints.injectSphericalVideoLayout == NoneAutoCustom.Auto) {	videoHints.sphericalVideoLayout = sphericalVideoLayout;	}
		}

		public bool StartCapture()
		{
			if (_capturing)
			{
				return false;
			}

			if (_waitForEndOfFrame == null)
			{
				// Start() hasn't happened yet, so queue the StartCapture
				QueueStartCapture();
				return false;
			}

			if (_handle < 0)
			{
				if (!PrepareCapture())
				{
					return false;
				}
			}

			if (_handle >= 0)
			{
				if (IsUsingUnityAudioComponent())
				{
					_unityAudioCapture.StartCapture();
				}

				// Set limit to number of frames encoded (or 0 for unlimited)
				{
					uint frameLimit = 0;
					if (_stopMode == StopMode.FramesEncoded)
					{
						frameLimit = (uint)_stopFrames;
					}
					else if (_stopMode == StopMode.SecondsEncoded && !_isRealTime)
					{
						frameLimit = (uint)Mathf.FloorToInt(_stopSeconds * _frameRate);
					}
					NativePlugin.SetEncodedFrameLimit(_handle, frameLimit);
				}

				if (!NativePlugin.Start(_handle))
				{
					StopCapture(true);
					Debug.LogError("[AVProMovieCapture] Failed to start recorder");
					return false;
				}
				ResetFPS();
				_captureStartTime = Time.realtimeSinceStartup;
				_capturePrePauseTotalTime = 0f;

				// NOTE: We set this to the elapsed time so that the first frame is captured immediately
				_timeSinceLastFrame = GetSecondsPerCaptureFrame();

				#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
				if (!_isRealTime && _timelineController != null)
				{
					_timelineController.StartCapture();
				}
				#endif
				#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
				if (!_isRealTime && _videoPlayerController != null)
				{
					_videoPlayerController.StartCapture();
				}
				#endif

				_capturing = true;
				_paused = false;

				if (_startDelay != StartDelayMode.None)
				{
					_startDelayTimer = 0f;
					_startPaused = true;
					PauseCapture();
				}

				#if UNITY_EDITOR
				if (UnityEditor.EditorApplication.isPaused)
				{
					PauseCapture();
				}
				#endif
			}

			return _capturing;
		}

		public void PauseCapture()
		{
			if (_capturing && !_paused)
			{
				if (IsUsingUnityAudioComponent())
				{
					_unityAudioCapture.enabled = false;
				}
				NativePlugin.Pause(_handle);

				if (!_isRealTime)
				{
					// TODO: should be store the timeScale value and restore it instead of assuming timeScale == 1.0?
					Time.timeScale = 0f;
				}

				_paused = true;
				ResetFPS();
			}
		}

		public void ResumeCapture()
		{
			if (_capturing && _paused)
			{
				if (IsUsingUnityAudioComponent())
				{
					_unityAudioCapture.FlushBuffer();
					_unityAudioCapture.enabled = true;
				}

				NativePlugin.Start(_handle);

				if (!_isRealTime)
				{
					Time.timeScale = 1f;
				}

				_paused = false;
				if (_startPaused)
				{
					_captureStartTime = Time.realtimeSinceStartup;
					_capturePrePauseTotalTime = 0f;
					_startPaused = false;
				}
			}
		}

		public void CancelCapture()
		{
			StopCapture(true, false, true);
		}

		public static void DeleteCapture(OutputTarget outputTarget, string path)
		{
			try
			{
				if (outputTarget == OutputTarget.VideoFile && File.Exists(path))
				{
					File.Delete(path);
				}
				else if (outputTarget == OutputTarget.ImageSequence)
				{
					string directory = Path.GetDirectoryName(path);
					if (Directory.Exists(directory))
					{
						Directory.Delete(directory, true);
					}
				}
			}
			catch (Exception ex)
			{
				Debug.LogWarning("[AVProMovieCapture] Failed to delete capture - " + ex.Message);
			}
		}

		public virtual void UnprepareCapture()
		{
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
			CleanupErrorHandler();
#endif
		}

		public static string LastFileSaved
		{
			get
			{
#if UNITY_EDITOR
				return UnityEditor.EditorPrefs.GetString("AVProMovieCapture-LastSavedFile", string.Empty);
#else
				return PlayerPrefs.GetString("AVProMovieCapture-LastSavedFile", string.Empty);
#endif
			}
			set
			{
				PlayerPrefs.SetString("AVProMovieCapture-LastSavedFile", value);
#if UNITY_EDITOR
				UnityEditor.EditorPrefs.SetString("AVProMovieCapture-LastSavedFile", value);
#endif
			}
		}

		protected void RenderThreadEvent(NativePlugin.PluginEvent renderEvent)
		{
			NativePlugin.RenderThreadEvent(renderEvent, _handle);
		}

		public virtual void StopCapture(bool skipPendingFrames = false, bool ignorePendingFileWrites = false, bool deleteCapture = false)
		{
			UnprepareCapture();

			if (_capturing)
			{
				if (_logCaptureStartStop)
				{
					if (!deleteCapture)
					{
						Debug.Log("[AVProMovieCapture] Stopping capture " + _handle);
					}
					else
					{
						Debug.Log("[AVProMovieCapture] Canceling capture " + _handle);
					}
				}
				_capturing = false;
			}

			bool applyPostOperations = false;
			FileWritingHandler fileWritingHandler = null;
			if (_handle >= 0)
			{
				NativePlugin.Stop(_handle, skipPendingFrames);

				if (_outputTarget == OutputTarget.VideoFile)
				{
					applyPostOperations = true;
				}

				fileWritingHandler = new FileWritingHandler(_outputTarget, _filePath, _handle, deleteCapture);
				if (_completedFileWritingAction != null)
				{
					fileWritingHandler.CompletedFileWritingAction = _completedFileWritingAction;
				}

				// Free the recorder, or if the file is still being written, store the action to be invoked where it is complete
				bool canFreeRecorder = (ignorePendingFileWrites || NativePlugin.IsFileWritingComplete(_handle));

				if (canFreeRecorder)
				{
					// If there is an external action set up, then notify it that writing has begun
					if (_beginFinalFileWritingAction != null)
					{
						_beginFinalFileWritingAction.Invoke(fileWritingHandler);
					}

					// Complete writing immediately
					fileWritingHandler.Dispose();
					fileWritingHandler = null;
				}
				else
				{
					// If no external action has been set up for the checking when the file writing begins and end,
					// add it to an internal list so we can make sure it completes
					if (_beginFinalFileWritingAction == null)
					{
						_pendingFileWrites.Add(fileWritingHandler);
					}

					if (!deleteCapture)
					{
						VideoEncoderHints hints = GetEncoderHints().videoHints;
						if (applyPostOperations && CanApplyPostOperations(_filePath, hints, _finalFilePath))
						{
							MP4FileProcessing.Options options = CreatePostOperationsOptions(hints, _finalFilePath);
							fileWritingHandler.SetFilePostProcess(options);
						}
					}
					applyPostOperations = false;
				}

				// If there is an external action set up, then notify it that writing has begun
				if (_beginFinalFileWritingAction != null && fileWritingHandler != null)
				{
					_beginFinalFileWritingAction.Invoke(fileWritingHandler);
				}

				_handle = -1;

				// Save the last captured path
				if (!deleteCapture)
				{
					if (!string.IsNullOrEmpty(_filePath))
					{
						if (_outputTarget == OutputTarget.VideoFile)
						{
							LastFileSaved = _filePath;
						}
						else if (_outputTarget == OutputTarget.ImageSequence)
						{
							LastFileSaved = System.IO.Path.GetDirectoryName(_filePath);
						}
					}
				}
				#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
				if (_videoPlayerController != null)
				{
					_videoPlayerController.StopCapture();
				}
				#endif
				#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
				if (_timelineController != null)
				{
					_timelineController.StopCapture();
				}
				#endif
			}

			_fileInfo = null;

			if (_unityAudioCapture)
			{
				_unityAudioCapture.StopCapture();
			}
			if (_motionBlur)
			{
				_motionBlur.enabled = false;
			}

			// Restore Unity timing
			Time.captureFramerate = 0;
			//Application.targetFrameRate = _oldTargetFrameRate;
			//_oldTargetFrameRate = -1;

			if (_oldFixedDeltaTime > 0f)
			{
				Time.fixedDeltaTime = _oldFixedDeltaTime;
			}
			_oldFixedDeltaTime = 0f;

			#if !UNITY_EDITOR_OSX && (UNITY_IOS || UNITY_ANDROID)
				// Android and iOS do not support disabling vsync so _oldVsyncCount is actually the target framerate before we started capturing.
				if (_oldVSyncCount != 0)
				{
					Application.targetFrameRate = _oldVSyncCount;
					_oldVSyncCount = 0;
				}
			#else
				if (_oldVSyncCount > 0)
				{
					QualitySettings.vSyncCount = _oldVSyncCount;
					_oldVSyncCount = 0;
				}
			#endif

			_motionBlur = null;

			if (_texture != null)
			{
				Destroy(_texture);
				_texture = null;
			}

			if (applyPostOperations)
			{
				ApplyPostOperations(_filePath, GetEncoderHints().videoHints, _finalFilePath);
			}
		}

		private static MP4FileProcessing.Options CreatePostOperationsOptions(VideoEncoderHints hints, string finalFilePath)
		{
			MP4FileProcessing.Options options = new MP4FileProcessing.Options();
			#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && (UNITY_STANDALONE_WIN || UNITY_ANDROID))
			// macOS and iOS don't require fast start postprocess as it is handled internally
			options.applyFastStart = hints.allowFastStartStreamingPostProcess;
			#endif

			options.applyStereoMode = (hints.injectStereoPacking != NoneAutoCustom.None) && (hints.stereoPacking != StereoPacking.None);
			if (options.applyStereoMode)
			{
				options.stereoMode = hints.stereoPacking;
			}

			options.applySphericalVideoLayout = (hints.injectSphericalVideoLayout != NoneAutoCustom.None) && (hints.sphericalVideoLayout != SphericalVideoLayout.None);
			if (options.applySphericalVideoLayout)
			{
				options.sphericalVideoLayout = hints.sphericalVideoLayout;
			}

			options.applyMoveCaptureFile = (finalFilePath != null);
			if(options.applyMoveCaptureFile)
			{
				options.finalCaptureFilePath = finalFilePath;
			}

			return options;
		}

		private static bool CanApplyPostOperations(string filePath, VideoEncoderHints hints, string finalFilePath)
		{
			bool result = false;
			if (HasExtension(filePath, ".mp4") || HasExtension(filePath, ".mov") && File.Exists(filePath))
			{
				result = CreatePostOperationsOptions(hints, finalFilePath).HasOptions();
			}
			return result;
		}

		protected void ApplyPostOperations(string filePath, VideoEncoderHints hints, string finalFilePath)
		{
			if (CanApplyPostOperations(filePath, hints, finalFilePath))
			{
				try
				{
					MP4FileProcessing.Options options = CreatePostOperationsOptions(hints, finalFilePath);
					if (!MP4FileProcessing.ProcessFile(filePath, false, options))
					{
						Debug.LogWarning("[AVProMovieCapture] failed to postprocess file: " + filePath);
					}
				}
				catch (System.Exception e)
				{
					Debug.LogException(e);
				}
			}
		}

		private void ToggleCapture()
		{
			if (_capturing)
			{
				//_queuedStopCapture = true;
				//_queuedStartCapture = false;
				StopCapture();
			}
			else
			{
				//_queuedStartCapture = true;
				//_queuedStopCapture = false;
				StartCapture();
			}
		}

		private bool IsEnoughDiskSpace()
		{
			bool result = true;
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			long fileSizeMB = GetCaptureFileSize() / (1024 * 1024);

			if ((_freeDiskSpaceMB - fileSizeMB) < _minimumDiskSpaceMB)
			{
				result = false;
			}
#endif
			return result;
		}

		protected bool CanContinue()
		{
			bool result = true;
			#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
			if (IsCapturing() && !IsPaused() && !_isRealTime && _videoPlayerController != null)
			{
				result = _videoPlayerController.CanContinue();
			}
			#endif
			return result;
		}

		private void Update()
		{
			if (_queuedStopCapture)
			{
				_queuedStopCapture = _queuedStartCapture = false;
				StopCapture(false, false);
			}
			if (_queuedStartCapture)
			{
				_queuedStopCapture = _queuedStartCapture = false;
				StartCapture();
			}
		}

		private void LateUpdate()
		{
			if (_handle >= 0 && !_paused)
			{
				CheckFreeDiskSpace();
			}

			if (_captureKey != KeyCode.None)
			{
				#if (!ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER)
				if (Input.GetKeyDown(_captureKey))
				{
					ToggleCapture();
				}
				#endif
			}

			RemoveCompletedFileWrites();

			if (_frameUpdateMode == FrameUpdateMode.Automatic)
			{
				// Resume capture if a start delay has been specified
				if (IsCapturing() && IsPaused() && _stats.NumEncodedFrames == 0)
				{
					float delta = 0f;
					if (_startDelay == StartDelayMode.GameSeconds)
					{
						if (!_isRealTime)
						{
							// In offline render mode Time.deltaTime is always zero due Time.timeScale being set to zero,
							// so just use the real world time
							delta = Time.unscaledDeltaTime;
						}
						else
						{
							delta = Time.deltaTime;
						}
					}
					else if (_startDelay == StartDelayMode.RealSeconds)
					{
						delta = Time.unscaledDeltaTime;
					}
					if (delta > 0f)
					{
						_startDelayTimer += delta;
						if (IsStartDelayComplete())
						{
							ResumeCapture();
						}
					}
				}

				PreUpdateFrame();
				UpdateFrame();
			}
		}

		private void RemoveCompletedFileWrites()
		{
			for (int i = _pendingFileWrites.Count - 1; i >= 0; i--)
			{
				FileWritingHandler handler = _pendingFileWrites[i];
				if (handler.IsFileReady())
				{
					_pendingFileWrites.RemoveAt(i);
				}
			}
		}

		private void CheckFreeDiskSpace()
		{
			if (_minimumDiskSpaceMB > 0)
			{
				if (!IsEnoughDiskSpace())
				{
					Debug.LogWarning("[AVProMovieCapture] Free disk space getting too low.  Stopping capture.");
					StopCapture(true);
				}
			}
		}

		protected bool IsStartDelayComplete()
		{
			bool result = false;
			if (_startDelay == StartDelayMode.None)
			{
				result = true;
			}
			else if (_startDelay == StartDelayMode.GameSeconds ||
					_startDelay == StartDelayMode.RealSeconds)
			{
				result = (_startDelayTimer >= _startDelaySeconds);
			}
			return result;
		}

		protected bool IsStopTimeReached()
		{
			bool result = false;
			if (_stopMode != StopMode.None)
			{
				switch (_stopMode)
				{
					case StopMode.FramesEncoded:
						result = (_stats.NumEncodedFrames >= _stopFrames);
						break;
					case StopMode.SecondsEncoded:
						if (!_isRealTime)
						{
							// In non-realtime mode this is a more accurate way to determine encoded time
							result = (_stats.NumEncodedFrames >= _stopSeconds * _frameRate);
						}
						else
						{
							result = (_stats.TotalEncodedSeconds >= _stopSeconds);
						}
						break;
					case StopMode.SecondsElapsed:
						if (!_startPaused && !_paused)
						{
							float timeSinceLastEditorPause = (Time.realtimeSinceStartup - _captureStartTime);
							result = (timeSinceLastEditorPause + _capturePrePauseTotalTime) >= _stopSeconds;
						}
						break;
				}
			}
			return result;
		}

		public float GetProgress()
		{
			float result = 0f;
			if (_stopMode != StopMode.None)
			{
				switch (_stopMode)
				{
					case StopMode.FramesEncoded:
						result = (_stats.NumEncodedFrames / (float)_stopFrames);
						break;
					case StopMode.SecondsEncoded:
						result = ((_stats.NumEncodedFrames / _frameRate) / _stopSeconds);
						break;
					case StopMode.SecondsElapsed:
						if (!_startPaused && !_paused)
						{
							float timeSinceLastEditorPause = (Time.realtimeSinceStartup - _captureStartTime);
							result = (timeSinceLastEditorPause + _capturePrePauseTotalTime) / _stopSeconds;
						}
						break;
				}
			}
			return result;
		}

		protected float GetSecondsPerCaptureFrame()
		{
			float timelapseScale = (float)_timelapseScale;
			if (!_isRealTime)
			{
				timelapseScale = 1f;
			}
			float captureFrameRate = _frameRate / timelapseScale;
			float secondsPerFrame = 1f / captureFrameRate;
			return secondsPerFrame;
		}

		protected bool CanOutputFrame()
		{
			bool result = false;
			if (_handle >= 0)
			{
				if (_isRealTime)
				{
					if (NativePlugin.IsNewFrameDue(_handle))
					{
						result = (_timeSinceLastFrame >= GetSecondsPerCaptureFrame());
						//result = true;
					}
				}
				else
				{
					const int WatchDogLimit = 1000;
					int watchdog = 0;
					if (_outputTarget != OutputTarget.NamedPipe)
					{
						// Wait for the encoder to have an available buffer
						// The watchdog prevents an infinite while loop
						while (_handle >= 0 && !NativePlugin.IsNewFrameDue(_handle) && watchdog < WatchDogLimit)
						{
							System.Threading.Thread.Sleep(1);
							watchdog++;
						}
					}

					// Return handle status as it may have closed elsewhere
					result = (_handle >= 0) && (watchdog < WatchDogLimit);
				}
			}
			return result;
		}

		protected void TickFrameTimer()
		{
			_timeSinceLastFrame += Time.deltaTime;//unscaledDeltaTime;
		}

		protected void RenormTimer()
		{
			float secondsPerFrame = GetSecondsPerCaptureFrame();
			if (_timeSinceLastFrame >= secondsPerFrame)
			{
				_timeSinceLastFrame -= secondsPerFrame;
			}
		}

		public virtual Texture GetPreviewTexture()
		{
			return null;
		}

		protected void EncodeUnityAudio()
		{
			if (IsUsingUnityAudioComponent())
			{
				int audioDataLength = 0;
				System.IntPtr audioDataPtr = _unityAudioCapture.ReadData(out audioDataLength);
				if (audioDataLength > 0)
				{
					NativePlugin.EncodeAudio(_handle, audioDataPtr, (uint)audioDataLength);
				}
			}
		}

		public void EncodeAudio(NativeArray<float> audioData)
		{
			if (audioData.Length > 0)
			{
#if UNITY_NATIVEARRAY_UNSAFE_SUPPORT
				unsafe
				{
					System.IntPtr pointer = (System.IntPtr)Unity.Collections.LowLevel.Unsafe.NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(audioData);
					NativePlugin.EncodeAudio(_handle, pointer, (uint)audioData.Length);
				}
#else
				EncodeAudio(audioData.ToArray());
#endif
			}
		}

		public void EncodeAudio(float[] audioData)
		{
			if (audioData.Length > 0)
			{
				int byteCount = Marshal.SizeOf(audioData[0]) * audioData.Length;

				// Copy the array to unmanaged memory.
				System.IntPtr pointer = Marshal.AllocHGlobal(byteCount);
				Marshal.Copy(audioData, 0, pointer, audioData.Length);

				// Encode
				NativePlugin.EncodeAudio(_handle, pointer, (uint)audioData.Length);

				// Free the unmanaged memory.
				Marshal.FreeHGlobal(pointer);
			}
		}

		public virtual void PreUpdateFrame()
		{
			#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
			if (IsCapturing() && !IsPaused() && !_isRealTime && _timelineController != null)
			{
				_timelineController.UpdateFrame();
			}
			#endif
			#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
			if (IsCapturing() && !IsPaused() && !_isRealTime && _videoPlayerController != null)
			{
				_videoPlayerController.UpdateFrame();
			}
			#endif
		}

		public virtual void UpdateFrame()
		{
			// NOTE: Unlike other CaptureFrom components, CaptureFromScreen uses a coroutine, so when it calls base.UpdateFrame() is could still process the frame afterwards
			if (_handle >= 0 && !_paused)
			{
				_stats.NumDroppedFrames = NativePlugin.GetNumDroppedFrames(_handle);
				_stats.NumDroppedEncoderFrames = NativePlugin.GetNumDroppedEncoderFrames(_handle);
				_stats.NumEncodedFrames = NativePlugin.GetNumEncodedFrames(_handle);
				_stats.TotalEncodedSeconds = NativePlugin.GetEncodedSeconds(_handle);

				if (IsStopTimeReached())
				{
					_queuedStopCapture = true;
				}
			}
		}

		protected void ResetFPS()
		{
			_stats.ResetFPS();
		}

		public void UpdateFPS()
		{
			_stats.UpdateFPS();
		}

		protected int GetCameraAntiAliasingLevel(Camera camera)
		{
			int aaLevel = QualitySettings.antiAliasing;
			if (aaLevel == 0)
			{
				aaLevel = 1;
			}

			if (_renderAntiAliasing > 0)
			{
				aaLevel = _renderAntiAliasing;
			}

			if (aaLevel != 1 && aaLevel != 2 && aaLevel != 4 && aaLevel != 8)
			{
				Debug.LogWarning("[AVProMovieCapture] Invalid antialiasing value, must be 1, 2, 4 or 8.  Defaulting to 1. >> " + aaLevel);
				aaLevel = 1;
			}

			if (aaLevel != 1)
			{
				if (camera.actualRenderingPath == RenderingPath.DeferredLighting || camera.actualRenderingPath == RenderingPath.DeferredShading)
				{
					Debug.LogWarning("[AVProMovieCapture] Not using antialiasing because MSAA is not supported by camera render path " + camera.actualRenderingPath);
					aaLevel = 1;
				}
			}
			return aaLevel;
		}

		public long GetCaptureFileSize()
		{
			long result = 0;
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_ANDROID))
			result = NativePlugin.GetFileSize(_handle);
#elif !UNITY_WEBPLAYER
			if (_handle >= 0 && _outputTarget == OutputTarget.VideoFile)
			{
				if (_fileInfo == null && File.Exists(_filePath))
				{
					_fileInfo = new System.IO.FileInfo(_filePath);
				}
				if (_fileInfo != null)
				{
					_fileInfo.Refresh();
					result = _fileInfo.Length;
				}
			}
#endif
			return result;
		}

		public static void GetResolution(Resolution res, ref int width, ref int height)
		{
			switch (res)
			{
				case Resolution.POW2_8192x8192:
					width = 8192; height = 8192;
					break;
				case Resolution.POW2_8192x4096:
					width = 8192; height = 4096;
					break;
				case Resolution.POW2_4096x4096:
					width = 4096; height = 4096;
					break;
				case Resolution.POW2_4096x2048:
					width = 4096; height = 2048;
					break;
				case Resolution.POW2_2048x4096:
					width = 2048; height = 4096;
					break;
				case Resolution.UHD_3840x2160:
					width = 3840; height = 2160;
					break;
				case Resolution.UHD_3840x2048:
					width = 3840; height = 2048;
					break;
				case Resolution.UHD_3840x1920:
					width = 3840; height = 1920;
					break;
				case Resolution.UHD_2560x1440:
					width = 2560; height = 1440;
					break;
				case Resolution.POW2_2048x2048:
					width = 2048; height = 2048;
					break;
				case Resolution.POW2_2048x1024:
					width = 2048; height = 1024;
					break;
				case Resolution.HD_1920x1080:
					width = 1920; height = 1080;
					break;
				case Resolution.HD_1280x720:
					width = 1280; height = 720;
					break;
				case Resolution.SD_1024x768:
					width = 1024; height = 768;
					break;
				case Resolution.SD_800x600:
					width = 800; height = 600;
					break;
				case Resolution.SD_800x450:
					width = 800; height = 450;
					break;
				case Resolution.SD_640x480:
					width = 640; height = 480;
					break;
				case Resolution.SD_640x360:
					width = 640; height = 360;
					break;
				case Resolution.SD_320x240:
					width = 320; height = 240;
					break;
			}
		}

		// Returns the next multiple of 4 or the same value if it's already a multiple of 4
		protected static int NextMultipleOf4(int value)
		{
			return (value + 3) & ~0x03;
		}

		// Audio capture support

		/// <summary>
		/// Authorisation for audio capture.
		/// </summary>
		public enum AudioCaptureDeviceAuthorisationStatus
		{
			/// <summary>Audio capture is unavailable.</summary>
			Unavailable = -1,
			/// <summary>Authorisation is still to be requested.</summary>
			NotDetermined,
			/// <summary>Authorisation has been denied.</summary>
			Denied,
			/// <summary>Authorisation has been granted.</summary>
			Authorised
		};

		/// <summary>
		/// Check to see if authorisation has been given to capture audio.
		/// </summary>
		private static bool _waitingForAudioCaptureDeviceAuthorisation = true;
		private static bool _hasCheckedAudioCaptureDeviceAuthorisationStatus = false;
		private static AudioCaptureDeviceAuthorisationStatus _audioCaptureDeviceAuthorisationStatus = AudioCaptureDeviceAuthorisationStatus.NotDetermined;

		public static AudioCaptureDeviceAuthorisationStatus HasUserAuthorisationToCaptureAudio()
		{
			if (!_hasCheckedAudioCaptureDeviceAuthorisationStatus || _audioCaptureDeviceAuthorisationStatus == AudioCaptureDeviceAuthorisationStatus.NotDetermined)
			{
				_hasCheckedAudioCaptureDeviceAuthorisationStatus = true;
#if !UNITY_EDITOR_OSX && (UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR))
				_audioCaptureDeviceAuthorisationStatus = (AudioCaptureDeviceAuthorisationStatus)NativePlugin.AudioCaptureDeviceAuthorisationStatus();
#elif UNITY_ANDROID && !UNITY_EDITOR
				_audioCaptureDeviceAuthorisationStatus = Permission.HasUserAuthorizedPermission(Permission.Microphone) ? AudioCaptureDeviceAuthorisationStatus.Authorised
				                                                                                                       : AudioCaptureDeviceAuthorisationStatus.NotDetermined;
#else
				_audioCaptureDeviceAuthorisationStatus = AudioCaptureDeviceAuthorisationStatus.Unavailable;
#endif
			}
			return _audioCaptureDeviceAuthorisationStatus;
		}

#if ENABLE_IL2CPP && (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR))
		[MonoPInvokeCallback(typeof(NativePlugin.RequestAudioCaptureDeviceAuthorisationDelegate))]
#endif
		private static void RequestUserAuthorisationToCaptureAudioCallback(int authorisation)
		{
			_audioCaptureDeviceAuthorisationStatus = (AudioCaptureDeviceAuthorisationStatus)authorisation;
			_hasCheckedAudioCaptureDeviceAuthorisationStatus = true;
			_waitingForAudioCaptureDeviceAuthorisation = false;
		}

		private class WaitForAudioCaptureDeviceAuthorisation : CustomYieldInstruction
		{
			public override bool keepWaiting { get { return CaptureBase._waitingForAudioCaptureDeviceAuthorisation; } }
		}

		/// <summary>
		/// Request authorisation to capture audio.
		/// </summary>
		public static CustomYieldInstruction RequestAudioCaptureDeviceUserAuthorisation()
		{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR)
			NativePlugin.RequestAudioCaptureDeviceAuthorisationDelegate callback = new NativePlugin.RequestAudioCaptureDeviceAuthorisationDelegate(RequestUserAuthorisationToCaptureAudioCallback);
			System.IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
			NativePlugin.RequestAudioCaptureDeviceAuthorisation(ptr);
			return new WaitForAudioCaptureDeviceAuthorisation();

#elif UNITY_ANDROID && !UNITY_EDITOR
			Permission.RequestUserPermission(Permission.Microphone);
			RequestUserAuthorisationToCaptureAudioCallback((int)HasUserAuthorisationToCaptureAudio());
			return new WaitForAudioCaptureDeviceAuthorisation();
#else
			return null;
#endif
		}

		// Photo library support

		/// <summary>
		/// Level of access for the photos library.
		///</summary>
		public enum PhotoLibraryAccessLevel
		{
			/// <summary>Can only add photos to the photo library, cannot create albums or read back images.</summary>
			AddOnly,
			/// <summary>Full access, can add photos, create albums and read back images.</summary>
			ReadWrite
		};

		/// <summary>
		/// Authorisation for access to the photos library.
		/// </summary>
		public enum PhotoLibraryAuthorisationStatus
		{
			/// <summary>The photo library is unavailable.</summary>
			Unavailable = -1,
			/// <summary>Authorisation to the photo library is still to be requested.</summary>
			NotDetermined,
			/// <summary>Access to the photo library has been denied.</summary>
			Denied,
			/// <summary>Access to the photo library has been granted.</summary>
			Authorised
		};

		/// <summary>
		/// Check to see if authorisation has been given to access the photo library.
		/// </summary>
		private static bool _waitingForAuthorisationToAccessPhotos = true;
		private static bool _hasCheckedPhotoLibraryAuthorisationStatus = false;
		private static PhotoLibraryAuthorisationStatus _photoLibraryAuthorisation = PhotoLibraryAuthorisationStatus.NotDetermined;

		public static PhotoLibraryAuthorisationStatus HasUserAuthorisationToAccessPhotos(PhotoLibraryAccessLevel accessLevel)
		{
			if (!_hasCheckedPhotoLibraryAuthorisationStatus || _photoLibraryAuthorisation == PhotoLibraryAuthorisationStatus.NotDetermined)
			{
				_hasCheckedPhotoLibraryAuthorisationStatus = true;
#if !UNITY_EDITOR_OSX && (UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR))
				_photoLibraryAuthorisation = (PhotoLibraryAuthorisationStatus)NativePlugin.PhotoLibraryAuthorisationStatus((int)accessLevel);
#else
				_photoLibraryAuthorisation = PhotoLibraryAuthorisationStatus.Unavailable;
#endif
			}
			return _photoLibraryAuthorisation;
		}


#if ENABLE_IL2CPP && (UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR))
		[MonoPInvokeCallback(typeof(NativePlugin.RequestPhotoLibraryAuthorisationDelegate))]
#endif
		private static void RequestUserAuthorisationToAccessPhotosCallback(int authorisation)
		{
			_photoLibraryAuthorisation = (PhotoLibraryAuthorisationStatus)authorisation;
			_hasCheckedPhotoLibraryAuthorisationStatus = true;
			_waitingForAuthorisationToAccessPhotos = false;
		}

		private class WaitForAuthorisationToAccessPhotos: CustomYieldInstruction
		{
			public override bool keepWaiting { get { return CaptureBase._waitingForAuthorisationToAccessPhotos; } }
		}

		/// <summary>
		/// Request authorisation to access the photo library.
		/// </summary>
		public static CustomYieldInstruction RequestUserAuthorisationToAccessPhotos(PhotoLibraryAccessLevel accessLevel)
		{
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX || (UNITY_IOS && !UNITY_EDITOR)
			NativePlugin.RequestPhotoLibraryAuthorisationDelegate callback = new NativePlugin.RequestPhotoLibraryAuthorisationDelegate(RequestUserAuthorisationToAccessPhotosCallback);
			System.IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
			NativePlugin.RequestPhotoLibraryAuthorisation((int)accessLevel, ptr);
			return new WaitForAuthorisationToAccessPhotos();
#else
			return null;
#endif
		}
	}
}
