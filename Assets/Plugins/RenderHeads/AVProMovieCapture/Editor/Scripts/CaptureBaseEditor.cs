#if UNITY_2017_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
#endif
#if UNITY_5_6_OR_NEWER && UNITY_2018_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
#endif
#if UNITY_2017_1_OR_NEWER
	#define AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
#endif
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(CaptureBase), true)]
	public class CaptureBaseEditor : UnityEditor.Editor
	{
		private const string SettingsPrefix = "AVProMovieCapture-BaseEditor-";
		internal const string UnityAssetStore_FullVersionUrl = "https://assetstore.unity.com/packages/tools/video/avpro-movie-capture-151061?aid=1101lcNgx";

		public readonly static string[] ResolutionStrings = { "8192x8192 (1:1)", "8192x4096 (2:1)", "4096x4096 (1:1)", "4096x2048 (2:1)", "2048x4096 (1:2)", "3840x2160 (16:9)", "3840x2048 (15:8)", "3840x1920 (2:1)", "2560x1440 (16:9)", "2048x2048 (1:1)", "2048x1024 (2:1)", "1920x1080 (16:9)", "1280x720 (16:9)", "1024x768 (4:3)", "800x600 (4:3)", "800x450 (16:9)", "640x480 (4:3)", "640x360 (16:9)", "320x240 (4:3)", "Original", "Custom" };

		private readonly static GUIContent _guiBlankSpace = new GUIContent(" ");
		private readonly static GUIContent _guiContentMotionBlurSamples = new GUIContent("Samples");
		private readonly static GUIContent _guiContentMotionBlurCameras = new GUIContent("Cameras");
		private readonly static GUIContent _guiContentFolder = new GUIContent("Folder");
		private readonly static GUIContent _guiContentPath = new GUIContent("Path");
		private readonly static GUIContent _guiContentSubfolders = new GUIContent("Subfolder(s)");
		private readonly static GUIContent _guiContentPrefix = new GUIContent("Prefix");
		private readonly static GUIContent _guiContentAppendTimestamp = new GUIContent("Append Timestamp");
		private readonly static GUIContent _guiContentManualExtension = new GUIContent("Manual Extension");
		private readonly static GUIContent _guiContentExtension = new GUIContent("Extension");
		private readonly static GUIContent _guiContentStartFrame = new GUIContent("Start Frame");
		private readonly static GUIContent _guiContentZeroDigits = new GUIContent("Zero Digits");
		private readonly static GUIContent _guiContentPipePath = new GUIContent("Pipe Path");
		private readonly static GUIContent _guiContentToggleKey = new GUIContent("Toggle Key");
		private readonly static GUIContent _guiContentStartMode = new GUIContent("Start Mode");
		private readonly static GUIContent _guiContentStartDelay = new GUIContent("Start Delay");
		private readonly static GUIContent _guiContentSeconds = new GUIContent("Seconds");
		private readonly static GUIContent _guiContentStopMode = new GUIContent("Stop Mode");
		private readonly static GUIContent _guiContentFrames = new GUIContent("Frames");
		private readonly static GUIContent _guiContentCodecSearchOrder = new GUIContent("Codec Search Order");
		private readonly static GUIContent _guiContentSupportTextureRecreate = new GUIContent("Support Texture Recreate", "Using this option will slow rendering (forces GPU sync), but is needed to handle cases where texture resources are recreated, due to alt-tab or window resizing.");
		private readonly static GUIContent _guiStreamableMP4 = new GUIContent("Streamable MP4");
		private readonly static GUIContent _guiStereoPacking = new GUIContent("Stereo Packing");
		private readonly static GUIContent _guiSphericalLayout = new GUIContent("Spherical Layout");
		private readonly static GUIContent _guiAndroidNoCaptureRotation = new GUIContent("No Capture Rotation");

		private static bool _isTrialVersion = false;
		private SerializedProperty _propCaptureKey;
		private SerializedProperty _propMinimumDiskSpaceMB;
		private SerializedProperty _propPersistAcrossSceneLoads;

		private SerializedProperty _propIsRealtime;

		private SerializedProperty _propOutputTarget;
		private SerializedProperty _propImageSequenceFormatWindows;
		private SerializedProperty _propImageSequenceFormatMacOS;
		private SerializedProperty _propImageSequenceFormatIOS;
		private SerializedProperty _propImageSequenceFormatAndroid;
		private SerializedProperty _propImageSequenceStartFrame;
		private SerializedProperty _propImageSequenceZeroDigits;
		private SerializedProperty _propOutputFolderType;
		private SerializedProperty _propOutputFolderPath;
		private SerializedProperty _propAppendFilenameTimestamp;
		private SerializedProperty _propFileNamePrefix;
		private SerializedProperty _propAllowManualFileExtension;
		private SerializedProperty _propFileNameExtension;
		private SerializedProperty _propForceFileName;
		private SerializedProperty _propNamedPipePath;

		private SerializedProperty _propVideoCodecPriorityWindows;
		private SerializedProperty _propVideoCodecPriorityMacOS;
		//private SerializedProperty _propVideoCodecPriorityAndroid;
		private SerializedProperty _propForceVideoCodecIndexWindows;
		private SerializedProperty _propForceVideoCodecIndexMacOS;
		private SerializedProperty _propForceVideoCodecIndexIOS;
		private SerializedProperty _propForceVideoCodecIndexAndroid;

		private SerializedProperty _propAudioCaptureSource;
		private SerializedProperty _propAudioCodecPriorityWindows;
		private SerializedProperty _propAudioCodecPriorityMacOS;
		//private SerializedProperty _propAudioCodecPriorityAndroid;
		private SerializedProperty _propForceAudioCodecIndexWindows;
		private SerializedProperty _propForceAudioCodecIndexMacOS;
		private SerializedProperty _propForceAudioCodecIndexIOS;
		private SerializedProperty _propForceAudioCodecIndexAndroid;
		private SerializedProperty _propForceAudioDeviceIndex;
		private SerializedProperty _propUnityAudioCapture;
		private SerializedProperty _propManualAudioSampleRate;
		private SerializedProperty _propManualAudioChannelCount;

		private SerializedProperty _propStartTrigger;
		private SerializedProperty _propStartDelay;
		private SerializedProperty _propStartDelaySeconds;

		private SerializedProperty _propStopMode;
		private SerializedProperty _propStopFrames;
		private SerializedProperty _propStopSeconds;

		private class PropVideoHints
		{
			public SerializedProperty propAverageBitrate;
			public SerializedProperty propMaximumBitrate;
			public SerializedProperty propQuality;
			public SerializedProperty propKeyframeInterval;
			public SerializedProperty propTransparency;
			public SerializedProperty propHardwareEncoding;
			public SerializedProperty propFastStart;
			public SerializedProperty propInjectStereoPacking;
			public SerializedProperty propStereoPacking;
			public SerializedProperty propInjectSphericalVideoLayout;
			public SerializedProperty propSphericalVideoLayout;
			public SerializedProperty propEnableFragmentedWriting;
			public SerializedProperty propMovieFragmentInterval;
		}

		private class PropImageHints
		{
			public SerializedProperty propQuality;
			public SerializedProperty propTransparency;
		}

		private PropVideoHints[] _propVideoHints;
		private PropImageHints[] _propImageHints;

		private SerializedProperty _propDownScale;
		private SerializedProperty _propMaxVideoSize;
		private SerializedProperty _propFrameRate;
		private SerializedProperty _propTimelapseScale;
		private SerializedProperty _propFrameUpdateMode;
		private SerializedProperty _propFlipVertically;
		private SerializedProperty _propForceGpuFlush;
		private SerializedProperty _propWaitForEndOfFrame;
		private SerializedProperty _propAndroidNoCaptureRotation;

		private SerializedProperty _propUseMotionBlur;
		private SerializedProperty _propMotionBlurSamples;
		private SerializedProperty _propMotionBlurCameras;

		private SerializedProperty _propLogCaptureStartStop;
		private SerializedProperty _propAllowVsyncDisable;
		private SerializedProperty _propSupportTextureRecreate;
		#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
		private SerializedProperty _propTimelineController;
		#endif
		#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
		private SerializedProperty _propVideoPlayerController;
		#endif

		private static bool _isExpandedStartStop = false;
		private static bool _isExpandedOutput = false;
		private static bool _isExpandedVisual = false;
		private static bool _isExpandedAudio = false;
		private static bool _isExpandedPost = false;
		private static bool _isExpandedMisc = false;
		private static bool _isExpandedTrial = true;
		private static bool _isExpandedAbout = false;
		private static NativePlugin.Platform _selectedPlatform = NativePlugin.Platform.Windows;
		private static GUIStyle _stylePlatformBox = null;

		protected CaptureBase _baseCapture;

		public override void OnInspectorGUI()
		{
			// Warning if the base component is used
			if (this.target.GetType() == typeof(CaptureBase))
			{
				GUI.color = Color.yellow;
				GUILayout.BeginVertical("box");
				GUILayout.TextArea("Error: This is not a component, this is the base class.\n\nPlease add one of the components\n(eg:CaptureFromScene / CaptureFromCamera etc)");
				GUILayout.EndVertical();
				return;
			}

			if (_stylePlatformBox == null)
			{
				_stylePlatformBox = new GUIStyle(GUI.skin.box);
				_stylePlatformBox.padding.top = 0;
				_stylePlatformBox.padding.bottom = 0;
			}

			GUI_Header();
			GUI_BaseOptions();
		}

		protected virtual void GUI_User()
		{

		}

		protected void GUI_Header()
		{
			// Describe the watermark for trial version
			if (_isTrialVersion)
			{
				EditorUtils.DrawSectionColored("- AVPRO MOVIE CAPTURE -\nTRIAL VERSION", ref _isExpandedTrial, DrawTrialMessage, Color.magenta, Color.magenta, Color.magenta);
			}

			// Button to launch the capture window
			{
				GUI.backgroundColor = new Color(0.96f, 0.25f, 0.47f);
				if (GUILayout.Button("\n◄ Open Movie Capture Window ►\n"))
				{
					CaptureEditorWindow.Init();
				}
				GUI.backgroundColor = Color.white;
			}
		}

		protected void DrawTrialMessage()
		{
			string message = "The free trial version is watermarked.  Upgrade to the full package to remove the watermark.";

			GUI.backgroundColor = Color.yellow;
			EditorGUILayout.BeginVertical(GUI.skin.box);
			//GUI.color = Color.yellow;
			//GUILayout.Label("AVPRO MOVIE CAPTURE - FREE TRIAL VERSION", EditorStyles.boldLabel);
			GUI.color = Color.white;
			GUILayout.Label(message, EditorStyles.wordWrappedLabel);
			if (GUILayout.Button("Upgrade Now"))
			{
				Application.OpenURL(UnityAssetStore_FullVersionUrl);
			}
			EditorGUILayout.EndVertical();
			GUI.backgroundColor = Color.white;
			GUI.color = Color.white;
		}

		protected void GUI_BaseOptions()
		{
			serializedObject.Update();

			if (_baseCapture == null)
			{
				return;
			}

			//DrawDefaultInspector();

			if (!_baseCapture.IsCapturing())
			{
				GUILayout.Space(8f);
				EditorUtils.BoolAsDropdown("Capture Mode", _propIsRealtime, "Realtime Capture", "Offline Render");
				GUILayout.Space(8f);

				if (serializedObject.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(target);
				}

				GUI_User();

				// After the user mode we must update the serialised object again
				serializedObject.Update();

				EditorUtils.DrawSection("Start / Stop", ref _isExpandedStartStop, GUI_StartStop);
				EditorUtils.DrawSection("Output", ref _isExpandedOutput, GUI_OutputFilePath);
				EditorUtils.DrawSection("Visual", ref _isExpandedVisual, GUI_Visual);
				if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile)
				{
					EditorUtils.DrawSection("Audio", ref _isExpandedAudio, GUI_Audio);
					EditorUtils.DrawSection("Post", ref _isExpandedPost, GUI_Post);
				}
				EditorUtils.DrawSection("Misc", ref _isExpandedMisc, GUI_Misc);
				//EditorUtils.DrawSection("Platform Specific", ref _isExpandedMisc, GUI_PlatformSpecific);
				EditorUtils.DrawSection("Help", ref _isExpandedAbout, GUI_About);

				if (serializedObject.ApplyModifiedProperties())
				{
					EditorUtility.SetDirty(target);
				}

				GUI_Controls();
			}
			else
			{
				GUI_Stats();
				GUI_Progress();
				GUI_Controls();
			}
		}

		protected void GUI_Progress()
		{
			if (_baseCapture == null)
			{
				return;
			}

			if (_propStopMode.enumValueIndex != (int)StopMode.None)
			{
				Rect r = GUILayoutUtility.GetRect(128f, EditorStyles.label.CalcHeight(GUIContent.none, 32f), GUILayout.ExpandWidth(true));
				float progress = _baseCapture.GetProgress();
				EditorGUI.ProgressBar(r, progress, (progress * 100f).ToString("F1") + "%");
			}
		}

		protected void GUI_Stats()
		{
			if (_baseCapture == null)
			{
				return;
			}

			if (Application.isPlaying && _baseCapture.IsCapturing())
			{
				CaptureEditorWindow.DrawBaseCapturingGUI(_baseCapture);

				{
					EditorGUILayout.BeginVertical("box");
					EditorGUI.indentLevel++;
					{
						uint lastEncodedSeconds = (uint)Mathf.FloorToInt((float)_baseCapture.CaptureStats.NumEncodedFrames / _baseCapture.FrameRate);
						if (_baseCapture.IsRealTime)
						{
							lastEncodedSeconds = _baseCapture.CaptureStats.TotalEncodedSeconds;
						}
						uint lastEncodedMinutes = lastEncodedSeconds / 60;
						lastEncodedSeconds = lastEncodedSeconds % 60;
						uint lastEncodedFrame = _baseCapture.CaptureStats.NumEncodedFrames % (uint)_baseCapture.FrameRate;

						string lengthText = string.Format("{0:00}:{1:00}.{2:000}", lastEncodedMinutes, lastEncodedSeconds, lastEncodedFrame);
						EditorGUILayout.LabelField("Video Length", lengthText);

						if (!_baseCapture.IsRealTime)
						{
							long lastFileSize = _baseCapture.GetCaptureFileSize();
							EditorGUILayout.LabelField("File Size", ((float)lastFileSize / (1024f * 1024f)).ToString("F1") + "MB");
							EditorGUILayout.LabelField("Avg Bitrate", (8f * ((float)lastFileSize / (1024f * 1024f)) / (float)((lastEncodedMinutes * 60) + lastEncodedSeconds)).ToString("F2") + "Mb/s");
						}
					}
					EditorGUI.indentLevel--;
					EditorGUILayout.EndVertical();
				}
			}
		}

		protected void GUI_Controls()
		{
			if (_baseCapture == null)
			{
				return;
			}

			GUILayout.Space(8.0f);

			EditorGUI.BeginDisabledGroup(!Application.isPlaying);
			{
				if (!_baseCapture.IsCapturing())
				{
					GUI.backgroundColor = Color.green;
					string startString = "Start Capture";
					if (!_baseCapture.IsRealTime)
					{
						startString = "Start Render";
					}
					if (GUILayout.Button(startString, GUILayout.Height(32f)))
					{
						_baseCapture.SelectVideoCodec();
						_baseCapture.SelectAudioCodec();
						_baseCapture.SelectAudioInputDevice();
						// We have to queue the start capture otherwise Screen.width and height aren't correct
						_baseCapture.QueueStartCapture();
					}
					GUI.backgroundColor = Color.white;
				}
				else
				{
					GUILayout.BeginHorizontal();
					if (!_baseCapture.IsPaused())
					{
						GUI.backgroundColor = Color.yellow;
						if (GUILayout.Button("Pause", GUILayout.Height(32f)))
						{
							_baseCapture.PauseCapture();
						}
					}
					else
					{
						GUI.backgroundColor = Color.green;
						if (GUILayout.Button("Resume", GUILayout.Height(32f)))
						{
							_baseCapture.ResumeCapture();
						}
					}
					GUI.backgroundColor = Color.cyan;
					if (GUILayout.Button("Cancel", GUILayout.Height(32f)))
					{
						_baseCapture.CancelCapture();
					}
					GUI.backgroundColor = Color.red;
					if (GUILayout.Button("Stop", GUILayout.Height(32f)))
					{
						_baseCapture.StopCapture();
					}
					GUI.backgroundColor = Color.white;
					GUILayout.EndHorizontal();
				}
			}
			EditorGUI.EndDisabledGroup();

			EditorGUILayout.Space();
			EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(CaptureBase.LastFileSaved));
			GUILayout.BeginHorizontal();
			if (GUILayout.Button("Browse Last"))
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
			EditorGUI.EndDisabledGroup();
		}

		protected void GUI_OutputFilePath()
		{
			EditorUtils.EnumAsDropdown("Output Target", _propOutputTarget, EditorUtils.OutputTargetNames);
			if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile ||
				_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence)
			{
				bool isImageSequence = (_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence);

				if (isImageSequence)
				{
					BeginPlatformSelection();
					if (_selectedPlatform == NativePlugin.Platform.Windows)
					{
						EditorUtils.EnumAsDropdown("Format", _propImageSequenceFormatWindows, Utils.WindowsImageSequenceFormatNames);
					}
					else if (_selectedPlatform == NativePlugin.Platform.macOS)
					{
						EditorUtils.EnumAsDropdown("Format", _propImageSequenceFormatMacOS, Utils.MacOSImageSequenceFormatNames);
					}
					else if (_selectedPlatform == NativePlugin.Platform.iOS)
					{
						EditorUtils.EnumAsDropdown("Format", _propImageSequenceFormatIOS, Utils.IOSImageSequenceFormatNames);
					}
					else if (_selectedPlatform == NativePlugin.Platform.Android)
					{
						EditorUtils.EnumAsDropdown("Format", _propImageSequenceFormatAndroid, Utils.AndroidImageSequenceFormatNames);
					}
					EndPlatformSelection();
					GUILayout.Space(8f);
				}

				GUILayout.Label(_guiContentFolder, EditorStyles.boldLabel);

				#if UNITY_EDITOR_OSX
				// Photo Library is only for the video output type so grab the current folder type in case we need to reset it
				int outputFolderTypePrevValue = _propOutputFolderType.enumValueIndex;
				if (outputFolderTypePrevValue == (int)CaptureBase.OutputPath.PhotoLibrary)
					// Already the Photo Library type so reset to the default option
					outputFolderTypePrevValue = (int)CaptureBase.DefaultOutputFolderType;
				#endif

				EditorGUILayout.PropertyField(_propOutputFolderType, _guiContentFolder);

				#if UNITY_EDITOR_OSX
				if (isImageSequence && _propOutputFolderType.enumValueIndex == (int)CaptureBase.OutputPath.PhotoLibrary)
				{
					Debug.LogWarning("Photo Library is unavailable for the Image Sequence output type");
					_propOutputFolderType.enumValueIndex = (int)outputFolderTypePrevValue;
				}
				#endif

				if (_propOutputFolderType.enumValueIndex == (int)CaptureBase.OutputPath.Absolute)
				{
					EditorGUILayout.BeginHorizontal();
					EditorGUILayout.PropertyField(_propOutputFolderPath, _guiContentPath);
					if (GUILayout.Button(">", GUILayout.Width(22)))
					{
						_propOutputFolderPath.stringValue = EditorUtility.SaveFolderPanel("Select Folder To Store Video Captures", System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../")), "");
					}
					EditorGUILayout.EndHorizontal();
				}
				else
				{
					EditorGUILayout.PropertyField(_propOutputFolderPath, _guiContentSubfolders);
				}

				GUILayout.Label("File Name", EditorStyles.boldLabel);

				if (!isImageSequence)
				{
					EditorGUILayout.PropertyField(_propFileNamePrefix, _guiContentPrefix);
					EditorGUILayout.PropertyField(_propAppendFilenameTimestamp,_guiContentAppendTimestamp);
					EditorGUILayout.PropertyField(_propAllowManualFileExtension, _guiContentManualExtension);
					if (_propAllowManualFileExtension.boolValue)
					{
						EditorGUILayout.PropertyField(_propFileNameExtension, _guiContentExtension);
					}
				}

				if (isImageSequence)
				{
					EditorGUILayout.PropertyField(_propFileNamePrefix,_guiContentPrefix);
					EditorGUILayout.PropertyField(_propImageSequenceStartFrame, _guiContentStartFrame);
					EditorGUILayout.PropertyField(_propImageSequenceZeroDigits, _guiContentZeroDigits);
				}
			}
			else
			{
				EditorGUILayout.PropertyField(_propNamedPipePath, _guiContentPipePath);
			}


			/*// File path
			EditorGUILayout.LabelField("File Path", EditorStyles.boldLabel);
			EditorGUI.indentLevel++;
			_outputFolderIndex = EditorGUILayout.Popup("Relative to", _outputFolderIndex, _outputFolders);
			if (_outputFolderIndex == 0 || _outputFolderIndex == 1)
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
			EditorGUI.indentLevel--;*/
		}

		protected void GUI_StartStop()
		{
			EditorGUILayout.PropertyField(_propCaptureKey, _guiContentToggleKey);

			EditorGUILayout.Separator();

			EditorGUILayout.PropertyField(_propStartTrigger, _guiContentStartMode);
			EditorGUILayout.PropertyField(_propStartDelay, _guiContentStartDelay);

			if ((StartDelayMode)_propStartDelay.enumValueIndex == StartDelayMode.RealSeconds ||
				(StartDelayMode)_propStartDelay.enumValueIndex == StartDelayMode.GameSeconds)
			{
				EditorGUILayout.PropertyField(_propStartDelaySeconds, _guiContentSeconds);
			}

			EditorGUILayout.Separator();

			EditorGUILayout.PropertyField(_propStopMode, _guiContentStopMode);
			if ((StopMode)_propStopMode.enumValueIndex == StopMode.FramesEncoded)
			{
				EditorGUILayout.PropertyField(_propStopFrames, _guiContentFrames);
			}
			else if ((StopMode)_propStopMode.enumValueIndex == StopMode.SecondsElapsed || (StopMode)_propStopMode.enumValueIndex == StopMode.SecondsEncoded)
			{
				EditorGUILayout.PropertyField(_propStopSeconds, _guiContentSeconds);
			}
		}

		private void BeginPlatformSelection(string title = null)
		{
			GUILayout.BeginVertical(_stylePlatformBox);
			if (!string.IsNullOrEmpty(title))
			{
				GUILayout.Label(title, EditorStyles.boldLabel);
			}
			int rowCount = 0;
			int platformIndex = (int)_selectedPlatform;
			for (int i = 0; i < NativePlugin.PlatformNames.Length; i++)
			{
				if (i % 3 == 0)
				{
					GUILayout.BeginHorizontal();
					rowCount++;
				}

				Color hilight = Color.yellow;

				if (i == platformIndex)
				{
				}
				else
				{
					// Unselected, unmodified
					if (EditorGUIUtility.isProSkin)
					{
						GUI.backgroundColor = Color.grey;
						GUI.color = new Color(0.65f, 0.66f, 0.65f);// Color.grey;
					}
				}

				if (i == platformIndex)
				{
					if (!GUILayout.Toggle(true, NativePlugin.PlatformNames[i], GUI.skin.button))
					{
						platformIndex = -1;
					}
				}
				else
				{
					if (GUILayout.Button(NativePlugin.PlatformNames[i]))
					{
						platformIndex = i;
					}
				}
				if ((i+1) % 3 == 0)
				{
					rowCount--;
					GUILayout.EndHorizontal();
				}
				GUI.backgroundColor = Color.white;
				GUI.contentColor = Color.white;
				GUI.color = Color.white;
			}

			if (rowCount > 0)
			{
				GUILayout.EndHorizontal();
			}

			if (platformIndex != (int)_selectedPlatform)
			{
				_selectedPlatform = (NativePlugin.Platform)platformIndex;

				// We do this to clear the focus, otherwise a focused text field will not change when the Toolbar index changes
				EditorGUI.FocusTextInControl("ClearFocus");
			}
		}

		private void EndPlatformSelection()
		{
			GUILayout.EndVertical();
		}

		protected virtual void GUI_Misc()
		{
			EditorGUILayout.PropertyField(_propLogCaptureStartStop);
			EditorGUILayout.PropertyField(_propAllowVsyncDisable);
			EditorGUILayout.PropertyField(_propWaitForEndOfFrame);
			EditorGUILayout.PropertyField(_propSupportTextureRecreate, _guiContentSupportTextureRecreate);
			EditorGUILayout.PropertyField(_propPersistAcrossSceneLoads);
			#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
			EditorGUILayout.PropertyField(_propTimelineController);
			#endif
			#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
			EditorGUILayout.PropertyField(_propVideoPlayerController);
			#endif

			BeginPlatformSelection();
			if (_selectedPlatform == NativePlugin.Platform.Windows)
			{
				EditorGUILayout.PropertyField(_propForceGpuFlush);
				EditorGUILayout.PropertyField(_propMinimumDiskSpaceMB);
			}
			else if (_selectedPlatform == NativePlugin.Platform.Android)
			{
				EditorGUILayout.PropertyField(_propAndroidNoCaptureRotation, _guiAndroidNoCaptureRotation);
			}
			EndPlatformSelection();
		}

		protected virtual void GUI_About()
		{
			CaptureEditorWindow.DrawConfigGUI_About();
		}

		protected void GUI_Visual()
		{
			EditorGUILayout.PropertyField(_propDownScale);
			if (_propDownScale.enumValueIndex == 5)		// 5 is DownScale.Custom
			{
				EditorGUILayout.PropertyField(_propMaxVideoSize, new GUIContent("Size"));
				_propMaxVideoSize.vector2Value = new Vector2(Mathf.Clamp((int)_propMaxVideoSize.vector2Value.x, 1, NativePlugin.MaxRenderWidth), Mathf.Clamp((int)_propMaxVideoSize.vector2Value.y, 1, NativePlugin.MaxRenderHeight));
			}
			GUILayout.BeginHorizontal();
			EditorGUILayout.PropertyField(_propFrameRate, GUILayout.ExpandWidth(false));
			_propFrameRate.floatValue = Mathf.Clamp(_propFrameRate.floatValue, 0.01f, 240f);
			EditorUtils.FloatAsPopup("▶", "Common Frame Rates", this.serializedObject, _propFrameRate, EditorUtils.CommonFrameRateNames, EditorUtils.CommonFrameRateValues);
			GUILayout.EndHorizontal();

			EditorGUI.BeginDisabledGroup(!_propIsRealtime.boolValue);
			EditorGUILayout.PropertyField(_propTimelapseScale);
			_propTimelapseScale.intValue = Mathf.Max(1, _propTimelapseScale.intValue);
			EditorGUI.EndDisabledGroup();
			EditorGUILayout.PropertyField(_propFrameUpdateMode);

			EditorGUILayout.PropertyField(_propFlipVertically);

			EditorGUILayout.Space();

			if (_propOutputTarget.enumValueIndex == (int)OutputTarget.VideoFile)
			{
				GUI_VisualCodecs();
				GUI_VideoHints();
			}
			else if (_propOutputTarget.enumValueIndex == (int)OutputTarget.ImageSequence)
			{
				GUI_ImageHints();
			}

			EditorGUILayout.Space();

			EditorGUI.BeginDisabledGroup(_propIsRealtime.boolValue);
			GUILayout.Label("Motion Blur", EditorStyles.boldLabel);
			if (_propIsRealtime.boolValue)
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Motion Blur only available in Offline Render mode");
				GUI.color = Color.white;
			}
			else
			{
				GUI_MotionBlur();
			}
			EditorGUI.EndDisabledGroup();
		}

		protected void GUI_VisualCodecs_Windows()
		{
			bool searchByName = (_propForceVideoCodecIndexWindows.intValue < 0);
			bool newSearchByName = EditorGUILayout.Toggle("Search by name", searchByName);
			if (searchByName != newSearchByName)
			{
				if (newSearchByName)
				{
					_propForceVideoCodecIndexWindows.intValue = -1;
				}
				else
				{
					_propForceVideoCodecIndexWindows.intValue = 0;
				}
			}

			if (_propForceVideoCodecIndexWindows.intValue < 0)
			{
				EditorGUILayout.PropertyField(_propVideoCodecPriorityWindows, _guiContentCodecSearchOrder, true);
			}
			else
			{
				EditorGUILayout.PropertyField(_propForceVideoCodecIndexWindows);
			}
		}

		protected void GUI_VisualCodecs_Android()
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.Toggle("Search by name", false);
			EditorGUI.EndDisabledGroup();
			_propForceVideoCodecIndexAndroid.intValue = EditorGUILayout.Popup(_propForceVideoCodecIndexAndroid.intValue, NativePlugin.VideoCodecNamesAndroid);
		}

		protected void GUI_VisualCodecs_MacOS()
		{
			bool searchByName = (_propForceVideoCodecIndexMacOS.intValue < 0);
			bool newSearchByName = EditorGUILayout.Toggle("Search by name", searchByName);
			if (searchByName != newSearchByName)
			{
				if (newSearchByName)
				{
					_propForceVideoCodecIndexMacOS.intValue = -1;
				}
				else
				{
					_propForceVideoCodecIndexMacOS.intValue = 0;
				}
			}

			if (_propForceVideoCodecIndexMacOS.intValue < 0)
			{
				EditorGUILayout.PropertyField(_propVideoCodecPriorityMacOS, _guiContentCodecSearchOrder, true);
			}
			else
			{
				_propForceVideoCodecIndexMacOS.intValue = EditorGUILayout.Popup(_propForceVideoCodecIndexMacOS.intValue, NativePlugin.VideoCodecNamesMacOS);
			}
		}

		protected void GUI_VisualCodecs_IOS()
		{
			EditorGUI.BeginDisabledGroup(true);
			EditorGUILayout.Toggle("Search by name", false);
			EditorGUI.EndDisabledGroup();
			_propForceVideoCodecIndexIOS.intValue = EditorGUILayout.Popup(_propForceVideoCodecIndexIOS.intValue, NativePlugin.VideoCodecNamesIOS);
		}

		protected void GUI_VisualCodecs()
		{
			BeginPlatformSelection("Video Codec");
			if (_selectedPlatform == NativePlugin.Platform.Windows)
			{
				GUI_VisualCodecs_Windows();
			}
			else if (_selectedPlatform == NativePlugin.Platform.macOS)
			{
				GUI_VisualCodecs_MacOS();
			}
			else if (_selectedPlatform == NativePlugin.Platform.iOS)
			{
				GUI_VisualCodecs_IOS();
			}
			else if (_selectedPlatform == NativePlugin.Platform.Android)
			{
				GUI_VisualCodecs_Android();
			}
			EndPlatformSelection();
		}

		protected void GUI_AudioCodecs()
		{
			BeginPlatformSelection("Audio Codec");
			if (_selectedPlatform == NativePlugin.Platform.Windows)
			{
				bool searchByName = (_propForceAudioCodecIndexWindows.intValue < 0);
				bool newSearchByName = EditorGUILayout.Toggle("Search by name", searchByName);
				if (searchByName != newSearchByName)
				{
					if (newSearchByName)
					{
						_propForceAudioCodecIndexWindows.intValue = -1;
					}
					else
					{
						_propForceAudioCodecIndexWindows.intValue = 0;
					}
				}

				if (_propForceAudioCodecIndexWindows.intValue < 0)
				{
					EditorGUILayout.PropertyField(_propAudioCodecPriorityWindows, _guiContentCodecSearchOrder, true);
				}
				else
				{
					EditorGUILayout.PropertyField(_propForceAudioCodecIndexWindows);
				}
			}
			else if (_selectedPlatform == NativePlugin.Platform.Android)
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Toggle("Search by name", false);
				EditorGUI.EndDisabledGroup();
				_propForceAudioCodecIndexAndroid.intValue = EditorGUILayout.Popup(_propForceAudioCodecIndexAndroid.intValue, NativePlugin.AudioCodecNamesAndroid);
			}
			else if (_selectedPlatform == NativePlugin.Platform.macOS)
			{
				bool searchByName = (_propForceAudioCodecIndexMacOS.intValue < 0);
				bool newSearchByName = EditorGUILayout.Toggle("Search by name", searchByName);
				if (searchByName != newSearchByName)
				{
					if (newSearchByName)
					{
						_propForceAudioCodecIndexMacOS.intValue = -1;
					}
					else
					{
						_propForceAudioCodecIndexMacOS.intValue = 0;
					}
				}

				if (_propForceAudioCodecIndexMacOS.intValue < 0)
				{
					EditorGUILayout.PropertyField(_propAudioCodecPriorityMacOS, _guiContentCodecSearchOrder, true);
				}
				else
				{
					_propForceAudioCodecIndexMacOS.intValue = EditorGUILayout.Popup(_propForceAudioCodecIndexMacOS.intValue, NativePlugin.AudioCodecNamesMacOS);
				}
			}
			else if (_selectedPlatform == NativePlugin.Platform.iOS)
			{
				EditorGUI.BeginDisabledGroup(true);
				EditorGUILayout.Toggle("Search by name", false);
				EditorGUI.EndDisabledGroup();
				_propForceAudioCodecIndexIOS.intValue = EditorGUILayout.Popup(_propForceAudioCodecIndexIOS.intValue, NativePlugin.AudioCodecNamesIOS);
			}
			EndPlatformSelection();
		}

		protected void GUI_Audio()
		{
			bool showAudioSources = true;
			if (_propOutputTarget.enumValueIndex != (int)OutputTarget.VideoFile)
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Audio Capture only available for video file output");
				GUI.color = Color.white;
				showAudioSources = false;
			}
			if (showAudioSources)
			{
				EditorUtils.EnumAsDropdown("Audio Source", _propAudioCaptureSource, EditorUtils.AudioCaptureSourceNames );
				if (_propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.None)
				{
					bool showAudioOptions = true;

					#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
					if (!_propIsRealtime.boolValue && _propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.Manual && _propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.Unity && _propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Only Manual, Unity and Wwise Audio Sources are available in offline capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#else
					if (!_propIsRealtime.boolValue && _propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.Manual && _propAudioCaptureSource.enumValueIndex != (int)AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Only Manual and Wwise Audio Source is available in offline capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#endif
					if (_propIsRealtime.boolValue && _propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Wwise)
					{
						GUI.color = Color.yellow;
						GUILayout.TextArea("Wwise Audio Source is not available in realtime capture mode");
						GUI.color = Color.white;
						showAudioOptions = false;
					}					
					#if !AVPRO_MOVIECAPTURE_WWISE_SUPPORT
					if (!_propIsRealtime.boolValue && _propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Wwise)
					{
						GUI.color = Color.red;
						GUILayout.TextArea("To support Wwise audio capture: add AVPRO_MOVIECAPTURE_WWISE_SUPPORT to script defines in Player Settings");
						GUI.color = Color.white;
						showAudioOptions = false;
					}
					#endif					

					if (showAudioOptions)
					{
						if (_propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Microphone)
						{
							// TODO: change this into platform specific........
							// TODO: add search by name support................
							EditorGUILayout.PropertyField(_propForceAudioDeviceIndex);
						}
						else if (_propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Unity || _propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Wwise)
						{
							EditorGUILayout.PropertyField(_propUnityAudioCapture);
						}
						else if (_propAudioCaptureSource.enumValueIndex == (int)AudioCaptureSource.Manual)
						{
							EditorUtils.IntAsDropdown("Sample Rate", _propManualAudioSampleRate, EditorUtils.CommonAudioSampleRateNames, EditorUtils.CommonAudioSampleRateValues);
							EditorGUILayout.PropertyField(_propManualAudioChannelCount, new GUIContent("Channels"));
						}

						EditorGUILayout.Space();
						GUI_AudioCodecs();
						EditorGUILayout.Space();
					}
				}
			}

			EditorGUI.EndDisabledGroup();
		}

		protected void GUI_VideoHints()
		{
			BeginPlatformSelection("Encoder Hints");
			if (_selectedPlatform >= NativePlugin.Platform.First && _selectedPlatform < NativePlugin.Platform.Count)
			{
				PropVideoHints props = _propVideoHints[(int)_selectedPlatform];
				EditorUtils.BitrateField("Average Bitrate", props.propAverageBitrate);
				EditorGUI.BeginDisabledGroup(_selectedPlatform != NativePlugin.Platform.Windows);
				EditorUtils.BitrateField("Maxiumum Bitrate", props.propMaximumBitrate);
				EditorGUI.EndDisabledGroup();
				EditorGUILayout.PropertyField(props.propQuality);
				EditorGUILayout.PropertyField(props.propKeyframeInterval);
				EditorGUILayout.PropertyField(props.propTransparency);
				if (_selectedPlatform == NativePlugin.Platform.Windows)
				{
					EditorGUILayout.PropertyField(props.propHardwareEncoding);
				}
				else
				{
					EditorGUI.BeginDisabledGroup(true);
					EditorGUILayout.Toggle("Use Hardware Encoding", true);
					EditorGUI.EndDisabledGroup();
				}

				if (_selectedPlatform == NativePlugin.Platform.macOS || _selectedPlatform == NativePlugin.Platform.iOS)
				{
					EditorGUILayout.PropertyField(props.propEnableFragmentedWriting);
					if (props.propEnableFragmentedWriting.boolValue)
					{
						EditorGUILayout.PropertyField(props.propMovieFragmentInterval);
					}
				}
			}
			EndPlatformSelection();
		}

		protected void GUI_ImageHints()
		{
			BeginPlatformSelection("Encoder Hints");
			if (_selectedPlatform >= NativePlugin.Platform.First && _selectedPlatform < NativePlugin.Platform.Count)
			{
				PropImageHints props = _propImageHints[(int)_selectedPlatform];
				if (_selectedPlatform != NativePlugin.Platform.Windows)
				{
					EditorGUILayout.PropertyField(props.propQuality);
				}
				EditorGUILayout.PropertyField(props.propTransparency);
			}
			EndPlatformSelection();
		}

		protected void GUI_PlatformSpecific()
		{
			BeginPlatformSelection();
			if (_selectedPlatform >= NativePlugin.Platform.First && _selectedPlatform < NativePlugin.Platform.Count)
			{
				GUILayout.Label("Video Codecs", EditorStyles.boldLabel);

				if (_selectedPlatform == NativePlugin.Platform.Windows)
				{
					GUI_VisualCodecs_Windows();
				}
				else if (_selectedPlatform == NativePlugin.Platform.macOS)
				{
					GUI_VisualCodecs_MacOS();
				}
				else if (_selectedPlatform == NativePlugin.Platform.iOS)
				{
					GUI_VisualCodecs_IOS();
				}
				else if (_selectedPlatform == NativePlugin.Platform.Android)
				{
					GUI_VisualCodecs_Android();
				}

				GUILayout.Label("Encoder Hints", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propAverageBitrate);
				if (_selectedPlatform == NativePlugin.Platform.Windows)
				{
					EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propMaximumBitrate);
				}
				EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propQuality);
				EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propKeyframeInterval);
				EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propTransparency);
				if (_selectedPlatform == NativePlugin.Platform.Windows)
				{
					EditorGUILayout.PropertyField(_propVideoHints[(int)_selectedPlatform].propHardwareEncoding);
				}
				else
				{
					EditorGUI.BeginDisabledGroup(true);
					EditorGUILayout.Toggle("Use Hardware Encoding", true);
					EditorGUI.EndDisabledGroup();
				}
			}
			EndPlatformSelection();
		}

		protected void GUI_Post()
		{
			BeginPlatformSelection();
			if (_selectedPlatform >= NativePlugin.Platform.First && _selectedPlatform < NativePlugin.Platform.Count)
			{
				PropVideoHints propHints = _propVideoHints[(int)_selectedPlatform];
				EditorGUILayout.PropertyField(propHints.propFastStart, _guiStreamableMP4);
				EditorGUILayout.PropertyField(propHints.propInjectStereoPacking, _guiStereoPacking);
				if (propHints.propInjectStereoPacking.enumValueIndex == (int)NoneAutoCustom.Custom)
				{
					EditorGUILayout.PropertyField(propHints.propStereoPacking, _guiBlankSpace);
				}
				EditorGUILayout.PropertyField(propHints.propInjectSphericalVideoLayout, _guiSphericalLayout);
				if (propHints.propInjectSphericalVideoLayout.enumValueIndex == (int)NoneAutoCustom.Custom)
				{
					EditorGUILayout.PropertyField(propHints.propSphericalVideoLayout, _guiBlankSpace);
				}
			}
			EndPlatformSelection();
		}

		protected void GUI_MotionBlur()
		{
			EditorGUILayout.PropertyField(_propUseMotionBlur);
			if (_propUseMotionBlur.boolValue)
			{
				EditorGUILayout.PropertyField(_propMotionBlurSamples, _guiContentMotionBlurSamples);
				EditorGUILayout.PropertyField(_propMotionBlurCameras, _guiContentMotionBlurCameras, true);
			}
		}

		private void LoadSettings()
		{
			_isExpandedStartStop = EditorPrefs.GetBool(SettingsPrefix + "ExpandStartStop", _isExpandedStartStop);
			_isExpandedOutput = EditorPrefs.GetBool(SettingsPrefix + "ExpandOutput", _isExpandedOutput);
			_isExpandedVisual = EditorPrefs.GetBool(SettingsPrefix + "ExpandVisual", _isExpandedVisual);
			_isExpandedAudio = EditorPrefs.GetBool(SettingsPrefix + "ExpandAudio", _isExpandedAudio);
			_isExpandedPost = EditorPrefs.GetBool(SettingsPrefix + "ExpandPost", _isExpandedPost);
			_isExpandedMisc = EditorPrefs.GetBool(SettingsPrefix + "ExpandMisc", _isExpandedMisc);
			_selectedPlatform = (NativePlugin.Platform)EditorPrefs.GetInt(SettingsPrefix + "SelectedPlatform", (int)_selectedPlatform);
		}

		private void SaveSettings()
		{
			EditorPrefs.SetBool(SettingsPrefix + "ExpandStartStop", _isExpandedStartStop);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandOutput", _isExpandedOutput);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandVisual", _isExpandedVisual);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandAudio", _isExpandedAudio);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandPost", _isExpandedPost);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandMisc", _isExpandedMisc);
			EditorPrefs.SetInt(SettingsPrefix + "SelectedPlatform", (int)_selectedPlatform);
		}

		protected virtual void OnEnable()
		{
			#if UNITY_EDITOR_WIN
			_selectedPlatform = NativePlugin.Platform.Windows;
			#elif UNITY_EDITOR_OSX
			_selectedPlatform = NativePlugin.Platform.macOS;
			#endif

			LoadSettings();

			_baseCapture = (CaptureBase)this.target;

			_propCaptureKey = serializedObject.AssertFindProperty("_captureKey");
			_propPersistAcrossSceneLoads = serializedObject.AssertFindProperty("_persistAcrossSceneLoads");
			_propIsRealtime = serializedObject.AssertFindProperty("_isRealTime");
			_propMinimumDiskSpaceMB = serializedObject.AssertFindProperty("_minimumDiskSpaceMB");

			_propOutputTarget = serializedObject.AssertFindProperty("_outputTarget");
			_propImageSequenceFormatWindows = serializedObject.AssertFindProperty("_imageSequenceFormatWindows");
			_propImageSequenceFormatMacOS = serializedObject.AssertFindProperty("_imageSequenceFormatMacOS");
			_propImageSequenceFormatIOS = serializedObject.AssertFindProperty("_imageSequenceFormatIOS");
			_propImageSequenceFormatAndroid = serializedObject.AssertFindProperty("_imageSequenceFormatAndroid");
			_propImageSequenceStartFrame = serializedObject.AssertFindProperty("_imageSequenceStartFrame");
			_propImageSequenceZeroDigits = serializedObject.AssertFindProperty("_imageSequenceZeroDigits");
			_propOutputFolderType = serializedObject.AssertFindProperty("_outputFolderType");
			_propOutputFolderPath = serializedObject.AssertFindProperty("_outputFolderPath");
			_propAppendFilenameTimestamp = serializedObject.AssertFindProperty("_appendFilenameTimestamp");
			_propFileNamePrefix = serializedObject.AssertFindProperty("_filenamePrefix");
			_propAllowManualFileExtension = serializedObject.AssertFindProperty("_allowManualFileExtension");
			_propFileNameExtension = serializedObject.AssertFindProperty("_filenameExtension");
			_propNamedPipePath = serializedObject.AssertFindProperty("_namedPipePath");

			_propVideoCodecPriorityWindows = serializedObject.AssertFindProperty("_videoCodecPriorityWindows");
			_propVideoCodecPriorityMacOS = serializedObject.AssertFindProperty("_videoCodecPriorityMacOS");
			//_propVideoCodecPriorityAndroid = serializedObject.AssertFindProperty("_videoCodecPriorityAndroid");
			_propForceVideoCodecIndexWindows = serializedObject.AssertFindProperty("_forceVideoCodecIndexWindows");
			_propForceVideoCodecIndexMacOS = serializedObject.AssertFindProperty("_forceVideoCodecIndexMacOS");
			_propForceVideoCodecIndexIOS = serializedObject.AssertFindProperty("_forceVideoCodecIndexIOS");
			_propForceVideoCodecIndexAndroid = serializedObject.AssertFindProperty("_forceVideoCodecIndexAndroid");

			_propAudioCodecPriorityWindows = serializedObject.AssertFindProperty("_audioCodecPriorityWindows");
			_propAudioCodecPriorityMacOS = serializedObject.AssertFindProperty("_audioCodecPriorityMacOS");
			//_propAudioCodecPriorityIOS = serializedObject.AssertFindProperty("_audioCodecPriorityIOS");
			//_propAudioCodecPriorityAndroid = serializedObject.AssertFindProperty("_audioCodecPriorityAndroid");
			_propForceAudioCodecIndexWindows = serializedObject.AssertFindProperty("_forceAudioCodecIndexWindows");
			_propForceAudioCodecIndexMacOS = serializedObject.AssertFindProperty("_forceAudioCodecIndexMacOS");
			_propForceAudioCodecIndexIOS = serializedObject.AssertFindProperty("_forceAudioCodecIndexIOS");
			_propForceAudioCodecIndexAndroid = serializedObject.AssertFindProperty("_forceAudioCodecIndexAndroid");

			_propAudioCaptureSource = serializedObject.AssertFindProperty("_audioCaptureSource");
			_propUnityAudioCapture = serializedObject.AssertFindProperty("_unityAudioCapture");
			_propForceAudioDeviceIndex = serializedObject.AssertFindProperty("_forceAudioInputDeviceIndex");
			_propManualAudioSampleRate = serializedObject.AssertFindProperty("_manualAudioSampleRate");
			_propManualAudioChannelCount = serializedObject.AssertFindProperty("_manualAudioChannelCount");

			_propDownScale = serializedObject.AssertFindProperty("_downScale");
			_propMaxVideoSize = serializedObject.AssertFindProperty("_maxVideoSize");
			_propFrameRate = serializedObject.AssertFindProperty("_frameRate");
			_propTimelapseScale = serializedObject.AssertFindProperty("_timelapseScale");
			_propFrameUpdateMode = serializedObject.AssertFindProperty("_frameUpdateMode");
			_propFlipVertically = serializedObject.AssertFindProperty("_flipVertically");
			_propForceGpuFlush = serializedObject.AssertFindProperty("_forceGpuFlush");
			_propWaitForEndOfFrame = serializedObject.AssertFindProperty("_useWaitForEndOfFrame");
			_propAndroidNoCaptureRotation = serializedObject.AssertFindProperty("_androidNoCaptureRotation");

			_propUseMotionBlur = serializedObject.AssertFindProperty("_useMotionBlur");
			_propMotionBlurSamples = serializedObject.AssertFindProperty("_motionBlurSamples");
			_propMotionBlurCameras = serializedObject.AssertFindProperty("_motionBlurCameras");

			_propStartTrigger = serializedObject.AssertFindProperty("_startTrigger");
			_propStartDelay = serializedObject.AssertFindProperty("_startDelay");
			_propStartDelaySeconds = serializedObject.AssertFindProperty("_startDelaySeconds");

			_propStopMode = serializedObject.AssertFindProperty("_stopMode");
			_propStopFrames = serializedObject.AssertFindProperty("_stopFrames");
			_propStopSeconds = serializedObject.AssertFindProperty("_stopSeconds");

			_propVideoHints = new PropVideoHints[(int)NativePlugin.Platform.Count];
			_propVideoHints[(int)NativePlugin.Platform.Windows] = GetProperties_VideoHints(serializedObject, "_encoderHintsWindows.videoHints");
			_propVideoHints[(int)NativePlugin.Platform.macOS] = GetProperties_VideoHints(serializedObject, "_encoderHintsMacOS.videoHints");
			_propVideoHints[(int)NativePlugin.Platform.iOS] = GetProperties_VideoHints(serializedObject, "_encoderHintsIOS.videoHints");
			_propVideoHints[(int)NativePlugin.Platform.Android] = GetProperties_VideoHints(serializedObject, "_encoderHintsAndroid.videoHints");

			_propImageHints = new PropImageHints[(int)NativePlugin.Platform.Count];
			_propImageHints[(int)NativePlugin.Platform.Windows] = GetProperties_ImageHints(serializedObject, "_encoderHintsWindows.imageHints");
			_propImageHints[(int)NativePlugin.Platform.macOS] = GetProperties_ImageHints(serializedObject, "_encoderHintsMacOS.imageHints");
			_propImageHints[(int)NativePlugin.Platform.iOS] = GetProperties_ImageHints(serializedObject, "_encoderHintsIOS.imageHints");
			_propImageHints[(int)NativePlugin.Platform.Android] = GetProperties_ImageHints(serializedObject, "_encoderHintsAndroid.imageHints");

			_propLogCaptureStartStop = serializedObject.AssertFindProperty("_logCaptureStartStop");
			_propAllowVsyncDisable = serializedObject.AssertFindProperty("_allowVSyncDisable");
			_propSupportTextureRecreate = serializedObject.AssertFindProperty("_supportTextureRecreate");

			#if AVPRO_MOVIECAPTURE_PLAYABLES_SUPPORT
			_propTimelineController = serializedObject.AssertFindProperty("_timelineController");
			#endif
			#if AVPRO_MOVIECAPTURE_VIDEOPLAYER_SUPPORT
			_propVideoPlayerController = serializedObject.AssertFindProperty("_videoPlayerController");
			#endif

			_isTrialVersion = false;
			if (Application.isPlaying)
			{
				_isTrialVersion = IsTrialVersion();
			}
		}

		private static PropVideoHints GetProperties_VideoHints(SerializedObject serializedObject, string prefix)
		{
			PropVideoHints result = new PropVideoHints();
			result.propAverageBitrate = serializedObject.AssertFindProperty(prefix + ".averageBitrate");
			result.propMaximumBitrate = serializedObject.AssertFindProperty(prefix + ".maximumBitrate");
			result.propQuality = serializedObject.AssertFindProperty(prefix + ".quality");
			result.propKeyframeInterval = serializedObject.AssertFindProperty(prefix + ".keyframeInterval");
			result.propFastStart = serializedObject.AssertFindProperty(prefix + ".allowFastStartStreamingPostProcess");
			result.propTransparency = serializedObject.AssertFindProperty(prefix + ".supportTransparency");
			result.propHardwareEncoding = serializedObject.AssertFindProperty(prefix + ".useHardwareEncoding");
			result.propInjectStereoPacking = serializedObject.AssertFindProperty(prefix + ".injectStereoPacking");
			result.propStereoPacking = serializedObject.AssertFindProperty(prefix + ".stereoPacking");
			result.propInjectSphericalVideoLayout = serializedObject.AssertFindProperty(prefix + ".injectSphericalVideoLayout");
			result.propSphericalVideoLayout = serializedObject.AssertFindProperty(prefix + ".sphericalVideoLayout");
			result.propEnableFragmentedWriting = serializedObject.AssertFindProperty(prefix + ".enableFragmentedWriting");
			result.propMovieFragmentInterval = serializedObject.AssertFindProperty(prefix + ".movieFragmentInterval");
			return result;
		}

		private static PropImageHints GetProperties_ImageHints(SerializedObject serializedObject, string prefix)
		{
			PropImageHints result = new PropImageHints();
			result.propQuality = serializedObject.AssertFindProperty(prefix + ".quality");
			result.propTransparency = serializedObject.AssertFindProperty(prefix + ".supportTransparency");
			return result;
		}

		private void OnDisable()
		{
			SaveSettings();
		}

		protected static bool IsTrialVersion()
		{
			bool result = false;
			try
			{
				result = NativePlugin.IsTrialVersion();
			}
			catch (System.DllNotFoundException)
			{
				// Silent catch as we report this error elsewhere
			}
			return result;
		}

		protected static void ShowNoticeBox(MessageType messageType, string message)
		{
			//GUI.backgroundColor = Color.yellow;
			//EditorGUILayout.HelpBox(message, messageType);

			switch (messageType)
			{
				case MessageType.Error:
					GUI.color = Color.red;
					message = "Error: " + message;
					break;
				case MessageType.Warning:
					GUI.color = Color.yellow;
					message = "Warning: " + message;
					break;
			}

			//GUI.color = Color.yellow;
			GUILayout.TextArea(message);
			GUI.color = Color.white;
		}

		public override bool RequiresConstantRepaint()
		{
			CaptureBase capture = (this.target) as CaptureBase;
			return (Application.isPlaying && capture.isActiveAndEnabled && capture.IsCapturing() && !capture.IsPaused());
		}
	}
}
#endif