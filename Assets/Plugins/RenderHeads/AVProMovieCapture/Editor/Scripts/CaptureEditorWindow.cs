#if UNITY_EDITOR
#if UNITY_2017_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
#endif
#if UNITY_5_4_OR_NEWER || (UNITY_5 && !UNITY_5_0)
	#define AVPRO_MOVIECAPTURE_WINDOWTITLE_51
	#define AVPRO_MOVIECAPTURE_GRAPHICSDEVICETYPE_51
#endif
#if UNITY_5_4_OR_NEWER || (UNITY_5 && !UNITY_5_0 && !UNITY_5_1 && !UNITY_5_2)
	#define AVPRO_MOVIECAPTURE_SCENEMANAGER_53
#endif
#if UNITY_5_4_OR_NEWER || UNITY_5
	#define AVPRO_MOVIECAPTURE_DEFERREDSHADING
#endif
#if UNITY_2017_1_OR_NEWER
	#define AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
#endif
#if UNITY_2018_1_OR_NEWER
	// Unity 2018.1 introduces stereo cubemap render methods
	#define AVPRO_MOVIECAPTURE_UNITY_STEREOCUBEMAP_RENDER
#endif
#if !UNITY_2018_3_OR_NEWER
	#define SUPPORT_SCENE_VIEW_GIZMOS_CAPTURE
#endif

using UnityEngine;
using UnityEditor;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	/// <summary>
	/// Creates a dockable window in Unity that can be used for handy in-editor capturing
	/// </summary>
	public class CaptureEditorWindow : EditorWindow
	{
		private const string TempGameObjectName = "Temp97435_MovieCapture";
		private const string SettingsPrefix = "AVProMovieCapture.EditorWindow.";
		private const string SelectorPrefix = SettingsPrefix + "CameraSelector.";

		private GameObject _gameObject;
		private CaptureBase _capture;
		private CaptureFromScreen _captureScreen;
		private CaptureFromCamera _captureCamera;
		private CaptureFromCamera360 _captureCamera360;
		private CaptureFromCamera360ODS _captureCamera360ODS;
		private CameraSelector _cameraSelector;
#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
		private TimelineController _timelineController;
#endif

		private static bool _isTrialVersion = false;
		private static bool _isCreated = false;
		private static bool _isInit = false;
		private static bool _isFailedInit = false;
		private static bool _showAlpha = false;

		private static int _superSizeIndex = 0;
		[SerializeField] string _screenshotFolder = "Captures/Shots/";
		[SerializeField] EditorScreenshot.Options _screenshotOptions = new EditorScreenshot.Options();

		private static string[] _fileExtensions = new string[0];
		private static string[] _audioDeviceNames = new string[0];

		private readonly string[] _downScales = { "Original", "Half", "Quarter", "Eighth", "Sixteenth", "Custom" };
		private readonly string[] _captureModes = { "Realtime Capture", "Offline Render" };
		private readonly string[] _outputFolders = { "Project Folder", "Persistent Data Folder", "Absolute Folder", "Desktop", "Pictures", "Videos" };		// NOTE: PhotoLibrary is not included as Unity Editor doesn't have permission to use it anyway
		private readonly string[] _sourceNames = { "Screen", "Camera", "Camera 360 (Mono+Stereo)", "Camera 360 (experimental ODS Stereo)" };
		private readonly string[] _tabNames = { "Capture", "Visual", "Audio", "Encoding" };

		private readonly static GUIContent _guiBlankSpace = new GUIContent(" ");
		private readonly static GUIContent _guiCameraSelectorTag = new GUIContent("Tag");
		private readonly static GUIContent _guiCameraSelectorName = new GUIContent("Name");
		private readonly static GUIContent _guiContributingCameras = new GUIContent("Contributing Cameras");
		private readonly static GUIContent _guiCaptureWorldSpaceUI= new GUIContent("Capture Worldspace UI");
		private readonly static GUIContent _guiCameraRotation = new GUIContent("Camera Rotation");
		private readonly static GUIContent _guiInterpupillaryDistance = new GUIContent("Interpupillary distance");
		private readonly static GUIContent _guiStartDelay = new GUIContent("Start Delay");
		private readonly static GUIContent _guiSeconds = new GUIContent("Seconds");
		private readonly static GUIContent _guiStartFrame = new GUIContent("Start Frame");
		private readonly static GUIContent _guiZeroDigits = new GUIContent("Zero Digits");
		private readonly static GUIContent _guiStereoPacking = new GUIContent("Stereo Packing");
		private readonly static GUIContent _guiSphericalLayout = new GUIContent("Spherical Layout");

		private enum SourceType
		{
			Screen,
			Camera,
			Camera360,
			Camera360ODS,
		}

		private enum ConfigTabs
		{
			Capture = 0,
			Visual = 1,
			Audio = 2,
			Encoding = 3,
		}

		[SerializeField] SourceType _sourceType = SourceType.Screen;

		private Camera _cameraNode;
		private string _cameraName;
		private int _captureModeIndex;
		private int _outputFolderIndex;

		[SerializeField] OutputTarget _outputTarget = OutputTarget.VideoFile;
		[SerializeField] ImageSequenceFormat _imageSequenceFormat = ImageSequenceFormat.PNG;
		private bool _filenamePrefixFromSceneName = true;
		private string _filenamePrefix = "capture";
		private string _filenameExtension = "mp4";
		private int _fileContainerIndex = 0;

		[SerializeField] int _imageSequenceStartFrame = 0;
		[SerializeField, Range(2, 12)] int _imageSequenceZeroDigits = 6;
		private string _outputFolderRelative = "Captures";
		private string _outputFolderAbsolute = string.Empty;
		private bool _appendTimestamp = true;

		[SerializeField] string _namedPipePath = @"\\.\pipe\pipename";

		private int _downScaleIndex;
		private int _downscaleX;
		private int _downscaleY;

		private bool _captureMouseCursor = false;
		private Texture2D _mouseCursorTexture = null;

		[SerializeField] CaptureBase.Resolution _renderResolution = CaptureBase.Resolution.Original;
		private Vector2 _renderSize;
		[SerializeField] int _renderAntiAliasing;

		[SerializeField] bool _useContributingCameras = true;
		[SerializeField] float _frameRate = 30f;
		[SerializeField] int _timelapseScale = 1;

		private AudioCaptureSource _audioCaptureSource = AudioCaptureSource.None;
		[SerializeField, Range(8000, 96000)] int _manualAudioSampleRate = 48000;
		[SerializeField, Range(1, 8)] int _manualAudioChannelCount = 2;
		private Vector2 _scroll = Vector2.zero;
		private bool _queueStart;
		private Codec _queueConfigureVideoCodec = null;
		private Codec _queueConfigureAudioCodec = null;

		private bool _useMotionBlur = false;
		private int _motionBlurSampleCount = 16;

		private int _cubemapResolution = 2048;
		private int _cubemapDepth = 24;
		[SerializeField] bool _render180Degrees = false;
		[SerializeField] bool _captureWorldSpaceGUI = false;
		[SerializeField] bool _supportCameraRotation = false;
		[SerializeField] bool _onlyLeftRightRotation = false;
		private int _cubemapStereoPacking = 0;
		private float _cubemapStereoIPD = 0.064f;

		[SerializeField] StartDelayMode _startDelay = StartDelayMode.None;
		[SerializeField] float _startDelaySeconds = 0f;

		[SerializeField] StopMode _stopMode = StopMode.None;
		private int _stopFrames = 300;
		private float _stopSeconds = 10f;

		[SerializeField] CameraSelector.SelectByMode _selectBy = CameraSelector.SelectByMode.HighestDepthCamera;
		[SerializeField] CameraSelector.ScanFrequencyMode _scanFrequency = CameraSelector.ScanFrequencyMode.SceneLoad;
		[SerializeField] bool _scanHiddenCameras = false;
		[SerializeField] string _selectCameraTag = "MainCamera";
		[SerializeField] string _selectCameraName = "Main Camera";

		private SerializedProperty _propCameraSelectorSelectBy;
		private SerializedProperty _propCameraSelectorScanFrequency;
		private SerializedProperty _propCameraSelectorScanHiddenCameras;
		private SerializedProperty _propCameraSelectorTag;
		private SerializedProperty _propCameraSelectorName;

		private SerializedProperty _propSourceType;
		private SerializedProperty _propOutputTarget;
		private SerializedProperty _propImageSequenceFormat;
		private SerializedProperty _propImageSequenceStartFrame;
		private SerializedProperty _propImageSequenceZeroDigits;
		private SerializedProperty _propNamedPipePath;
		private SerializedProperty _propFrameRate;
		[Tooltip("Timelapse scale makes the frame capture run at a fraction of the target frame rate.  Default value is 1")]
		private SerializedProperty _propTimelapseScale;
		private SerializedProperty _propStartDelay;
		private SerializedProperty _propStartDelaySeconds;
		private SerializedProperty _propStopMode;
		private SerializedProperty _propRenderResolution;
		private SerializedProperty _propUseContributingCameras;
		private SerializedProperty _propRender180Degrees;
		private SerializedProperty _propCaptureWorldSpaceGUI;
		private SerializedProperty _propSupportCameraRotation;
		private SerializedProperty _propOnlyLeftRightRotation;
		private SerializedProperty _propManualAudioSampleRate;
		private SerializedProperty _propManualAudioChannelCount;

		private SerializedProperty _propRenderAntiAliasing;
		private SerializedProperty _propOdsRender180Degrees;
		private SerializedProperty _propOdsCamera;
		private SerializedProperty _propOdsIPD;
		private SerializedProperty _propOdsPixelSliceSize;
		private SerializedProperty _propOdsPaddingSize;
		private SerializedProperty _propOdsCameraClearMode;
		private SerializedProperty _propOdsCameraClearColor;

		[SerializeField] CaptureFromCamera360ODS.Settings _odsSettings = new CaptureFromCamera360ODS.Settings();

		private SerializedProperty _propVideoHintsAverageBitrate;
#if UNITY_EDITOR_WIN
		private SerializedProperty _propVideoHintsMaximumBitrate;
#endif
		private SerializedProperty _propVideoHintsQuality;
		private SerializedProperty _propVideoHintsKeyframeInterval;
		private SerializedProperty _propVideoHintsAllowFastStart;
		private SerializedProperty _propVideoHintsSupportTransparency;
#if UNITY_EDITOR_WIN
		private SerializedProperty _propVideoHintsUseHardwareEncoding;
#elif UNITY_EDITOR_OSX
		private	SerializedProperty _propVideoHintsEnableFragmentedWriting;
		private	SerializedProperty _propVideoHintsMovieFragmentInterval;
#endif
		private SerializedProperty _propVideoHintsInjectStereoPacking;
		private SerializedProperty _propVideoHintsStereoPacking;
		private SerializedProperty _propVideoHintsInjectSphericalVideoLayout;
		private SerializedProperty _propVideoHintsSphericalVideoLayout;

#if UNITY_EDITOR_OSX
		private SerializedProperty _propImageHintsQuality;
#endif
		private SerializedProperty _propImageHintsSupportTransparency;

		private SerializedProperty _propScreenshotFolder;
		private SerializedProperty _propScreenshotOptions;

		[SerializeField] EncoderHints _encoderHints = new EncoderHints();

		// TODO: we should actually be saving these parameters per-scene...

		private Codec _videoCodec = null;
		private Codec _audioCodec = null;
		private Device _audioInputDevice = null;

		private long _lastFileSize;
		private uint _lastEncodedMinutes;
		private uint _lastEncodedSeconds;
		private uint _lastEncodedFrame;
		private int _selectedTool;
		private int _selectedConfigTab;
		private bool _expandSectionTrial = true;

		private static Texture2D _icon;
		private string _pluginVersionWarningText = string.Empty;

		private SerializedObject _so;

		private const string LinkPluginWebsite = "http://renderheads.com/products/avpro-movie-capture/";
		private const string LinkForumPage = "http://forum.unity3d.com/threads/released-avpro-movie-capture.120717/";
		private const string LinkAssetStorePage = "https://assetstore.unity.com/packages/tools/video/avpro-movie-capture-151061?aid=1101lcNgx";
		private const string LinkSupport = "https://github.com/RenderHeads/UnityPlugin-AVProMovieCapture/issues";
		private const string LinkUserManual = "https://www.renderheads.com/content/docs/AVProMovieCapture/articles/download.html";

		private const string SupportMessage = "If you are reporting a bug, please include any relevant files and details so that we may remedy the problem as fast as possible.\n\n" +
			"Essential details:\n" +
			"+ Error message\n" +
			"      + The exact error message\n" +
			"      + The console/output log if possible\n" +
			"+ Development environment\n" +
			"      + Unity version\n" +
			"      + Development OS version\n" +
			"      + AVPro Movie Capture plugin version\n";

		[MenuItem("Window/Open AVPro Movie Capture..")]
		public static void Init()
		{
			if (_isInit || _isCreated)
			{
				CaptureEditorWindow window = (CaptureEditorWindow)EditorWindow.GetWindow(typeof(CaptureEditorWindow));
				window.Close();
				return;
			}

			_isCreated = true;

			// Get existing open window or if none, make a new one:
			CaptureEditorWindow window2 = (CaptureEditorWindow)EditorWindow.GetWindow(typeof(CaptureEditorWindow));
			if (window2 != null)
			{
				window2.SetupWindow();
			}
		}

		public void SetupWindow()
		{
			_isCreated = true;
			if ((Application.platform == RuntimePlatform.WindowsEditor)
			||  (Application.platform == RuntimePlatform.OSXEditor))
			{
				this.minSize = new Vector2(200f, 48f);
				this.maxSize = new Vector2(340f, 620f);
#if AVPRO_MOVIECAPTURE_WINDOWTITLE_51
				if (_icon != null)
				{
					this.titleContent = new GUIContent("Movie Capture", _icon, "AVPro Movie Capture");
				}
				else
				{
					this.titleContent = new GUIContent("Movie Capture", "AVPro Movie Capture");
				}
#else
				this.title = "Movie Capture";
#endif
				this.CreateGUI();
				this.LoadSettings();

				_so = new SerializedObject(this);
				if (_so == null)
				{
					Debug.LogError("[AVProMovieCapture] SerializedObject is null");
				}

				_propSourceType = _so.AssertFindProperty("_sourceType");
				_propOutputTarget = _so.AssertFindProperty("_outputTarget");
				_propImageSequenceFormat = _so.AssertFindProperty("_imageSequenceFormat");
				_propImageSequenceStartFrame = _so.AssertFindProperty("_imageSequenceStartFrame");
				_propImageSequenceZeroDigits = _so.AssertFindProperty("_imageSequenceZeroDigits");
				_propNamedPipePath = _so.AssertFindProperty("_namedPipePath");
				_propRenderResolution = _so.AssertFindProperty("_renderResolution");
				_propUseContributingCameras = _so.AssertFindProperty("_useContributingCameras");
				_propRender180Degrees = _so.AssertFindProperty("_render180Degrees");
				_propCaptureWorldSpaceGUI = _so.AssertFindProperty("_captureWorldSpaceGUI");
				_propSupportCameraRotation = _so.AssertFindProperty("_supportCameraRotation");
				_propOnlyLeftRightRotation = _so.AssertFindProperty("_onlyLeftRightRotation");

				// Audio
				_propManualAudioSampleRate = _so.AssertFindProperty("_manualAudioSampleRate");
				_propManualAudioChannelCount = _so.AssertFindProperty("_manualAudioChannelCount");

				// Time
				_propFrameRate = _so.AssertFindProperty("_frameRate");
				_propTimelapseScale = _so.AssertFindProperty("_timelapseScale");

				// Start/Stop
				_propStopMode = _so.AssertFindProperty("_stopMode");
				_propStartDelay = _so.AssertFindProperty("_startDelay");
				_propStartDelaySeconds = _so.AssertFindProperty("_startDelaySeconds");

				// Camera Selector
				_propCameraSelectorSelectBy = _so.AssertFindProperty("_selectBy");
				_propCameraSelectorScanFrequency = _so.AssertFindProperty("_scanFrequency");
				_propCameraSelectorScanHiddenCameras = _so.AssertFindProperty("_scanHiddenCameras");
				_propCameraSelectorTag = _so.AssertFindProperty("_selectCameraTag");
				_propCameraSelectorName = _so.AssertFindProperty("_selectCameraName");

				_propRenderAntiAliasing = _so.AssertFindProperty("_renderAntiAliasing");
				_propOdsIPD = _so.AssertFindProperty("_odsSettings.ipd");
				_propOdsRender180Degrees = _so.AssertFindProperty("_odsSettings.render180Degrees");
				_propOdsPixelSliceSize = _so.AssertFindProperty("_odsSettings.pixelSliceSize");
				_propOdsPaddingSize = _so.AssertFindProperty("_odsSettings.paddingSize");
				_propOdsCameraClearMode = _so.AssertFindProperty("_odsSettings.cameraClearMode");
				_propOdsCameraClearColor = _so.AssertFindProperty("_odsSettings.cameraClearColor");

				_propVideoHintsAverageBitrate = _so.AssertFindProperty("_encoderHints.videoHints.averageBitrate");
#if UNITY_EDITOR_WIN
				_propVideoHintsMaximumBitrate = _so.AssertFindProperty("_encoderHints.videoHints.maximumBitrate");
#endif
				_propVideoHintsQuality = _so.AssertFindProperty("_encoderHints.videoHints.quality");
				_propVideoHintsKeyframeInterval = _so.AssertFindProperty("_encoderHints.videoHints.keyframeInterval");
				_propVideoHintsAllowFastStart = _so.AssertFindProperty("_encoderHints.videoHints.allowFastStartStreamingPostProcess");
				_propVideoHintsSupportTransparency = _so.AssertFindProperty("_encoderHints.videoHints.supportTransparency");
#if UNITY_EDITOR_WIN
				_propVideoHintsUseHardwareEncoding = _so.AssertFindProperty("_encoderHints.videoHints.useHardwareEncoding");
#elif UNITY_EDITOR_OSX
				_propVideoHintsEnableFragmentedWriting = _so.AssertFindProperty("_encoderHints.videoHints.enableFragmentedWriting");
				_propVideoHintsMovieFragmentInterval = _so.AssertFindProperty("_encoderHints.videoHints.movieFragmentInterval");
#endif
				_propVideoHintsInjectStereoPacking = _so.AssertFindProperty("_encoderHints.videoHints.injectStereoPacking");
				_propVideoHintsStereoPacking = _so.AssertFindProperty("_encoderHints.videoHints.stereoPacking");
				_propVideoHintsInjectSphericalVideoLayout = _so.AssertFindProperty("_encoderHints.videoHints.injectSphericalVideoLayout");
				_propVideoHintsSphericalVideoLayout = _so.AssertFindProperty("_encoderHints.videoHints.sphericalVideoLayout");

#if UNITY_EDITOR_OSX
				_propImageHintsQuality = _so.AssertFindProperty("_encoderHints.imageHints.quality");
#endif
				_propImageHintsSupportTransparency = _so.AssertFindProperty("_encoderHints.imageHints.supportTransparency");

				_propScreenshotOptions = _so.AssertFindProperty("_screenshotOptions");
				_propScreenshotFolder = _so.AssertFindProperty("_screenshotFolder");

				this.Repaint();
			}
		}

		private void LoadSettings()
		{
			_sourceType = (SourceType)EditorPrefs.GetInt(SettingsPrefix + "SourceType", (int)_sourceType);

			_cameraName = EditorPrefs.GetString(SettingsPrefix + "CameraName", string.Empty);
			_captureModeIndex = EditorPrefs.GetInt(SettingsPrefix + "CaptureModeIndex", 0);

			_captureMouseCursor = EditorPrefs.GetBool(SettingsPrefix + "CaptureMouseCursor", false);
			string mouseCursorGuid = EditorPrefs.GetString(SettingsPrefix + "CaptureMouseTexture", string.Empty);
			if (!string.IsNullOrEmpty(mouseCursorGuid))
			{
				string mouseCursorPath = AssetDatabase.GUIDToAssetPath(mouseCursorGuid);
				if (!string.IsNullOrEmpty(mouseCursorPath))
				{
					_mouseCursorTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(mouseCursorPath, typeof(Texture2D));
				}
			}

			_outputTarget = (OutputTarget)EditorPrefs.GetInt(SettingsPrefix + "OutputTarget", (int)_outputTarget);
			_imageSequenceFormat = (ImageSequenceFormat)EditorPrefs.GetInt(SettingsPrefix + "ImageSequenceFormat", (int)_imageSequenceFormat);
			_namedPipePath = EditorPrefs.GetString(SettingsPrefix + "NamedPipePath", _namedPipePath);
			_filenamePrefixFromSceneName = EditorPrefs.GetBool(SettingsPrefix + "FilenamePrefixFromScenename", _filenamePrefixFromSceneName);
			_filenamePrefix = EditorPrefs.GetString(SettingsPrefix + "FilenamePrefix", "capture");
			_filenameExtension = EditorPrefs.GetString(SettingsPrefix + "FilenameExtension", _filenameExtension);
			_fileContainerIndex = EditorPrefs.GetInt(SettingsPrefix + "FileContainerIndex", _fileContainerIndex);
			_appendTimestamp = EditorPrefs.GetBool(SettingsPrefix + "AppendTimestamp", true);
			_imageSequenceStartFrame = EditorPrefs.GetInt(SettingsPrefix + "ImageSequenceStartFrame", 0);
			_imageSequenceZeroDigits = EditorPrefs.GetInt(SettingsPrefix + "ImageSequenceZeroDigits", 6);

			_outputFolderIndex = EditorPrefs.GetInt(SettingsPrefix + "OutputFolderIndex", (int)CaptureBase.OutputPath.RelativeToProject);
			_outputFolderRelative = EditorPrefs.GetString(SettingsPrefix + "OutputFolderRelative", "Captures");
			_outputFolderAbsolute = EditorPrefs.GetString(SettingsPrefix + "OutputFolderAbsolute", string.Empty);

			_downScaleIndex = EditorPrefs.GetInt(SettingsPrefix + "DownScaleIndex", 0);
			_downscaleX = EditorPrefs.GetInt(SettingsPrefix + "DownScaleX", 1);
			_downscaleY = EditorPrefs.GetInt(SettingsPrefix + "DownScaleY", 1);
			_frameRate = EditorPrefs.GetFloat(SettingsPrefix + "FrameRate", _frameRate);
			_timelapseScale = EditorPrefs.GetInt(SettingsPrefix + "TimelapseScale", 1);

			_renderResolution = (CaptureBase.Resolution)EditorPrefs.GetInt(SettingsPrefix + "RenderResolution", (int)_renderResolution);
			_renderSize.x = EditorPrefs.GetInt(SettingsPrefix + "RenderWidth", 0);
			_renderSize.y = EditorPrefs.GetInt(SettingsPrefix + "RenderHeight", 0);
			_renderAntiAliasing = EditorPrefs.GetInt(SettingsPrefix + "RenderAntiAliasing", 0);
			_useContributingCameras = EditorPrefs.GetBool(SettingsPrefix + "UseContributingCameras", true);

			_audioCaptureSource = (AudioCaptureSource)EditorPrefs.GetInt(SettingsPrefix + "AudioCaptureSource", (int)_audioCaptureSource);
			_audioInputDevice = DeviceManager.AudioInputDevices.FindDevice(EditorPrefs.GetString(SettingsPrefix + "AudioInputDeviceName", ""));
			_manualAudioChannelCount = Mathf.Clamp(EditorPrefs.GetInt(SettingsPrefix + "ManualAudioChannelCount", (int)_manualAudioChannelCount), 1, 8);
			_manualAudioSampleRate = Mathf.Clamp(EditorPrefs.GetInt(SettingsPrefix + "ManualAudioSampleRate", (int)_manualAudioSampleRate), 8000, 96000);

			_useMotionBlur = EditorPrefs.GetBool(SettingsPrefix + "UseMotionBlur", false);
			_motionBlurSampleCount = EditorPrefs.GetInt(SettingsPrefix + "MotionBlurSampleCount", 16);

			_render180Degrees = EditorPrefs.GetBool(SettingsPrefix + "Render180Degrees", false);
			_captureWorldSpaceGUI = EditorPrefs.GetBool(SettingsPrefix + "CaptureWorldSpaceGUI", false);
			_supportCameraRotation = EditorPrefs.GetBool(SettingsPrefix + "SupportCameraRotation", false);
			_onlyLeftRightRotation = EditorPrefs.GetBool(SettingsPrefix + "OnlyLeftRightRotation", false);
			_cubemapResolution = EditorPrefs.GetInt(SettingsPrefix + "CubemapResolution", 2048);
			_cubemapDepth = EditorPrefs.GetInt(SettingsPrefix + "CubemapDepth", 24);
			_cubemapStereoPacking = EditorPrefs.GetInt(SettingsPrefix + "CubemapStereoPacking", 0);
			_cubemapStereoIPD = EditorPrefs.GetFloat(SettingsPrefix + "CubemapStereoIPD", 0.064f);

			_startDelay = (StartDelayMode)EditorPrefs.GetInt(SettingsPrefix + "StartDelay", (int)_startDelay);
			_startDelaySeconds = EditorPrefs.GetFloat(SettingsPrefix + "StartDelaySeconds", _startDelaySeconds);

			_stopMode = (StopMode)EditorPrefs.GetInt(SettingsPrefix + "StopMode", (int)_stopMode);
			_stopFrames = EditorPrefs.GetInt(SettingsPrefix + "StopFrames", _stopFrames);
			_stopSeconds = EditorPrefs.GetFloat(SettingsPrefix + "StopSeconds", _stopSeconds);

			_encoderHints.videoHints.averageBitrate = (uint)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.AverageBitrate", (int)_encoderHints.videoHints.averageBitrate);
			_encoderHints.videoHints.maximumBitrate = (uint)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.MaximumBitrate", (int)_encoderHints.videoHints.maximumBitrate);
			_encoderHints.videoHints.quality = EditorPrefs.GetFloat(SettingsPrefix + "EncoderHints.VideoHints.Quality", _encoderHints.videoHints.quality);
			_encoderHints.videoHints.keyframeInterval = (uint)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.KeyframeInterval", (int)_encoderHints.videoHints.keyframeInterval);
			_encoderHints.videoHints.allowFastStartStreamingPostProcess = EditorPrefs.GetBool(SettingsPrefix + "EncoderHints.VideoHints.AllowFastStart", _encoderHints.videoHints.allowFastStartStreamingPostProcess);
			_encoderHints.videoHints.supportTransparency = EditorPrefs.GetBool(SettingsPrefix + "EncoderHints.VideoHints.SupportTransparency", _encoderHints.videoHints.supportTransparency);
			_encoderHints.videoHints.useHardwareEncoding = EditorPrefs.GetBool(SettingsPrefix + "EncoderHints.VideoHints.UseHardwareEncoding", _encoderHints.videoHints.useHardwareEncoding);
			_encoderHints.videoHints.enableFragmentedWriting = EditorPrefs.GetBool(SettingsPrefix + "EncoderHints.VideoHints.EnableFragmentedWriting", _encoderHints.videoHints.enableFragmentedWriting);
			_encoderHints.videoHints.movieFragmentInterval = (double)EditorPrefs.GetFloat(SettingsPrefix + "EncoderHints.VideoHints.MovieFragmentInterval", (float)_encoderHints.videoHints.movieFragmentInterval);
			_encoderHints.videoHints.injectStereoPacking = (NoneAutoCustom)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.InjectStereoPacking", (int)_encoderHints.videoHints.injectStereoPacking);
			_encoderHints.videoHints.stereoPacking = (StereoPacking)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.StereoPacking", (int)_encoderHints.videoHints.stereoPacking);
			_encoderHints.videoHints.injectSphericalVideoLayout = (NoneAutoCustom)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.InjectSphericalVideoLayout", (int)_encoderHints.videoHints.injectSphericalVideoLayout);
			_encoderHints.videoHints.sphericalVideoLayout = (SphericalVideoLayout)EditorPrefs.GetInt(SettingsPrefix + "EncoderHints.VideoHints.SphericalVideoLayout", (int)_encoderHints.videoHints.sphericalVideoLayout);
			_encoderHints.imageHints.quality = EditorPrefs.GetFloat(SettingsPrefix + "EncoderHints.ImageHints.Quality", _encoderHints.imageHints.quality);
			_encoderHints.imageHints.supportTransparency = EditorPrefs.GetBool(SettingsPrefix + "EncoderHints.ImageHints.SupportTransparency", _encoderHints.imageHints.supportTransparency);

			if (!string.IsNullOrEmpty(_cameraName))
			{
				Camera[] cameras = (Camera[])GameObject.FindObjectsOfType(typeof(Camera));
				foreach (Camera cam in cameras)
				{
					if (cam.name == _cameraName)
					{
						_cameraNode = cam;
						break;
					}
				}
			}

			_showAlpha = EditorPrefs.GetBool(SettingsPrefix + "ShowAlphaChannel", false);
			_superSizeIndex = EditorPrefs.GetInt(SettingsPrefix + "SuperSizeIndex", 0);
			_screenshotFolder = EditorPrefs.GetString(SettingsPrefix + "ScreenshotFolder", _screenshotFolder);
			_screenshotOptions.exrPrecision = (EditorScreenshot.ExrPrecision)EditorPrefs.GetInt(SettingsPrefix + "ScreenshotOptions.ExrPrecision", (int)_screenshotOptions.exrPrecision);
			_screenshotOptions.exrCompression = (EditorScreenshot.ExrCompression)EditorPrefs.GetInt(SettingsPrefix + "ScreenshotOptions.ExrCompression", (int)_screenshotOptions.exrCompression);
			_screenshotOptions.jpgQuality = EditorPrefs.GetInt(SettingsPrefix + "ScreenshotOptions.JpgQuality", (int)_screenshotOptions.jpgQuality);

			// Codecs
			_videoCodec = CodecManager.VideoCodecs.FindCodec(EditorPrefs.GetString(SettingsPrefix + "VideoCodecName", ""));
			_audioCodec = CodecManager.AudioCodecs.FindCodec(EditorPrefs.GetString(SettingsPrefix + "AudioCodecName", ""));
			UpdateSelectedCodec();

			// Camera selector
			_selectBy = (CameraSelector.SelectByMode)EditorPrefs.GetInt(SelectorPrefix + "SelectBy", (int)_selectBy);
			_scanFrequency = (CameraSelector.ScanFrequencyMode)EditorPrefs.GetInt(SelectorPrefix + "ScanFrequency", (int)_scanFrequency);
			_scanHiddenCameras = EditorPrefs.GetBool(SelectorPrefix + "ScanHiddenCameras", _scanHiddenCameras);
			_selectCameraTag = EditorPrefs.GetString(SelectorPrefix + "Tag", _selectCameraTag);
			_selectCameraName = EditorPrefs.GetString(SelectorPrefix + "Name", _selectCameraName);
		}

		private void SaveSettings()
		{
			EditorPrefs.SetInt(SettingsPrefix + "SourceType", (int)_sourceType);
			EditorPrefs.SetString(SettingsPrefix + "CameraName", _cameraName);
			EditorPrefs.SetInt(SettingsPrefix + "CaptureModeIndex", _captureModeIndex);
			EditorPrefs.SetBool(SettingsPrefix + "CaptureMouseCursor", _captureMouseCursor);
			string mouseCursorGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(_mouseCursorTexture));
			EditorPrefs.SetString(SettingsPrefix + "CaptureMouseTexture", mouseCursorGuid);

			EditorPrefs.SetInt(SettingsPrefix + "OutputTarget", (int)_outputTarget);
			EditorPrefs.SetInt(SettingsPrefix + "ImageSequenceFormat", (int)_imageSequenceFormat);
			EditorPrefs.SetString(SettingsPrefix + "NamedPipePath", _namedPipePath);
			EditorPrefs.SetBool(SettingsPrefix + "FilenamePrefixFromScenename", _filenamePrefixFromSceneName);
			EditorPrefs.SetString(SettingsPrefix + "FilenamePrefix", _filenamePrefix);
			EditorPrefs.SetString(SettingsPrefix + "FilenameExtension", _filenameExtension);
			EditorPrefs.SetInt(SettingsPrefix + "FileContainerIndex", _fileContainerIndex);
			EditorPrefs.SetBool(SettingsPrefix + "AppendTimestamp", _appendTimestamp);
			EditorPrefs.SetInt(SettingsPrefix + "ImageSequenceStartFrame", _imageSequenceStartFrame);
			EditorPrefs.SetInt(SettingsPrefix + "ImageSequenceZeroDigits", _imageSequenceZeroDigits);

			EditorPrefs.SetInt(SettingsPrefix + "OutputFolderIndex", _outputFolderIndex);
			EditorPrefs.SetString(SettingsPrefix + "OutputFolderRelative", _outputFolderRelative);
			EditorPrefs.SetString(SettingsPrefix + "OutputFolderAbsolute", _outputFolderAbsolute);

			EditorPrefs.SetInt(SettingsPrefix + "DownScaleIndex", _downScaleIndex);
			EditorPrefs.SetInt(SettingsPrefix + "DownScaleX", _downscaleX);
			EditorPrefs.SetInt(SettingsPrefix + "DownScaleY", _downscaleY);
			EditorPrefs.SetFloat(SettingsPrefix + "FrameRate", _frameRate);
			EditorPrefs.SetInt(SettingsPrefix + "TimelapseScale", _timelapseScale);

			EditorPrefs.SetInt(SettingsPrefix + "RenderResolution", (int)_renderResolution);
			EditorPrefs.SetInt(SettingsPrefix + "RenderWidth", (int)_renderSize.x);
			EditorPrefs.SetInt(SettingsPrefix + "RenderHeight", (int)_renderSize.y);
			EditorPrefs.SetInt(SettingsPrefix + "RenderAntiAliasing", _renderAntiAliasing);
			EditorPrefs.SetBool(SettingsPrefix + "UseContributingCameras", _useContributingCameras);

			EditorPrefs.SetString(SettingsPrefix + "VideoCodecName", (_videoCodec != null)?_videoCodec.Name:string.Empty);
			EditorPrefs.SetString(SettingsPrefix + "AudioCodecName", (_audioCodec != null)?_audioCodec.Name:string.Empty);

			EditorPrefs.SetInt(SettingsPrefix + "AudioCaptureSource", (int)_audioCaptureSource);
			EditorPrefs.SetString(SettingsPrefix + "AudioInputDeviceName", (_audioInputDevice != null)?_audioInputDevice.Name:string.Empty);
			EditorPrefs.SetInt(SettingsPrefix + "ManualAudioChannelCount", _manualAudioChannelCount);
			EditorPrefs.SetInt(SettingsPrefix + "ManualAudioSampleRate", _manualAudioSampleRate);

			EditorPrefs.SetBool(SettingsPrefix + "UseMotionBlur", _useMotionBlur);
			EditorPrefs.SetInt(SettingsPrefix + "MotionBlurSampleCount", _motionBlurSampleCount);

			EditorPrefs.SetBool(SettingsPrefix + "Render180Degrees", _render180Degrees);
			EditorPrefs.SetBool(SettingsPrefix + "CaptureWorldSpaceGUI", _captureWorldSpaceGUI);
			EditorPrefs.SetBool(SettingsPrefix + "SupportCameraRotation", _supportCameraRotation);
			EditorPrefs.SetBool(SettingsPrefix + "OnlyLeftRightRotation", _onlyLeftRightRotation);
			EditorPrefs.SetInt(SettingsPrefix + "CubemapResolution", _cubemapResolution);
			EditorPrefs.SetInt(SettingsPrefix + "CubemapDepth", _cubemapDepth);
			EditorPrefs.SetInt(SettingsPrefix + "CubemapStereoPacking", _cubemapStereoPacking);
			EditorPrefs.SetFloat(SettingsPrefix + "CubemapStereoIPD", _cubemapStereoIPD);

			EditorPrefs.SetInt(SettingsPrefix + "StartDelay", (int)_startDelay);
			EditorPrefs.SetFloat(SettingsPrefix + "StartDelaySeconds", _startDelaySeconds);

			EditorPrefs.SetInt(SettingsPrefix + "StopMode", (int)_stopMode);
			EditorPrefs.SetInt(SettingsPrefix + "StopFrames", _stopFrames);
			EditorPrefs.SetFloat(SettingsPrefix + "StopSeconds", _stopSeconds);

			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.AverageBitrate", (int)_encoderHints.videoHints.averageBitrate);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.MaximumBitrate", (int)_encoderHints.videoHints.maximumBitrate);
			EditorPrefs.SetFloat(SettingsPrefix + "EncoderHints.VideoHints.Quality", _encoderHints.videoHints.quality);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.KeyframeInterval", (int)_encoderHints.videoHints.keyframeInterval);
			EditorPrefs.SetBool(SettingsPrefix + "EncoderHints.VideoHints.AllowFastStart", _encoderHints.videoHints.allowFastStartStreamingPostProcess);
			EditorPrefs.SetBool(SettingsPrefix + "EncoderHints.VideoHints.SupportTransparency", _encoderHints.videoHints.supportTransparency);
			EditorPrefs.SetBool(SettingsPrefix + "EncoderHints.VideoHints.UseHardwareEncoding", _encoderHints.videoHints.useHardwareEncoding);
			EditorPrefs.SetBool(SettingsPrefix + "EncoderHints.VideoHints.EnableFragmentedWriting", _encoderHints.videoHints.enableFragmentedWriting);
			EditorPrefs.SetFloat(SettingsPrefix + "EncoderHints.VideoHints.MovieFragmentInterval", (float)_encoderHints.videoHints.movieFragmentInterval);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.InjectStereoPacking", (int)_encoderHints.videoHints.injectStereoPacking);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.StereoPacking", (int)_encoderHints.videoHints.stereoPacking);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.InjectSphericalVideoLayout", (int)_encoderHints.videoHints.injectSphericalVideoLayout);
			EditorPrefs.SetInt(SettingsPrefix + "EncoderHints.VideoHints.SphericalVideoLayout", (int)_encoderHints.videoHints.sphericalVideoLayout);
			EditorPrefs.SetFloat(SettingsPrefix + "EncoderHints.ImageHints.Quality", _encoderHints.imageHints.quality);
			EditorPrefs.SetBool(SettingsPrefix + "EncoderHints.ImageHints.SupportTransparency", _encoderHints.imageHints.supportTransparency);

			EditorPrefs.SetBool(SettingsPrefix + "ShowAlphaChannel", _showAlpha);
			EditorPrefs.SetInt(SettingsPrefix + "SuperSizeIndex", _superSizeIndex);
			EditorPrefs.SetString(SettingsPrefix + "ScreenshotFolder", _screenshotFolder);
			EditorPrefs.SetInt(SettingsPrefix + "ScreenshotOptions.ExrPrecision", (int)_screenshotOptions.exrPrecision);
			EditorPrefs.SetInt(SettingsPrefix + "ScreenshotOptions.ExrCompression", (int)_screenshotOptions.exrCompression);
			EditorPrefs.SetInt(SettingsPrefix + "ScreenshotOptions.JpgQuality", _screenshotOptions.jpgQuality);

			// Camera selector
			EditorPrefs.SetInt(SelectorPrefix + "SelectBy", (int)_selectBy);
			EditorPrefs.SetInt(SelectorPrefix + "ScanFrequency", (int)_scanFrequency);
			EditorPrefs.SetBool(SelectorPrefix + "ScanHiddenCameras", _scanHiddenCameras);
			EditorPrefs.SetString(SelectorPrefix + "Tag", _selectCameraTag);
			EditorPrefs.SetString(SelectorPrefix + "Name", _selectCameraName);
		}

		private void ResetSettings()
		{
			_sourceType = SourceType.Screen;
			_cameraNode = null;
			_cameraName = string.Empty;
			_captureModeIndex = 0;
			_captureMouseCursor = false;
			_mouseCursorTexture = null;
			_outputTarget = OutputTarget.VideoFile;
			_imageSequenceFormat = ImageSequenceFormat.PNG;
			_namedPipePath = @"\\.\pipe\test_pipe";
			_filenamePrefixFromSceneName = true;
			_filenamePrefix = "capture";
			_filenameExtension = "mp4";
			_imageSequenceStartFrame = 0;
			_imageSequenceZeroDigits = 6;
			_outputFolderIndex = (int)CaptureBase.OutputPath.RelativeToProject;
			_outputFolderRelative = "Captures";
			_outputFolderAbsolute = string.Empty;
			_appendTimestamp = true;
			_downScaleIndex = 0;
			_downscaleX = 1;
			_downscaleY = 1;
			_frameRate = 30f;
			_timelapseScale = 1;
			_videoCodec = null;
			_audioCodec = null;
			_renderResolution = CaptureBase.Resolution.Original;
			_renderSize = Vector2.one;
			_renderAntiAliasing = 0;
			_useContributingCameras = true;
			_audioCaptureSource = AudioCaptureSource.None;
			_audioInputDevice = null;
			_manualAudioChannelCount = 2;
			_manualAudioSampleRate = 48000;
			_useMotionBlur = false;
			_motionBlurSampleCount = 16;
			_render180Degrees = false;
			_captureWorldSpaceGUI = false;
			_supportCameraRotation = false;
			_onlyLeftRightRotation = false;
			_cubemapResolution = 2048;
			_cubemapDepth = 24;
			_cubemapStereoPacking = 0;
			_startDelay = StartDelayMode.None;
			_startDelaySeconds = 0f;
			_stopMode = StopMode.None;
			_cubemapStereoIPD = 0.064f;
			_stopFrames = 300;
			_stopSeconds = 10f;
			_encoderHints = new EncoderHints();
			_odsSettings = new CaptureFromCamera360ODS.Settings();

			UpdateSelectedCodec();

			// Camera selector
			_selectBy = CameraSelector.SelectByMode.HighestDepthCamera;
			_scanFrequency = CameraSelector.ScanFrequencyMode.SceneLoad;
			_scanHiddenCameras = false;
			_selectCameraTag = "MainCamera";
			_selectCameraName = "Main Camera";
		}

		private void Configure(CaptureBase capture)
		{
			capture.VideoCodecPriorityWindows = new string[0];
			capture.VideoCodecPriorityMacOS = new string[0];
			capture.AudioCodecPriorityWindows = new string[0];
			capture.AudioCodecPriorityMacOS = new string[0];

			capture.FrameRate = _frameRate;
			capture.TimelapseScale = _timelapseScale;
			capture.ResolutionDownScale = GetDownScaleFromIndex(_downScaleIndex);
			if (capture.ResolutionDownScale == CaptureBase.DownScale.Custom)
			{
				capture.ResolutionDownscaleCustom = new Vector2(_downscaleX, _downscaleY);
			}

			capture.StartDelay = _startDelay;
			capture.StartDelaySeconds = _startDelaySeconds;
			capture.StopMode = _stopMode;
			capture.StopAfterFramesElapsed = _stopFrames;
			capture.StopAfterSecondsElapsed = _stopSeconds;
			capture.SetEncoderHints(_encoderHints);

			capture.IsRealTime = IsCaptureRealTime();

			capture.OutputTarget = _outputTarget;
			if (_outputTarget == OutputTarget.VideoFile)
			{
				capture.FilenamePrefix = _filenamePrefix;
				capture.AppendFilenameTimestamp  = _appendTimestamp;
				capture.AllowManualFileExtension = true;
				capture.FilenameExtension = _filenameExtension;
			}
			else if (_outputTarget == OutputTarget.NamedPipe)
			{
				capture.NamedPipePath = _namedPipePath;
			}
			else if (_outputTarget == OutputTarget.ImageSequence)
			{
				capture.NativeImageSequenceFormat = _imageSequenceFormat;
				capture.ImageSequenceStartFrame = _imageSequenceStartFrame;
				capture.ImageSequenceZeroDigits = _imageSequenceZeroDigits;
				capture.FilenamePrefix = _filenamePrefix;
			}

			if (_outputFolderIndex == (int)CaptureBase.OutputPath.RelativeToPeristentData)
			{
				capture.OutputFolder = CaptureBase.OutputPath.RelativeToPeristentData;
				capture.OutputFolderPath = _outputFolderRelative;
			}
			else if (_outputFolderIndex == (int)CaptureBase.OutputPath.Absolute)
			{
				capture.OutputFolder = CaptureBase.OutputPath.Absolute;
				capture.OutputFolderPath = _outputFolderAbsolute;
			}
			else if (_outputFolderIndex == (int)CaptureBase.OutputPath.RelativeToDesktop)
			{
				capture.OutputFolder = CaptureBase.OutputPath.RelativeToDesktop;
				capture.OutputFolderPath = _outputFolderRelative;
			}
			else if (_outputFolderIndex == (int)CaptureBase.OutputPath.RelativeToPictures)
			{
				capture.OutputFolder = CaptureBase.OutputPath.RelativeToPictures;
				capture.OutputFolderPath = _outputFolderRelative;
			}
			else if (_outputFolderIndex == (int)CaptureBase.OutputPath.RelativeToVideos)
			{
				capture.OutputFolder = CaptureBase.OutputPath.RelativeToVideos;
				capture.OutputFolderPath = _outputFolderRelative;
			}
			else if (_outputFolderIndex == (int)CaptureBase.OutputPath.PhotoLibrary)
			{
				capture.OutputFolder = CaptureBase.OutputPath.PhotoLibrary;
				capture.OutputFolderPath = _outputFolderRelative;
			}
			else
			{
				capture.OutputFolder = CaptureBase.OutputPath.RelativeToProject;
				capture.OutputFolderPath = _outputFolderRelative;
			}

			capture.NativeForceVideoCodecIndex = (_videoCodec != null)?_videoCodec.Index:-1;

			capture.AudioCaptureSource = IsAudioCaptured()?_audioCaptureSource:AudioCaptureSource.None;
			if (capture.AudioCaptureSource != AudioCaptureSource.None)
			{
				capture.NativeForceAudioCodecIndex = (_audioCodec != null)?_audioCodec.Index:-1;
				if (capture.AudioCaptureSource == AudioCaptureSource.Microphone)
				{
					capture.ForceAudioInputDeviceIndex = (_audioInputDevice != null)?_audioInputDevice.Index:-1;
				}
				else if (capture.AudioCaptureSource == AudioCaptureSource.Manual)
				{
					capture.ManualAudioChannelCount = _manualAudioChannelCount;
					capture.ManualAudioSampleRate = _manualAudioSampleRate;
				}
			}

			if (_useMotionBlur && !capture.IsRealTime && Camera.main != null)
			{
				capture.UseMotionBlur = _useMotionBlur;
				capture.MotionBlurSamples = _motionBlurSampleCount;
				capture.MotionBlurCameras = new Camera[1];
				capture.MotionBlurCameras[0] = Camera.main;
			}
			else
			{
				capture.UseMotionBlur = false;
			}

			if (_captureScreen != null)
			{
				// Toggle mouse cursor
				if (_captureMouseCursor)
				{
					_captureScreen.CaptureMouseCursor = true;
					if (_captureScreen.MouseCursor == null)
					{
						_captureScreen.MouseCursor = capture.gameObject.AddComponent<MouseCursor>();
					}
					if (_captureScreen.MouseCursor != null)
					{
						_captureScreen.MouseCursor.SetTexture(_mouseCursorTexture);
					}
				}
				else
				{
					_captureScreen.CaptureMouseCursor = false;
					if (_captureScreen.MouseCursor != null)
					{
						_captureScreen.MouseCursor.enabled = false;
					}
				}
			}
		}

		private void CreateComponents()
		{
			// Create hidden gameobject
			if (_gameObject == null)
			{
				_gameObject = GameObject.Find(TempGameObjectName);
				if (_gameObject == null)
				{
					_gameObject = new GameObject(TempGameObjectName);
					_gameObject.hideFlags = HideFlags.HideAndDontSave;
#if UNITY_5 || UNITY_5_4_OR_NEWER
					_gameObject.hideFlags |= HideFlags.DontSaveInBuild|HideFlags.DontSaveInEditor|HideFlags.DontUnloadUnusedAsset;
#endif
					Object.DontDestroyOnLoad(_gameObject);
				}
			}

			// Remove old capture component if different
			if (_captureScreen != null && _sourceType != SourceType.Screen)
			{
				Destroy(_captureScreen);
				_captureScreen = null;
			}
			if (_captureCamera != null && _sourceType != SourceType.Camera)
			{
				Destroy(_captureCamera);
				_captureCamera = null;
			}
			if (_captureCamera360 != null && _sourceType != SourceType.Camera360)
			{
				Destroy(_captureCamera360);
				_captureCamera360 = null;
			}
			if (_captureCamera360ODS != null && _sourceType != SourceType.Camera360ODS)
			{
				Destroy(_captureCamera360ODS);
				_captureCamera360ODS = null;
			}
			if (_cameraSelector != null && _sourceType == SourceType.Screen)
			{
				Destroy(_cameraSelector);
				_cameraSelector = null;
			}

#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
			// Remove timelineController for realtime captures
			if (IsCaptureRealTime())
			{
				if (_timelineController != null)
				{
					Destroy(_timelineController);
					_timelineController = null;
				}
			}
			// Add timelineController for non-realtime captures
			else
			{
				if (_timelineController == null)
				{
					_timelineController = _gameObject.AddComponent<TimelineController>();
				}
			}
#endif
			switch (_sourceType)
			{
				case SourceType.Screen:
					if (_captureScreen == null)
					{
						_captureScreen = _gameObject.AddComponent<CaptureFromScreen>();
					}
					_capture = _captureScreen;
					break;
				case SourceType.Camera:
					if (_captureCamera == null)
					{
						_captureCamera = _gameObject.AddComponent<CaptureFromCamera>();
					}
					if (_cameraSelector == null)
					{
						_cameraSelector = _gameObject.AddComponent<CameraSelector>();
					}
					SetupCameraSelector();
					_captureCamera.SetCamera(_cameraNode, _useContributingCameras);
					_captureCamera.CameraSelector = _cameraSelector;
					_capture = _captureCamera;
					_capture.CameraRenderResolution = _renderResolution;
					_capture.CameraRenderCustomResolution = _renderSize;
					_capture.CameraRenderAntiAliasing = _renderAntiAliasing;
					break;
				case SourceType.Camera360:
					if (_captureCamera360 == null)
					{
						_captureCamera360 = _gameObject.AddComponent<CaptureFromCamera360>();
					}
					if (_cameraSelector == null)
					{
						_cameraSelector = _gameObject.AddComponent<CameraSelector>();
					}
					SetupCameraSelector();
					_capture = _captureCamera360;
					_capture.CameraRenderResolution = _renderResolution;
					_capture.CameraRenderCustomResolution = _renderSize;
					_capture.CameraRenderAntiAliasing = _renderAntiAliasing;
					_captureCamera360.SetCamera(_cameraNode);
					_captureCamera360.CameraSelector = _cameraSelector;
					_captureCamera360.Render180Degrees = _render180Degrees;
					_captureCamera360.SupportCameraRotation = _supportCameraRotation;
					_captureCamera360.OnlyLeftRightRotation = _onlyLeftRightRotation;
					_captureCamera360.SupportGUI = _captureWorldSpaceGUI;
					_captureCamera360.CubemapFaceResolution = (CaptureBase.CubemapResolution)_cubemapResolution;
					_captureCamera360.CubemapDepthResolution = (CaptureBase.CubemapDepth)_cubemapDepth;
					_captureCamera360.StereoRendering = (StereoPacking)_cubemapStereoPacking;
					_captureCamera360.IPD = _cubemapStereoIPD;
					break;
				case SourceType.Camera360ODS:
					if (_captureCamera360ODS == null)
					{
						_captureCamera360ODS = _gameObject.AddComponent<CaptureFromCamera360ODS>();
					}
					if (_cameraSelector == null)
					{
						_cameraSelector = _gameObject.AddComponent<CameraSelector>();
					}
					SetupCameraSelector();
					_capture = _captureCamera360ODS;
					_capture.CameraRenderResolution = _renderResolution;
					_capture.CameraRenderCustomResolution = _renderSize;
					_capture.CameraRenderAntiAliasing = _renderAntiAliasing;
					_captureCamera360ODS.Setup.camera = _cameraNode;
					_captureCamera360ODS.Setup.cameraSelector = _cameraSelector;
					_captureCamera360ODS.Setup.render180Degrees = _odsSettings.render180Degrees;
					_captureCamera360ODS.Setup.ipd = _odsSettings.ipd;
					_captureCamera360ODS.Setup.pixelSliceSize = _odsSettings.pixelSliceSize;
					_captureCamera360ODS.Setup.paddingSize = _odsSettings.paddingSize;
					_captureCamera360ODS.Setup.cameraClearMode = _odsSettings.cameraClearMode;
					_captureCamera360ODS.Setup.cameraClearColor= _odsSettings.cameraClearColor;
					break;
			}
#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
			if (_capture != null)
			{
				_capture.TimelineController = _timelineController;
			}
#endif
		}

		private void SetupCameraSelector()
		{
			if (_cameraSelector == null) return;

			_cameraSelector.SelectBy = _selectBy;
			_cameraSelector.ScanFrequency = _scanFrequency;
			_cameraSelector.ScanHiddenCameras = _scanHiddenCameras;
			if (_selectBy == CameraSelector.SelectByMode.Tag)
			{
				_cameraSelector.SelectTag = _selectCameraTag;
			}
			else if (_selectBy == CameraSelector.SelectByMode.Name)
			{
				_cameraSelector.SelectName = _selectCameraName;
			}
			else if (_selectBy == CameraSelector.SelectByMode.Manual)
			{
				_cameraSelector.Camera = _cameraNode;
			}
		}

		private void CreateGUI()
		{
			try
			{
				if (!NativePlugin.Init())
				{
					Debug.LogError("[AVProMovieCapture] Failed to initialise");
					return;
				}
			}
			catch (System.DllNotFoundException e)
			{
				_isFailedInit = true;
				string missingDllMessage = string.Empty;
#if (UNITY_5 || UNITY_5_4_OR_NEWER)
				missingDllMessage = "Unity couldn't find the plugin DLL. Please select the native plugin files in 'Plugins/RenderHeads/AVProMovieCapture/Plugins' folder and select the correct platform in the Inspector.";
#else
				missingDllMessage = "Unity couldn't find the plugin DLL, Unity 4.x requires the 'Plugins' folder to be at the root of your project.  Please move the contents of the 'Plugins' folder (in Plugins/RenderHeads/AVProMovieCapture/Plugins) to the 'Plugins' folder in the root of your project.";
#endif
				Debug.LogError("[AVProMovieCapture] " + missingDllMessage);
#if UNITY_EDITOR
				UnityEditor.EditorUtility.DisplayDialog("Plugin files not found", missingDllMessage, "Ok");
#endif
				throw e;
			}

			// Audio device enumeration
			{
				int numAudioDevices = Mathf.Max(0, NativePlugin.GetAudioInputDeviceCount());
				_audioDeviceNames = new string[numAudioDevices];
				for (int i = 0; i < numAudioDevices; i++)
				{
					_audioDeviceNames[i] = i.ToString("D2") + ") " + NativePlugin.GetAudioInputDeviceName(i).Replace("/", "_");
				}
			}

			_isInit = true;
		}

		private void OnEnable()
		{
			if (_icon == null)
			{
				_icon = Resources.Load<Texture2D>("AVProMovieCaptureIcon");
			}

			if (!_isCreated)
			{
				SetupWindow();
			}

			_isTrialVersion = IsTrialVersion();

			// Check that the plugin version number is not too old
			{
				string pluginVersionString = NativePlugin.GetPluginVersionString();
				_pluginVersionWarningText = string.Empty;
				if (!pluginVersionString.StartsWith(NativePlugin.ExpectedPluginVersion))
				{
					_pluginVersionWarningText = "Warning: Plugin version number " + pluginVersionString + " doesn't match the expected version number " + NativePlugin.ExpectedPluginVersion + ".  It looks like the plugin didn't upgrade correctly.  To resolve this please restart Unity and try to upgrade the package again.";
				}
			}
		}

		private void OnDisable()
		{
			SaveSettings();
			StopCapture();
			if (_gameObject != null)
			{
				DestroyImmediate(_gameObject);
				_gameObject = null;
				_capture = null;
				_captureScreen = null;
				_captureCamera = null;
				_captureCamera360 = null;
				_captureCamera360ODS = null;
				_cameraSelector = null;
				#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
				_timelineController = null;
				#endif
			}
			_isInit = false;
			_isCreated = false;
			Repaint();
		}

		private void StartCapture()
		{
			_lastFileSize = 0;
			_lastEncodedSeconds = 0;
			_lastEncodedMinutes = 0;
			_lastEncodedFrame = 0;

			CreateComponents();
			if (_capture != null)
			{
				Configure(_capture);
				_capture.SelectVideoCodec();
				if (_capture.AudioCaptureSource != AudioCaptureSource.None)
				{
					_capture.SelectAudioCodec();
					_capture.SelectAudioInputDevice();
				}
				_capture.QueueStartCapture();
			}
		}

		private void StopCapture(bool cancelCapture = false)
		{
			if (_capture != null)
			{
				if (_capture.IsCapturing())
				{
					if (!cancelCapture)
					{
						_capture.StopCapture();
					}
					else
					{
						_capture.CancelCapture();
					}
				}
				_capture = null;
			}
		}

		// Updates 10 times/second
		void OnInspectorUpdate()
		{
			if (_capture != null)
			{
				if (Application.isPlaying)
				{
					if (_capture.IsCapturing())
					{
						_lastFileSize = _capture.GetCaptureFileSize();
					}

					if (!_capture.IsRealTime)
					{
						_lastEncodedSeconds = (uint)Mathf.FloorToInt((float)_capture.CaptureStats.NumEncodedFrames / _capture.FrameRate);
					}
					else
					{
						_lastEncodedSeconds = _capture.CaptureStats.TotalEncodedSeconds;
					}
					_lastEncodedMinutes = _lastEncodedSeconds / 60;
					_lastEncodedSeconds = _lastEncodedSeconds % 60;
					_lastEncodedFrame = _capture.CaptureStats.NumEncodedFrames % (uint)_capture.FrameRate;

					// If the capture has stopped automatically, we need to update the UI
					if (!_capture.IsPrepared() || (_capture.StopMode != StopMode.None && _capture.CaptureStats.NumEncodedFrames > 0 && !_capture.IsCapturing() && !_capture.IsStartCaptureQueued()))
					{
						StopCapture();
					}
				}
				else
				{
					StopCapture();
				}
			}
			else
			{
				if (_queueConfigureVideoCodec != null)
				{
					Codec tempCodec = _queueConfigureVideoCodec;
					_queueConfigureVideoCodec = null;
					tempCodec.ShowConfigWindow();
				}
				if (_queueConfigureAudioCodec != null)
				{
					Codec tempCodec = _queueConfigureAudioCodec;
					_queueConfigureAudioCodec = null;
					tempCodec.ShowConfigWindow();
				}

				if (_queueStart && Application.isPlaying)
				{
					_queueStart = false;
					StartCapture();
				}
			}

			Repaint();
		}

		private struct MediaApiItemMenuData
		{
			public MediaApiItemMenuData(IMediaApiItem item)
			{
				this.item = item;
			}

			public IMediaApiItem item;
		}

		private void MediaApiItemMenuCallback_Select(object obj)
		{
			if (((MediaApiItemMenuData)obj).item is Codec)
			{
				Codec codec = (Codec)((MediaApiItemMenuData)obj).item;

				if (codec.CodecType == CodecType.Video)
				{
					_videoCodec = codec;
				}
				else if (codec.CodecType == CodecType.Audio)
				{
					_audioCodec = codec;
				}
			}
			else if (((MediaApiItemMenuData)obj).item is Device)
			{
				Device device = (Device)((MediaApiItemMenuData)obj).item;

				if (device.DeviceType == DeviceType.AudioInput)
				{
					_audioInputDevice = device;
				}
			}
			UpdateSelectedCodec();
		}

		private GenericMenu CreateMediaItemMenu(System.Collections.IEnumerable items, IMediaApiItem selectedItem, IMediaApiItem matchMediaType)
		{
			GenericMenu menu = new GenericMenu();

#if UNITY_EDITOR_WIN
			MediaApi lastApi = MediaApi.Unknown;
#endif

			foreach (IMediaApiItem item in items)
			{
				bool isEnabled = (matchMediaType == null || matchMediaType.MediaApi == item.MediaApi);
#if UNITY_EDITOR_WIN
				if (isEnabled && item.MediaApi != lastApi)
				{
					string title = string.Empty;
					switch (item.MediaApi)
					{
						case MediaApi.DirectShow:
						title = "DirectShow Legacy API:";
						break;
						case MediaApi.MediaFoundation:
						title = "Media Foundation API:";
						break;
					}
					menu.AddSeparator("");
					menu.AddDisabledItem(new GUIContent(title));
					lastApi = item.MediaApi;
				}
#endif
				if (isEnabled)
				{
					// Have to replace '/' as Unity context menu uses to this indicate a sub-menu
					string displayName = item.Name.Replace("/", "⁄");
					menu.AddItem(new GUIContent(displayName), item == selectedItem, MediaApiItemMenuCallback_Select, new MediaApiItemMenuData(item));
				}
				else
				{
					//menu.AddDisabledItem(new GUIContent(item.Name));
				}
			}

			return menu;
		}

		private bool ShowMediaItemList(string title, System.Collections.IEnumerable itemList, IMediaApiItem selectedItem, IMediaApiItem matchMediaType = null)
		{
			bool result = false;

			if (itemList == null || selectedItem == null)
			{
				return result;
			}

			if (!string.IsNullOrEmpty(title))
			{
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			}

			EditorGUILayout.BeginHorizontal();
			var rect = EditorGUILayout.GetControlRect(false);
			if (EditorGUI.DropdownButton(rect, new GUIContent(selectedItem.Name), FocusType.Keyboard))
			{
				CreateMediaItemMenu(itemList, selectedItem, matchMediaType).DropDown(rect);
			}

#if UNITY_EDITOR_WIN
			if (selectedItem is Codec)
			{
				EditorGUI.BeginDisabledGroup(!((Codec)selectedItem).HasConfigwindow);
				if (GUILayout.Button("Configure"))
				{
					result = true;
				}
				EditorGUI.EndDisabledGroup();
			}
#endif
			EditorGUILayout.EndHorizontal();

			return result;
		}

		private static bool ShowConfigList(string title, string[] items, bool[] isConfigurable, ref Codec codec, ref int itemIndex, ref bool itemChanged, bool showConfig = true, bool listEnabled = true)
		{
			bool result = false;

			if (itemIndex < 0 || items == null)
				return result;

			if (!string.IsNullOrEmpty(title))
			{
				EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
			}
			EditorGUI.BeginDisabledGroup(!listEnabled);
			EditorGUILayout.BeginHorizontal();
			int newItemIndex = EditorGUILayout.Popup(itemIndex, items);
			itemChanged = (newItemIndex != itemIndex);
			itemIndex = newItemIndex;

#if UNITY_EDITOR_WIN
			if (showConfig && isConfigurable != null && itemIndex < isConfigurable.Length)
			{
				EditorGUI.BeginDisabledGroup(itemIndex == 0 || !isConfigurable[itemIndex]);
				if (GUILayout.Button("Configure"))
				{
					result = true;
				}
				EditorGUI.EndDisabledGroup();
			}
#endif
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();

			return result;
		}

		void OnGUI()
		{
			if ((Application.platform != RuntimePlatform.WindowsEditor)
			&&  (Application.platform != RuntimePlatform.OSXEditor))
			{
				EditorGUILayout.LabelField("AVPro Movie Capture Window only works on the Windows and macOS platforms.");
				return;
			}

			if (!_isInit)
			{
				if (_isFailedInit)
				{
					GUILayout.Label("Error", EditorStyles.boldLabel);
					GUI.enabled = false;

					string missingDllMessage = string.Empty;
#if (UNITY_5 || UNITY_5_4_OR_NEWER)
					missingDllMessage = "Unity couldn't find the plugin DLL. Please select the native plugin files in 'Plugins/RenderHeads/AVProMovieCapture/Plugins' folder and select the correct platform in the Inspector.";
#else
					missingDllMessage = "Unity couldn't find the plugin DLL, Unity 4.x requires the 'Plugins' folder to be at the root of your project.  Please move the contents of the 'Plugins' folder (in Plugins/RenderHeads/AVProMovieCapture/Plugins) to the 'Plugins' folder in the root of your project.";
#endif

					GUILayout.TextArea(missingDllMessage);
					GUI.enabled = true;
					return;
				}
				else
				{
					EditorGUILayout.LabelField("Initialising...");
					return;
				}
			}

			if (!string.IsNullOrEmpty(_pluginVersionWarningText))
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea(_pluginVersionWarningText);
				GUI.color = Color.white;
			}

			if (_so == null)
			{
				return;
			}

			_so.Update();

#if AVPRO_MOVIECAPTURE_GRAPHICSDEVICETYPE_51 && UNITY_EDITOR_WIN
			if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D11 &&
				SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Direct3D12)
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Consider switching to D3D11 or D3D12 for best capture results.  You may need to change your Build platform to Windows.");
				GUI.color = Color.white;
			}
#endif

			if (_isTrialVersion)
			{
				EditorUtils.DrawSectionColored("- AVPRO MOVIE CAPTURE - TRIAL VERSION", ref _expandSectionTrial, DrawTrialMessage, Color.magenta, Color.magenta, Color.magenta);
				//EditorGUILayout.Space();
			}

			DrawControlButtonsGUI();
			EditorGUILayout.Space();

			// Live Capture Stats
			if (Application.isPlaying && _capture != null && (_capture.IsCapturing() || _capture.IsStartCaptureQueued()))
			{
				if (_propStopMode.enumValueIndex != (int)StopMode.None)
				{
					Rect r = GUILayoutUtility.GetRect(128f, EditorStyles.label.CalcHeight(GUIContent.none, 32f), GUILayout.ExpandWidth(true));
					float progress = _capture.GetProgress();
					EditorGUI.ProgressBar(r, progress, (progress * 100f).ToString("F1") + "%");
				}

				_scroll = EditorGUILayout.BeginScrollView(_scroll);
				DrawBaseCapturingGUI(_capture);
				DrawMoreCapturingGUI();
				EditorGUILayout.EndScrollView();
			}
			// Configuration
			else if (_capture == null)
			{
				string[] _toolNames = { "Settings", "Screenshot", "Help" };
				_selectedTool = GUILayout.Toolbar(_selectedTool, _toolNames);
				switch (_selectedTool)
				{
					case 0:
						DrawConfigGUI_Toolbar();
						_scroll = EditorGUILayout.BeginScrollView(_scroll);
						DrawConfigGUI();
						EditorGUILayout.EndScrollView();
						break;
					case 1:
						_scroll = EditorGUILayout.BeginScrollView(_scroll);
						DrawConfigGUI_Screenshot();
						EditorGUILayout.EndScrollView();
						break;
					case 2:
						_scroll = EditorGUILayout.BeginScrollView(_scroll);
						DrawConfigGUI_About();
						EditorGUILayout.EndScrollView();
						break;
				}
			}

			if (_so.ApplyModifiedProperties())
			{
				EditorUtility.SetDirty(this);
			}
		}

		private void DrawTrialMessage()
		{
			string message = "The free trial version is watermarked.  Upgrade to the full package to remove the watermark.";

			//GUI.backgroundColor = Color.yellow;
			//EditorGUILayout.BeginVertical(GUI.skin.box);
			//GUI.color = Color.yellow;
			//GUILayout.Label("AVPRO MOVIE CAPTURE - FREE TRIAL VERSION", EditorStyles.boldLabel);
			GUI.color = Color.white;
			GUILayout.Label(message, EditorStyles.wordWrappedLabel);
			if (GUILayout.Button("Upgrade Now"))
			{
				Application.OpenURL(LinkAssetStorePage);
			}
			//EditorGUILayout.EndVertical();
			GUI.backgroundColor = Color.white;
			GUI.color = Color.white;
		}

		private void DrawControlButtonsGUI()
		{
			EditorGUILayout.BeginHorizontal();
			if (_capture == null)
			{
				GUI.backgroundColor = Color.green;
				string startString = "Start Capture";
				if (!IsCaptureRealTime())
				{
					startString = "Start Render";
				}
				if (GUILayout.Button(startString, GUILayout.Height(32f)))
				{
					bool isReady = true;
					if (_sourceType == SourceType.Camera &&
						_cameraNode == null &&
						_selectBy == CameraSelector.SelectByMode.Manual)
					{
						if ((ConfigTabs)_selectedConfigTab != ConfigTabs.Capture)
						{
							_cameraNode = Utils.GetUltimateRenderCamera();
						}
						if (_cameraNode == null)
						{
							Debug.LogError("[AVProMovieCapture] Please select a Camera to capture from, or select to capture from Screen.");
							isReady = false;
						}
					}

					if (isReady)
					{
						if (!Application.isPlaying)
						{
							EditorApplication.isPlaying = true;
							_queueStart = true;
						}
						else
						{
							StartCapture();
							Repaint();
						}
					}
				}
			}
			else
			{
				GUI.backgroundColor = Color.cyan;
				if (GUILayout.Button("Cancel", GUILayout.Height(32f)))
				{
					StopCapture(true);
					Repaint();
				}
				GUI.backgroundColor = Color.red;
				if (GUILayout.Button("Stop", GUILayout.Height(32f)))
				{
					StopCapture(false);
					Repaint();
				}

				if (_capture != null)
				{
					if (_capture.IsPaused())
					{
						GUI.backgroundColor = Color.green;
						if (GUILayout.Button("Resume", GUILayout.Height(32f)))
						{
							_capture.ResumeCapture();
							Repaint();
						}
					}
					else
					{
						GUI.backgroundColor = Color.yellow;
						if (GUILayout.Button("Pause", GUILayout.Height(32f)))
						{
							_capture.PauseCapture();
							Repaint();
						}
					}
				}
			}
			EditorGUILayout.EndHorizontal();

			GUI.backgroundColor = Color.white;

			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Browse"))
			{
				if (!string.IsNullOrEmpty(CaptureBase.LastFileSaved))
				{
					Utils.ShowInExplorer(CaptureBase.LastFileSaved);
				}
			}
			{
				Color prevColor = GUI.color;
				GUI.color = Color.cyan;
				if (GUILayout.Button("View Last Capture"))
				{
					if (!string.IsNullOrEmpty(CaptureBase.LastFileSaved))
					{
						Utils.OpenInDefaultApp(CaptureBase.LastFileSaved);
					}
				}
				GUI.color = prevColor;
			}
			GUILayout.EndHorizontal();
		}


		public static void DrawBaseCapturingGUI(CaptureBase capture)
		{
			GUILayout.Space(8.0f);
			Texture texture = capture.GetPreviewTexture();
			if (texture != null)
			{
				float aspect = (float)texture.width / (float)texture.height;
				GUILayout.BeginHorizontal();

				//if (Event.current.type == EventType.Repaint)
				{
					if (_showAlpha)
					{
						Rect textureRect = GUILayoutUtility.GetAspectRect(aspect);//(width, width / aspect, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(false));
						EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.ScaleToFit);
						textureRect = GUILayoutUtility.GetAspectRect(aspect);//width, width / aspect, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false)); ;
						EditorGUI.DrawTextureAlpha(textureRect, texture, ScaleMode.ScaleToFit);
					}
					else
					{
						Rect textureRect = GUILayoutUtility.GetAspectRect(aspect, GUILayout.MaxHeight(256f));//width, width / aspect, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
						EditorGUI.DrawPreviewTexture(textureRect, texture, null, ScaleMode.ScaleToFit);
					}
				}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				_showAlpha = GUILayout.Toggle(_showAlpha, "Show Alpha", GUILayout.ExpandWidth(false));
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(8.0f);
			}

			GUILayout.Label("Output", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			EditorGUI.indentLevel++;

			GUILayout.Label("Recording to: " + System.IO.Path.GetFileName(capture.LastFilePath), EditorStyles.wordWrappedLabel);
			GUILayout.Space(8.0f);

			GUILayout.Label("Video");
			EditorGUILayout.LabelField("Dimensions", capture.GetRecordingWidth() + "x" + capture.GetRecordingHeight() + " @ " + capture.FrameRate.ToString("F2") + "hz");
			if (capture.OutputTarget == OutputTarget.VideoFile)
			{
				EditorGUILayout.LabelField("Codec", (capture.SelectedVideoCodec != null)?capture.SelectedVideoCodec.Name:"None");
			}
			else if (capture.OutputTarget == OutputTarget.ImageSequence)
			{
				EditorGUILayout.LabelField("Codec", capture.NativeImageSequenceFormat.ToString());
			}

			if (capture.AudioCaptureSource != AudioCaptureSource.None)
			{
				GUILayout.Label("Audio");
				if (capture.AudioCaptureSource == AudioCaptureSource.Unity && capture.UnityAudioCapture != null)
				{
					EditorGUILayout.LabelField("Source", "Unity");
				}
				else if (capture.AudioCaptureSource == AudioCaptureSource.Microphone)
				{
					EditorGUILayout.LabelField("Source", (capture.SelectedAudioInputDevice != null)?capture.SelectedAudioInputDevice.Name:"None");
				}
				else if (capture.AudioCaptureSource == AudioCaptureSource.Wwise && capture.UnityAudioCapture != null)
				{
					EditorGUILayout.LabelField("Source", "Wwise");
				}
				EditorGUILayout.LabelField("Codec", (capture.SelectedAudioCodec!= null)?capture.SelectedAudioCodec.Name:"None");
				if (capture.UnityAudioCapture != null && (capture.AudioCaptureSource == AudioCaptureSource.Unity || capture.AudioCaptureSource == AudioCaptureSource.Wwise))
				{
					EditorGUILayout.LabelField("Sample Rate", (capture.CaptureStats.UnityAudioSampleRate/1000f).ToString("F1") + "Khz");
					EditorGUILayout.LabelField("Channels", capture.CaptureStats.UnityAudioChannelCount.ToString());
				}
			}

			EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();

			GUILayout.Space(8.0f);

			GUILayout.Label("Stats", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			EditorGUI.indentLevel++;

			if (capture.CaptureStats.FPS > 0f)
			{
				Color originalColor = GUI.color;
				if (capture.IsRealTime)
				{
					float fpsDelta = (capture.CaptureStats.FPS - capture.FrameRate);
					GUI.color = Color.red;
					if (fpsDelta > -10)
					{
						GUI.color = Color.yellow;
					}
					if (fpsDelta > -2)
					{
						GUI.color = Color.green;
					}
				}

				EditorGUILayout.LabelField("Capture Rate", string.Format("{0:0.##} / {1:F2} FPS", capture.CaptureStats.FPS, capture.FrameRate));

				GUI.color = originalColor;
			}
			else
			{
				EditorGUILayout.LabelField("Capture Rate", string.Format(".. / {0:F2} FPS", capture.FrameRate));
			}

			EditorGUILayout.LabelField("Encoded Frames", capture.CaptureStats.NumEncodedFrames.ToString());

			EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
		}

		public void DrawMoreCapturingGUI()
		{
			GUILayout.Label("More Stats", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			EditorGUI.indentLevel++;

			EditorGUILayout.LabelField("File Size", ((float)_lastFileSize / (1024f * 1024f)).ToString("F1") + "MB");
			EditorGUILayout.LabelField("Video Length", _lastEncodedMinutes.ToString("00") + ":" + _lastEncodedSeconds.ToString("00") + "." + _lastEncodedFrame.ToString("000"));
			EditorGUILayout.LabelField("Avg Bitrate", (8f * ((float)_lastFileSize / (1024f * 1024f)) / (float)((_lastEncodedMinutes * 60) + _lastEncodedSeconds)).ToString("F2") + "Mb/s");

			EditorGUILayout.PrefixLabel("Dropped Frames");
			EditorGUI.indentLevel++;
			EditorGUILayout.LabelField("In Unity", _capture.CaptureStats.NumDroppedFrames.ToString());
			EditorGUILayout.LabelField("In Encoder", _capture.CaptureStats.NumDroppedEncoderFrames.ToString());
			EditorGUI.indentLevel--;

			if (IsAudioCaptured())
			{
				if (_capture.AudioCaptureSource == AudioCaptureSource.Unity && _capture.UnityAudioCapture != null)
				{
					EditorGUILayout.LabelField("Audio Overflows", _capture.UnityAudioCapture.OverflowCount.ToString());
				}
			}

			EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
		}

		private void DrawConfigGUI_Toolbar()
		{
			_selectedConfigTab = GUILayout.Toolbar(_selectedConfigTab, _tabNames);
		}

		private void DrawConfigGUI()
		{
			switch ((ConfigTabs)_selectedConfigTab)
			{
				case ConfigTabs.Encoding:
					DrawConfigGUI_Encoding();
					break;
				case ConfigTabs.Capture:
					DrawConfigGUI_Capture();
					break;
				case ConfigTabs.Visual:
					DrawConfigGUI_Visual();
					break;
				case ConfigTabs.Audio:
					DrawConfigGUI_Audio();
					break;
			}

			GUILayout.FlexibleSpace();
		}

		public void DrawConfigGUI_Screenshot()
		{
			EditorGUILayout.PropertyField(_propScreenshotFolder, new GUIContent("Folder"));
			EditorGUILayout.PropertyField(_propScreenshotOptions, new GUIContent("Format Options"));
			EditorGUILayout.Space();

			string folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../" + _screenshotFolder));

			{
				EditorGUILayout.BeginVertical(GUI.skin.box);

				int superSize = 1;
				switch (_superSizeIndex)
				{
					case 1:
					superSize = 2;
					break;
					case 2:
					superSize = 4;
					break;
				}
				Vector2 captureSize = EditorScreenshot.GetGameViewSize() * superSize;

				GUILayout.BeginHorizontal();
				GUILayout.Label("GameView", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
				GUILayout.Label(string.Format("({0}x{1})", (int)captureSize.x, (int)captureSize.y));
				GUILayout.EndHorizontal();
				EditorGUILayout.Space();

				GUILayout.BeginHorizontal();
				GUILayout.Label("Supersample");
				_superSizeIndex = GUILayout.SelectionGrid(_superSizeIndex, new string[]{"1x", "2x", "4x"}, 4);
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Capture PNG"))
				{
					EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.PNG;
					string filePath = EditorScreenshot.GenerateFilename("Screen", format, (int)captureSize.x, (int)captureSize.y);
					filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
					EditorScreenshot.GameViewToPNG(filePath, superSize);
				}

				{
					EditorGUI.BeginDisabledGroup(!EditorScreenshot.SupportsGameViewJPGTGAEXR());
					if (GUILayout.Button("Capture JPG"))
					{
						EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.JPG;
						string filePath = EditorScreenshot.GenerateFilename("Screen", format, (int)captureSize.x, (int)captureSize.y);
						filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
						EditorScreenshot.GameViewToFile(filePath, format, _screenshotOptions, superSize);
					}
					GUILayout.EndHorizontal();
					GUILayout.BeginHorizontal();

					{
						EditorGUI.BeginDisabledGroup(!EditorScreenshot.SupportsTGA());
						if (GUILayout.Button("Capture TGA"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.TGA;
							string filePath = EditorScreenshot.GenerateFilename("Screen", format, (int)captureSize.x, (int)captureSize.y);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.GameViewToFile(filePath, format, _screenshotOptions, superSize);
						}
						EditorGUI.EndDisabledGroup();
					}
					{
						EditorGUI.BeginDisabledGroup(!EditorScreenshot.SupportsGameViewEXR());
						if (GUILayout.Button("Capture EXR"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.EXR;
							string filePath = EditorScreenshot.GenerateFilename("Screen", format, (int)captureSize.x, (int)captureSize.y);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.GameViewToFile(filePath, format, _screenshotOptions, superSize);
						}
						EditorGUI.EndDisabledGroup();
					}
					EditorGUI.EndDisabledGroup();
				}
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
			}

			EditorGUILayout.Space();

			{
				EditorGUILayout.BeginVertical(GUI.skin.box);
				GUILayout.BeginHorizontal();
				RenderTexture sceneViewTexture = EditorScreenshot.GetSceneViewTexture();
				GUILayout.Label("Scene View", EditorStyles.boldLabel, GUILayout.ExpandWidth(false));
				if (sceneViewTexture != null)
				{
					GUILayout.Label(string.Format("({0}x{1})", sceneViewTexture.width, sceneViewTexture.height));
				}
				else
				{
					GUI.color = Color.red;
					GUILayout.Label("*Missing*", EditorStyles.boldLabel);
					GUI.color = Color.white;
				}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Capture PNG"))
				{
					EditorScreenshot.SceneViewToFile("Scene", folder, EditorScreenshot.ImageFormat.PNG, _screenshotOptions);
				}
				if (GUILayout.Button("Capture JPG"))
				{
					EditorScreenshot.SceneViewToFile("Scene", folder, EditorScreenshot.ImageFormat.JPG, _screenshotOptions);
				}
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				if (GUILayout.Button("Capture TGA"))
				{
					EditorScreenshot.SceneViewToFile("Scene", folder, EditorScreenshot.ImageFormat.TGA, _screenshotOptions);
				}
				if (GUILayout.Button("Capture EXR"))
				{
					EditorScreenshot.SceneViewToFile("Scene", folder, EditorScreenshot.ImageFormat.EXR, _screenshotOptions);
				}
				GUILayout.EndHorizontal();
				GUILayout.EndVertical();
			}

			EditorGUILayout.Space();

			{
				GUILayout.BeginVertical(GUI.skin.box);
				GUILayout.Label("Cameras", EditorStyles.boldLabel);
				if (GUILayout.Button("Refresh"))
				{
					_cameras = Resources.FindObjectsOfTypeAll<Camera>();
				}
				if (_cameras != null && _cameras.Length > 0)
				{
					_cameraIndex = (int)GUILayout.HorizontalSlider(_cameraIndex, 0, _cameras.Length - 1, GUILayout.ExpandWidth(true));
					EditorGUILayout.Space();

					Camera camera = _cameras[_cameraIndex];

					string desc = _cameraIndex + "/" + _cameras.Length + ": ";
					Texture texture = Texture2D.blackTexture;
					if (camera != null)
					{
						desc += camera.name;
						if (camera.targetTexture != null)
						{
							desc += " " + camera.targetTexture.width + "x" + camera.targetTexture.height;
							#if UNITY_2019_1_OR_NEWER
							desc += " " + camera.targetTexture.graphicsFormat;
							#endif
							texture = camera.targetTexture;
						}
					}
					GUILayout.Label(desc);
					Rect textureRect = GUILayoutUtility.GetRect(256f, 256f);
					GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit);
				}

				if (_cameras != null && _cameras.Length > 0)
				{
					RenderTexture rt = _cameras[_cameraIndex].targetTexture;
					if (rt != null)
					{
						GUILayout.BeginHorizontal();
						if (GUILayout.Button("Capture PNG"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.PNG;
							string filePath = EditorScreenshot.GenerateFilename("RT-" + rt.name, format, rt.width, rt.height);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.RenderTextureToFile(filePath, format, _screenshotOptions, rt);
						}
						if (GUILayout.Button("Capture EXR"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.EXR;
							string filePath = EditorScreenshot.GenerateFilename("RT-" + rt.name, format, rt.width, rt.height);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.RenderTextureToFile(filePath, format, _screenshotOptions, rt);
						}
						GUILayout.EndHorizontal();
					}
				}
				GUILayout.EndVertical();
			}

			EditorGUILayout.Space();

			{
				GUILayout.BeginVertical(GUI.skin.box);
				GUILayout.Label("RenderTextures", EditorStyles.boldLabel);
				if (GUILayout.Button("Refresh"))
				{
					_rts = Resources.FindObjectsOfTypeAll<RenderTexture>();
				}
				if (_rts != null && _rts.Length > 0)
				{
					_rtIndex = (int)GUILayout.HorizontalSlider(_rtIndex, 0, _rts.Length - 1, GUILayout.ExpandWidth(true));
					EditorGUILayout.Space();

					RenderTexture rt = _rts[_rtIndex];

					string desc = _rtIndex + "/" + _rts.Length + ": ";
					Texture texture = Texture2D.blackTexture;
					if (rt != null)
					{
						desc += rt.name + " " + rt.width + "x" + rt.height;
						#if UNITY_2019_1_OR_NEWER
						desc += " " + rt.graphicsFormat;
						#endif
						texture = rt;
					}

					GUILayout.Label(desc);
					Rect textureRect = GUILayoutUtility.GetRect(256f, 256f);
					GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit);
				}

				if (_rts != null && _rts.Length > 0)
				{
					RenderTexture rt = _rts[_rtIndex];
					if (rt != null)
					{
						GUILayout.BeginHorizontal();
						if (GUILayout.Button("Capture PNG"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.PNG;
							string filePath = EditorScreenshot.GenerateFilename("RT-" + rt.name, format, rt.width, rt.height);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.RenderTextureToFile(filePath, format, _screenshotOptions, rt);
						}
						if (GUILayout.Button("Capture EXR"))
						{
							EditorScreenshot.ImageFormat format = EditorScreenshot.ImageFormat.EXR;
							string filePath = EditorScreenshot.GenerateFilename("RT-" + rt.name, format, rt.width, rt.height);
							filePath = EditorScreenshot.GenerateFilePath(folder, filePath);
							EditorScreenshot.RenderTextureToFile(filePath, format, _screenshotOptions, rt);
						}
						GUILayout.EndHorizontal();
					}
				}
				GUILayout.EndVertical();
			}
		}

		private static int _rtIndex;
		private static int _cameraIndex;
		private static RenderTexture[] _rts;
		private static Camera[] _cameras;

		public static void DrawConfigGUI_About()
		{
			string version = NativePlugin.GetPluginVersionString();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if (_icon == null)
			{
				_icon = Resources.Load<Texture2D>("AVProMovieCaptureIcon");
			}
			if (_icon != null)
			{
				GUILayout.Label(new GUIContent(_icon));
			}
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.color = Color.yellow;
			GUILayout.Label("AVPro Movie Capture by RenderHeads Ltd", EditorStyles.boldLabel);
			GUI.color = Color.white;
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			GUI.color = Color.yellow;
			GUILayout.Label("version " + version + " (scripts v" + NativePlugin.ScriptVersion + ")");
			GUI.color = Color.white;
			GUILayout.FlexibleSpace();
			EditorGUILayout.EndHorizontal();

			// Links
			{
				GUILayout.Space(32f);
				GUI.backgroundColor = Color.white;

				EditorGUILayout.LabelField("AVPro Movie Capture Links", EditorStyles.boldLabel);

				GUILayout.Space(8f);

				EditorGUILayout.LabelField("Documentation");
				if (GUILayout.Button("User Manual", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkUserManual);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Bugs and Support");
				if (GUILayout.Button("GitHub Issues", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkSupport);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Rate and Review (★★★★☆)", GUILayout.ExpandWidth(false));
				if (GUILayout.Button("Asset Store Page", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkAssetStorePage);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Community");
				if (GUILayout.Button("Forum Thread", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkForumPage);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Website", GUILayout.ExpandWidth(false));
				if (GUILayout.Button("Official Website", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkPluginWebsite);
				}
			}

			// Credits
			{
				GUILayout.Space(32f);
				EditorGUILayout.LabelField("Credits", EditorStyles.boldLabel);
				GUILayout.Space(8f);
				EditorUtils.CentreLabel("Programming", EditorStyles.boldLabel);
				EditorUtils.CentreLabel("Andrew Griffiths");
				EditorUtils.CentreLabel("Morris Butler");
				EditorUtils.CentreLabel("Richard Turnbull");
				EditorUtils.CentreLabel("Sunrise Wang");
				GUILayout.Space(8f);
				EditorUtils.CentreLabel("Graphics", EditorStyles.boldLabel);
				EditorUtils.CentreLabel("Jeff Rusch");
				EditorUtils.CentreLabel("Luke Godward");
			}

			// Bug reporting
			{
				GUILayout.Space(32f);

				EditorGUILayout.LabelField("Bug Reporting Notes", EditorStyles.boldLabel);

				EditorGUILayout.SelectableLabel(SupportMessage, EditorStyles.wordWrappedLabel, GUILayout.Height(180f));
			}
		}

		private void DrawConfigGUI_Capture()
		{
			//GUILayout.Label("Capture", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			//EditorGUI.indentLevel++;

			// Time
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Time", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				_captureModeIndex = EditorGUILayout.Popup("Capture Mode", _captureModeIndex, _captureModes);
				GUILayout.BeginHorizontal();
				EditorGUILayout.PropertyField(_propFrameRate, GUILayout.ExpandWidth(false));
				_propFrameRate.floatValue = Mathf.Clamp(_propFrameRate.floatValue, 0.01f, 240f);
				EditorUtils.FloatAsPopup("▶", "Common Frame Rates", _so, _propFrameRate, EditorUtils.CommonFrameRateNames, EditorUtils.CommonFrameRateValues);
				GUILayout.EndHorizontal();
				if (IsCaptureRealTime())
				{
					EditorGUILayout.PropertyField(_propTimelapseScale);
					_propTimelapseScale.intValue = Mathf.Max(1, _propTimelapseScale.intValue);
				}
				EditorGUI.indentLevel--;
			}

			// Source
			GUILayout.Space(8f);
			EditorGUILayout.LabelField("Source", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			EditorUtils.EnumAsDropdown("Source", _propSourceType, _sourceNames);

			if (_sourceType == SourceType.Camera360ODS && IsCaptureRealTime())
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Warning: This source type is very slow and not suitable for 'Realtime Capture'.  Consider changing the capture mode to 'Offline Render'.");
				GUI.color = Color.white;
			}

			EditorGUI.indentLevel--;

			//_sourceType = (SourceType)EditorGUILayout.EnumPopup("Type", _sourceType);
			if (_sourceType == SourceType.Camera || _sourceType == SourceType.Camera360 || _sourceType == SourceType.Camera360ODS)
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Camera Selector", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(_propCameraSelectorSelectBy);
				if (_propCameraSelectorSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Name)
				{
					EditorGUILayout.PropertyField(_propCameraSelectorName, _guiCameraSelectorName);

				}
				else if (_propCameraSelectorSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Tag)
				{
					EditorGUILayout.PropertyField(_propCameraSelectorTag, _guiCameraSelectorTag);
				}
				else if (_propCameraSelectorSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Manual)
				{
					if (_cameraNode == null)
					{
						_cameraNode = Utils.GetUltimateRenderCamera();
					}
					_cameraNode = (Camera)EditorGUILayout.ObjectField("Camera", _cameraNode, typeof(Camera), true);
				}
#if !SUPPORT_SCENE_VIEW_GIZMOS_CAPTURE
				else if (_propCameraSelectorSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.EditorSceneView)
				{
					GUI.color = Color.yellow;
					GUILayout.TextArea("Warning: Scene View capture only currently supports gizmo capture up to Unity 2018.2.x");
					GUI.color = Color.white;
				}
#endif
				if (_sourceType == SourceType.Camera && _cameraNode != null)
				{
					EditorGUILayout.PropertyField(_propUseContributingCameras, _guiContributingCameras);
				}

				EditorGUILayout.PropertyField(_propCameraSelectorScanFrequency);
				EditorGUILayout.PropertyField(_propCameraSelectorScanHiddenCameras);

				EditorGUI.indentLevel--;
			}


			// Screen options
			if (_sourceType == SourceType.Screen)
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Cursor", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				_captureMouseCursor = EditorGUILayout.Toggle("Capture Mouse Cursor", _captureMouseCursor);
				_mouseCursorTexture = (Texture2D)EditorGUILayout.ObjectField("Mouse Cursor Texture", _mouseCursorTexture, typeof(Texture2D), false);
				EditorGUI.indentLevel--;
			}

			// Camera overrides
			if (_sourceType == SourceType.Camera || _sourceType == SourceType.Camera360 || _sourceType == SourceType.Camera360ODS)
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Camera Overrides", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;

				{
					EditorUtils.EnumAsDropdown("Resolution", _propRenderResolution, CaptureBaseEditor.ResolutionStrings);
				}

				if (_renderResolution == CaptureBase.Resolution.Custom)
				{
					_renderSize = EditorGUILayout.Vector2Field("Size", _renderSize);
					_renderSize = new Vector2(Mathf.Clamp((int)_renderSize.x, 1, NativePlugin.MaxRenderWidth), Mathf.Clamp((int)_renderSize.y, 1, NativePlugin.MaxRenderHeight));
				}

				{
					string currentAA = "None";
					if (QualitySettings.antiAliasing > 1)
					{
						currentAA = QualitySettings.antiAliasing.ToString() + "x";
					}
					EditorUtils.IntAsDropdown("Anti-aliasing", _propRenderAntiAliasing, new string[] { "Current (" + currentAA + ")", "None", "2x", "4x", "8x" }, new int[] { -1, 1, 2, 4, 8 });
				}

				if (_cameraNode != null)
				{
					if (_cameraNode.actualRenderingPath == RenderingPath.DeferredLighting
#if AVPRO_MOVIECAPTURE_DEFERREDSHADING
					|| _cameraNode.actualRenderingPath == RenderingPath.DeferredShading
#endif
					)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Warning: Antialiasing by MSAA is not supported as camera is using deferred rendering path");
						GUI.color = Color.white;
						_renderAntiAliasing = -1;
					}
					if (_cameraNode.clearFlags == CameraClearFlags.Nothing || _cameraNode.clearFlags == CameraClearFlags.Depth)
					{
						if (_renderResolution != CaptureBase.Resolution.Original || _renderAntiAliasing != -1)
						{
							GUI.color = Color.yellow;
							GUILayout.TextArea("Warning: Overriding camera resolution or anti-aliasing when clear flag is set to " + _cameraNode.clearFlags + " may result in incorrect captures");
							GUI.color = Color.white;
						}
					}
				}

				EditorGUI.indentLevel--;
			}

			// 360 Cubemap
			if (_sourceType == SourceType.Camera360)
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("360 Camera", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				EditorGUILayout.PropertyField(_propRender180Degrees);
				{
					CaptureBase.CubemapResolution cubemapEnum = (CaptureBase.CubemapResolution)_cubemapResolution;
					_cubemapResolution = (int)((CaptureBase.CubemapResolution)EditorGUILayout.EnumPopup("Resolution", cubemapEnum));
				}
				{
					CaptureBase.CubemapDepth depthEnum = (CaptureBase.CubemapDepth)_cubemapDepth;
					_cubemapDepth = (int)((CaptureBase.CubemapDepth)EditorGUILayout.EnumPopup("Depth", depthEnum));
				}
				{
					StereoPacking stereoEnum = (StereoPacking)_cubemapStereoPacking;
					_cubemapStereoPacking = (int)((StereoPacking)EditorGUILayout.EnumPopup("Stereo Mode", stereoEnum));
				}
				if (_cubemapStereoPacking != (int)StereoPacking.None)
				{
					#if AVPRO_MOVIECAPTURE_UNITY_STEREOCUBEMAP_RENDER
					if (!PlayerSettings.enable360StereoCapture)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Warning: 360 Stereo Capture needs to be enabled in PlayerSettings");
						GUI.color = Color.white;
						if (GUILayout.Button("Enable 360 Stereo Capture"))
						{
							PlayerSettings.enable360StereoCapture = true;
						}
					}
					#endif
					_cubemapStereoIPD = EditorGUILayout.FloatField("Interpupillary distance", _cubemapStereoIPD);
				}
				EditorGUILayout.PropertyField(_propCaptureWorldSpaceGUI, _guiCaptureWorldSpaceUI);
				EditorGUILayout.PropertyField(_propSupportCameraRotation, _guiCameraRotation);

				if (_propSupportCameraRotation.boolValue)
				{
					EditorGUILayout.PropertyField(_propOnlyLeftRightRotation);
				}
				EditorGUI.indentLevel--;
			}

			// 360 Cubemap ODS
			if (_sourceType == SourceType.Camera360ODS)
			{
				GUILayout.Space(8f);
				EditorGUI.indentLevel++;
				EditorGUILayout.LabelField("Source Options", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				{
					EditorGUILayout.PropertyField(_propOdsRender180Degrees);
					EditorGUILayout.PropertyField(_propOdsIPD, _guiInterpupillaryDistance);
					EditorGUILayout.PropertyField(_propOdsPixelSliceSize);
					EditorGUILayout.PropertyField(_propOdsPaddingSize);
					EditorGUILayout.PropertyField(_propOdsCameraClearMode);
					EditorGUILayout.PropertyField(_propOdsCameraClearColor);
				}
				EditorGUI.indentLevel--;
				EditorGUI.indentLevel--;
			}

			// Start / Stop
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Start / Stop", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;

				EditorGUILayout.PropertyField(_propStartDelay, _guiStartDelay);

				if ((StartDelayMode)_propStartDelay.enumValueIndex == StartDelayMode.RealSeconds ||
					(StartDelayMode)_propStartDelay.enumValueIndex == StartDelayMode.GameSeconds)
				{
					EditorGUILayout.PropertyField(_propStartDelaySeconds, _guiSeconds);
				}

				EditorGUILayout.Separator();

				_stopMode = (StopMode)EditorGUILayout.EnumPopup("Stop Mode", _stopMode);
				if (_stopMode == StopMode.FramesEncoded)
				{
					_stopFrames = EditorGUILayout.IntField("Frames", _stopFrames);
				}
				else if (_stopMode == StopMode.SecondsElapsed || _stopMode == StopMode.SecondsEncoded)
				{
					_stopSeconds = EditorGUILayout.FloatField("Seconds", _stopSeconds);
				}
				EditorGUI.indentLevel--;
			}

			GUILayout.Space(8f);
			if (GUILayout.Button("Reset All Settings"))
			{
				ResetSettings();
			}
			GUILayout.Space(4f);

			//EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
		}

		private void DrawConfigGUI_Encoding()
		{
			//GUILayout.Label("Target", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			//EditorGUI.indentLevel++;

			EditorUtils.EnumAsDropdown("Output Target", _propOutputTarget, EditorUtils.OutputTargetNames);

			GUILayout.Space(8f);
			if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile ||
				_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence)
			{
				bool isImageSequence = (_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence);

				if (isImageSequence)
				{
					EditorUtils.EnumAsDropdown("Format", _propImageSequenceFormat, Utils.GetNativeImageSequenceFormatNames());
					GUILayout.Space(8f);
				}

				// File path
				EditorGUILayout.LabelField("File Path", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				_outputFolderIndex = EditorGUILayout.Popup("Relative to", _outputFolderIndex, _outputFolders);
				if (_outputFolderIndex != (int)CaptureBase.OutputPath.Absolute)
				{
					_outputFolderRelative = EditorGUILayout.TextField("SubFolder(s)", _outputFolderRelative);
				}
				else
				{
					EditorGUILayout.BeginHorizontal();
					_outputFolderAbsolute = EditorGUILayout.TextField("Path", _outputFolderAbsolute);
					if (GUILayout.Button(">", GUILayout.Width(22)))
					{
						_outputFolderAbsolute = EditorUtility.SaveFolderPanel("Select Folder To Store Video Captures", System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../")), "");
						EditorUtility.SetDirty(this);
					}
					EditorGUILayout.EndHorizontal();
				}
				EditorGUI.indentLevel--;

				GUILayout.Space(8f);

				// File name
				EditorGUILayout.LabelField("File Name", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				_filenamePrefixFromSceneName = EditorGUILayout.Toggle("From Scene Name", _filenamePrefixFromSceneName);
				if (_filenamePrefixFromSceneName)
				{
#if AVPRO_MOVIECAPTURE_SCENEMANAGER_53
					string currentScenePath = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().path;
#else
					string currentScenePath = EditorApplication.currentScene;
#endif
					_filenamePrefix = System.IO.Path.GetFileNameWithoutExtension(currentScenePath);
					if (string.IsNullOrEmpty(_filenamePrefix))
					{
						_filenamePrefix = "capture";
					}
				}
				EditorGUI.BeginDisabledGroup(_filenamePrefixFromSceneName);
				_filenamePrefix = EditorGUILayout.TextField("Prefix", _filenamePrefix);
				EditorGUI.EndDisabledGroup();
				if (!isImageSequence)
				{
					_appendTimestamp = EditorGUILayout.Toggle("Append Timestamp", _appendTimestamp);
				}
				else
				{
					EditorGUILayout.PropertyField(_propImageSequenceStartFrame, _guiStartFrame);
					EditorGUILayout.PropertyField(_propImageSequenceZeroDigits, _guiZeroDigits);
				}
				EditorGUI.indentLevel--;
				GUILayout.Space(8f);

				// File container
				if (!isImageSequence)
				{
					EditorGUILayout.LabelField("File Container", EditorStyles.boldLabel);
					EditorGUI.indentLevel++;
					_fileContainerIndex = EditorGUILayout.Popup("Extension", _fileContainerIndex, _fileExtensions);
					if (_fileContainerIndex >= 0  && _fileContainerIndex < _fileExtensions.Length)
					{
						_filenameExtension = _fileExtensions[_fileContainerIndex].ToLower();
					}
					EditorGUI.indentLevel--;
				}
			}
			else if (_propOutputTarget.enumValueIndex == (int)OutputTarget.NamedPipe)
			{
				EditorGUILayout.PropertyField(_propNamedPipePath);
			}

			DrawVisualCodecList();
			DrawAudioCodecList();

			if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile ||
				_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence)
			{
				GUILayout.Space(8f);
				EditorGUILayout.LabelField("Encoder Hints", EditorStyles.boldLabel);
				EditorGUI.indentLevel++;
				if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile)
				{
					EditorUtils.BitrateField("Average Bitrate", _propVideoHintsAverageBitrate);
#if UNITY_EDITOR_WIN
					EditorUtils.BitrateField("Maxiumum Bitrate", _propVideoHintsMaximumBitrate);

					// Ensure that the maximum value is larger than the average value, or zero
					if (_propVideoHintsMaximumBitrate.intValue != 0)
					{
						_propVideoHintsMaximumBitrate.intValue = Mathf.Max(_propVideoHintsMaximumBitrate.intValue, _propVideoHintsAverageBitrate.intValue);
					}
#endif
					EditorGUILayout.PropertyField(_propVideoHintsQuality);
					EditorGUILayout.PropertyField(_propVideoHintsKeyframeInterval);
					EditorGUILayout.PropertyField(_propVideoHintsSupportTransparency);

#if UNITY_EDITOR_WIN
					EditorGUILayout.PropertyField(_propVideoHintsUseHardwareEncoding, new GUIContent("Hardware Encoding"));
#elif UNITY_EDITOR_OSX
					EditorGUILayout.PropertyField(_propVideoHintsEnableFragmentedWriting);
					if (_propVideoHintsEnableFragmentedWriting.boolValue)
					{
						EditorGUILayout.PropertyField(_propVideoHintsMovieFragmentInterval);
					}
#endif
				}
				else if (_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence)
				{
#if UNITY_EDITOR_OSX
					EditorGUILayout.PropertyField(_propImageHintsQuality);
#endif
					EditorGUILayout.PropertyField(_propImageHintsSupportTransparency);
				}
				EditorGUI.indentLevel--;

				if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile)
				{
					GUILayout.Space(8f);
					EditorGUILayout.LabelField("Post Process", EditorStyles.boldLabel);
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(_propVideoHintsAllowFastStart);

					EditorGUILayout.PropertyField(_propVideoHintsInjectStereoPacking, _guiStereoPacking);
					if (_propVideoHintsInjectStereoPacking.enumValueIndex == (int)NoneAutoCustom.Custom)
					{
						EditorGUILayout.PropertyField(_propVideoHintsStereoPacking, _guiBlankSpace);
					}
					EditorGUILayout.PropertyField(_propVideoHintsInjectSphericalVideoLayout, _guiSphericalLayout);
					if (_propVideoHintsInjectSphericalVideoLayout.enumValueIndex == (int)NoneAutoCustom.Custom)
					{
						EditorGUILayout.PropertyField(_propVideoHintsSphericalVideoLayout, _guiBlankSpace);
					}
				}
			}

			//EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
		}

		private void DrawConfigGUI_Visual()
		{
			//GUILayout.Label("Video", EditorStyles.boldLabel);
			EditorGUILayout.BeginVertical(GUI.skin.box);
			//EditorGUI.indentLevel++;

			{
				Vector2 outSize = Vector2.zero;
				if (_sourceType == SourceType.Screen)
				{
					// We can't just use Screen.width and Screen.height because Unity returns the size of this window
					// So instead we look for a camera with no texture target and a valid viewport
					int inWidth = 1;
					int inHeight = 1;
					foreach (Camera cam in Camera.allCameras)
					{
						if (cam.targetTexture == null)
						{
							float rectWidth = Mathf.Clamp01(cam.rect.width + cam.rect.x) - Mathf.Clamp01(cam.rect.x);
							float rectHeight = Mathf.Clamp01(cam.rect.height + cam.rect.y) - Mathf.Clamp01(cam.rect.y);
							if (rectWidth > 0.0f && rectHeight > 0.0f)
							{
								inWidth = Mathf.FloorToInt(cam.pixelWidth / rectWidth);
								inHeight = Mathf.FloorToInt(cam.pixelHeight / rectHeight);
								//Debug.Log (rectWidth + "    " + (cam.rect.height - cam.rect.y) + " " + cam.pixelHeight + " = " + inWidth + "x" + inHeight);
								break;
							}
						}
					}
					outSize = CaptureBase.GetRecordingResolution(inWidth, inHeight, GetDownScaleFromIndex(_downScaleIndex), new Vector2(_downscaleX, _downscaleY));
				}
				else
				{
					if (_cameraNode != null)
					{
						int inWidth = Mathf.FloorToInt(_cameraNode.pixelRect.width);
						int inHeight = Mathf.FloorToInt(_cameraNode.pixelRect.height);

						if (_renderResolution != CaptureBase.Resolution.Original)
						{
							float rectWidth = Mathf.Clamp01(_cameraNode.rect.width + _cameraNode.rect.x) - Mathf.Clamp01(_cameraNode.rect.x);
							float rectHeight = Mathf.Clamp01(_cameraNode.rect.height + _cameraNode.rect.y) - Mathf.Clamp01(_cameraNode.rect.y);

							if (_renderResolution == CaptureBase.Resolution.Custom)
							{
								inWidth = (int)_renderSize.x;
								inHeight = (int)_renderSize.y;
							}
							else
							{
								CaptureBase.GetResolution(_renderResolution, ref inWidth, ref inHeight);
								inWidth = Mathf.FloorToInt(inWidth * rectWidth);
								inHeight = Mathf.FloorToInt(inHeight * rectHeight);
							}
						}

						outSize = CaptureBase.GetRecordingResolution(inWidth, inHeight, GetDownScaleFromIndex(_downScaleIndex), new Vector2(_downscaleX, _downscaleY));
					}
				}

				GUILayout.Space(8f);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUI.color = Color.cyan;
				GUILayout.TextArea("Output: " + (int)outSize.x + " x " + (int)outSize.y + " @ " + _frameRate.ToString("F2"));
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(8f);
				GUI.color = Color.white;

			}

			_downScaleIndex = EditorGUILayout.Popup("Down Scale", _downScaleIndex, _downScales);
			if (_downScaleIndex == 5)
			{
				Vector2 maxVideoSize = new Vector2(_downscaleX, _downscaleY);
				maxVideoSize = EditorGUILayout.Vector2Field("Size", maxVideoSize);
				_downscaleX = Mathf.Clamp((int)maxVideoSize.x, 1, NativePlugin.MaxRenderWidth);
				_downscaleY = Mathf.Clamp((int)maxVideoSize.y, 1, NativePlugin.MaxRenderHeight);
			}

			GUILayout.Space(8f);
			//EditorGUILayout.LabelField("Codec", EditorStyles.boldLabel);

			DrawConfigGUI_MotionBlur();

			//EditorGUI.indentLevel--;
			EditorGUILayout.EndVertical();
		}

		private void UpdateSelectedCodec()
		{
			// Assign the first codec if none is selected
			if (_videoCodec == null && CodecManager.VideoCodecs.Count > 0)
			{
				_videoCodec = CodecManager.VideoCodecs.Codecs[0];
			}
			if (_videoCodec != null)
			{
				// Ensure the audio codec uses the same API as the video codec,
				// otherwise assign the first audio codec
				if (_audioCodec == null || (_audioCodec != null && _audioCodec.MediaApi != _videoCodec.MediaApi))
				{
					_audioCodec = CodecManager.AudioCodecs.GetFirstWithMediaApi(_videoCodec.MediaApi);
				}
				if (_audioInputDevice == null || (_audioInputDevice != null && _audioInputDevice.MediaApi != _videoCodec.MediaApi))
				{
					_audioInputDevice = DeviceManager.AudioInputDevices.GetFirstWithMediaApi(_videoCodec.MediaApi);
				}
			}

			// Select the appropriate file extension based on the selected codecs
			UpdateFileExtension();
		}

		private void UpdateFileExtension()
		{
			// There are cases where the plugin is deinitalised, so it needs to be inited to be able to query the file extension
			if (NativePlugin.GetVideoCodecCount() <= 0)
			{
				NativePlugin.Init();
			}

			_fileExtensions = GetSuitableFileExtensions(_videoCodec, IsAudioCaptured()?_audioCodec:null);
			if (_fileContainerIndex >= _fileExtensions.Length)
			{
				_fileContainerIndex = 0;
			}
			if (_fileContainerIndex < _fileExtensions.Length)
			{
				_filenameExtension = _fileExtensions[_fileContainerIndex].ToLower();
			}
		}

		private void DrawVisualCodecList()
		{
			GUILayout.Space(8f);
			if (_outputTarget == OutputTarget.VideoFile)
			{
				if (_videoCodec == null && CodecManager.VideoCodecs.Count > 0)
				{
					_videoCodec = CodecManager.VideoCodecs.Codecs[0];
				}
				if (_audioCodec == null && CodecManager.AudioCodecs.Count > 0)
				{
					_audioCodec = CodecManager.AudioCodecs.Codecs[0];
				}
				if (ShowMediaItemList("Video Codec", CodecManager.VideoCodecs, _videoCodec, null))
				{
					_queueConfigureVideoCodec = _videoCodec;
				}

				if (_videoCodec != null)
				{
#if UNITY_EDITOR_WIN
					if (_videoCodec.MediaApi == MediaApi.DirectShow)
					{
						if (_videoCodec.Name.EndsWith("Cinepak Codec by Radius")
								|| _videoCodec.Name.EndsWith("DV Video Encoder")
								|| _videoCodec.Name.EndsWith("Microsoft Video 1")
								|| _videoCodec.Name.EndsWith("Microsoft RLE")
								|| _videoCodec.Name.EndsWith("Logitech Video (I420)")
								|| _videoCodec.Name.EndsWith("Intel IYUV codec")
								)
						{
							GUI.color = Color.yellow;
							GUILayout.TextArea("Warning: Legacy codec, not recommended");
							GUI.color = Color.white;
						}
						if (_videoCodec.Name.Contains("Decoder") || _videoCodec.Name.Contains("Decompressor"))
						{
							GUI.color = Color.yellow;
							GUILayout.TextArea("Warning: Codec may contain decompressor only");
							GUI.color = Color.white;
						}
						if (CodecManager.VideoCodecs.Count < 6)
						{
							GUI.color = Color.cyan;
							GUILayout.TextArea("Low number of codecs, consider installing more");
							GUI.color = Color.white;
						}
					}
#endif
					if (_videoCodec.Name.EndsWith("Uncompressed"))
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Warning: Uncompressed may result in very large files");
						GUI.color = Color.white;
					}
				}
			}
		}

		private bool IsCaptureRealTime()
		{
			return (_captureModeIndex == 0);
		}

		private bool IsAudioCaptured()
		{
			return (_outputTarget == OutputTarget.VideoFile && _audioCaptureSource != AudioCaptureSource.None && (IsCaptureRealTime() || _audioCaptureSource != AudioCaptureSource.Manual));
		}

		private void DrawAudioCodecList()
		{
			if (_outputTarget != OutputTarget.VideoFile)
			{
				return;
			}

			GUILayout.Space(8f);
			EditorGUI.BeginDisabledGroup(!IsAudioCaptured());

			if (ShowMediaItemList("Audio Codec", CodecManager.AudioCodecs, _audioCodec, _videoCodec))
			{
				_queueConfigureAudioCodec = _audioCodec;
			}

#if UNITY_EDITOR_WIN
			if (_audioCodec != null  && (_audioCodec.Name.EndsWith("MPEG Layer-3")))
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Warning: We have had reports that this codec doesn't work. Consider using a different codec");
				GUI.color = Color.white;
			}
#endif
			EditorGUI.EndDisabledGroup();
		}

		private void DrawConfigGUI_MotionBlur()
		{
			EditorGUI.BeginDisabledGroup(IsCaptureRealTime());
			//EditorGUILayout.BeginVertical(GUI.skin.box);
			//EditorGUI.indentLevel++;

			GUILayout.Space(8f);
			GUILayout.Label("Motion Blur (beta)", EditorStyles.boldLabel);
			//EditorGUILayout.BeginVertical(GUI.skin.box);
			//EditorGUI.indentLevel++;

			if (IsCaptureRealTime())
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Motion Blur only available in Offline Render mode");
				GUI.color = Color.white;
			}

			_useMotionBlur = EditorGUILayout.Toggle("Use Motion Blur", _useMotionBlur);
			EditorGUI.BeginDisabledGroup(!_useMotionBlur);
			EditorGUILayout.PrefixLabel("Samples");
			EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
			_motionBlurSampleCount = EditorGUILayout.IntSlider(_motionBlurSampleCount, 0, 64);
			EditorGUILayout.EndHorizontal();
			EditorGUI.EndDisabledGroup();

			//EditorGUI.indentLevel--;
			//EditorGUILayout.EndVertical();
			EditorGUI.EndDisabledGroup();
		}

		private void DrawConfigGUI_Audio()
		{
			bool showAudioSources = true;
			if (_outputTarget != OutputTarget.VideoFile)
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Audio capture only available when outputing to video file");
				GUI.color = Color.white;
				showAudioSources = false;
			}

			if (showAudioSources)
			{
				EditorGUILayout.BeginVertical(GUI.skin.box);
				_audioCaptureSource = (AudioCaptureSource)EditorGUILayout.EnumPopup("Audio Source", _audioCaptureSource);

				if (_audioCaptureSource != AudioCaptureSource.None)
				{
					bool showAudioOptions = true;

					#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
					if (!IsCaptureRealTime() && _audioCaptureSource != AudioCaptureSource.Manual && _audioCaptureSource != AudioCaptureSource.Unity && _audioCaptureSource != AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Only Manual, Unity and Wwise Audio Sources are available in offline capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#else
					if (!IsCaptureRealTime() && _audioCaptureSource != AudioCaptureSource.Manual && _audioCaptureSource != AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Only Manual and Wwise Audio Source is available in offline capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#endif
					if (IsCaptureRealTime() && _audioCaptureSource == AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Wwise Audio Source is not available in realtime capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#if !AVPRO_MOVIECAPTURE_WWISE_SUPPORT
					if (!IsCaptureRealTime() && _audioCaptureSource == AudioCaptureSource.Wwise)
					{
						GUI.color = Color.red;
						GUILayout.TextArea("To support Wwise audio capture: add AVPRO_MOVIECAPTURE_WWISE_SUPPORT to script defines in Player Settings");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#endif

					if (showAudioOptions)
					{
						if (_audioCaptureSource == AudioCaptureSource.Microphone)
						{
							GUILayout.Space(8f);

							ShowMediaItemList("Microphone", DeviceManager.AudioInputDevices, _audioInputDevice, _videoCodec);

							GUILayout.Space(8f);
						}
						else if (_audioCaptureSource == AudioCaptureSource.Manual)
						{
							EditorUtils.IntAsDropdown("Sample Rate", _propManualAudioSampleRate, EditorUtils.CommonAudioSampleRateNames, EditorUtils.CommonAudioSampleRateValues);
							EditorGUILayout.PropertyField(_propManualAudioChannelCount, new GUIContent("Channels"));
						}
					}
				}
				EditorGUILayout.EndVertical();
			}
		}

		private static string[] GetSuitableFileExtensions(Codec videoCodec, Codec audioCodec = null)
		{
			string[] result = null;
			if (videoCodec != null)
			{
				int audioCodecIndex = -1;
				if (audioCodec != null)
				{
					audioCodecIndex = audioCodec.Index;
				}
				result = NativePlugin.GetContainerFileExtensions(videoCodec.Index, audioCodecIndex);
			}
			if (result != null)
			{
				for (int i = 0; i < result.Length; i++)
				{
					result[i] = result[i].ToUpper();
				}
			}
			else
			{
				result = new string[0];
			}
			return result;
		}

		private static CaptureBase.DownScale GetDownScaleFromIndex(int index)
		{
			CaptureBase.DownScale result = CaptureBase.DownScale.Original;
			switch (index)
			{
				case 0:
					result = CaptureBase.DownScale.Original;
					break;
				case 1:
					result = CaptureBase.DownScale.Half;
					break;
				case 2:
					result = CaptureBase.DownScale.Quarter;
					break;
				case 3:
					result = CaptureBase.DownScale.Eighth;
					break;
				case 4:
					result = CaptureBase.DownScale.Sixteenth;
					break;
				case 5:
					result = CaptureBase.DownScale.Custom;
					break;
			}

			return result;
		}

		private static bool IsTrialVersion()
		{
			return NativePlugin.IsTrialVersion();
		}
	}
}
#endif