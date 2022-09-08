#if UNITY_2017_3_OR_NEWER
	#define AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
#endif

using UnityEngine;
using System.Text;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Uses IMGUI to render a GUI to control video capture.  This is mainly used for the demos.
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Utils/Capture GUI", 300)]
	public class CaptureGUI : MonoBehaviour
	{
		private enum Section
		{
			None,
			VideoCodecs,
			AudioCodecs,
			AudioInputDevices,
			ImageCodecs,
		}
		[SerializeField] CaptureBase _movieCapture = null;
		[SerializeField] bool _showUI = true;
		[SerializeField] bool _whenRecordingAutoHideUI = true;
		[SerializeField] GUISkin _guiSkin = null;

		public CaptureBase MovieCapture
		{
			get { return _movieCapture; }
			set { _movieCapture = value; }
		}
		public bool HideUiWhenRecording
		{
			get { return _whenRecordingAutoHideUI; }
			set { _whenRecordingAutoHideUI = value; }
		}
		public bool ShowUI
		{
			get { return _showUI; }
			set { _showUI = value; }
		}

		private readonly static string[] CommonFrameRateNames = { "1", "10", "15", "23.98", "24", "25", "29.97", "30", "50", "59.94", "60", "75", "90", "120" };
		private readonly static float[] CommonFrameRateValues = { 1f, 10f, 15f, 23.976f, 24f, 25f, 29.97f, 30f, 50f, 59.94f, 60f, 75f, 90f, 120f };

		// GUI
		private Section _shownSection = Section.None;
		private string[] _videoCodecNames = new string[0];
		private string[] _audioCodecNames = new string[0];
		private bool[] _videoCodecConfigurable = new bool[0];
		private bool[] _audioCodecConfigurable = new bool[0];
		private string[] _audioDeviceNames = new string[0];
		private string[] _downScales = { "Original", "Half", "Quarter", "Eighth", "Sixteenth", "Custom" };
		private string[] _outputType = { "Video File", "Image Sequence", "Named Pipe" };
		private int _downScaleIndex;
		private GUIStyle _tintableBox;

		private Vector2 _videoPos = Vector2.zero;
		private Vector2 _audioPos = Vector2.zero;
		private Vector2 _audioCodecPos = Vector2.zero;
		private Vector2 _imageCodecPos = Vector2.zero;

		// Status
		private long _lastFileSize;
		private uint _lastEncodedMinutes;
		private uint _lastEncodedSeconds;
		private uint _lastEncodedFrame;

		private void Start()
		{
			if (_movieCapture != null)
			{
				CreateGUI();
			}
		}

		private void CreateGUI()
		{
			switch (_movieCapture.ResolutionDownScale)
			{
				default:
				case CaptureBase.DownScale.Original:
					_downScaleIndex = 0;
					break;
				case CaptureBase.DownScale.Half:
					_downScaleIndex = 1;
					break;
				case CaptureBase.DownScale.Quarter:
					_downScaleIndex = 2;
					break;
				case CaptureBase.DownScale.Eighth:
					_downScaleIndex = 3;
					break;
				case CaptureBase.DownScale.Sixteenth:
					_downScaleIndex = 4;
					break;
				case CaptureBase.DownScale.Custom:
					_downScaleIndex = 5;
					break;
			}

			if (CodecManager.VideoCodecs.Count > 0)
			{
				_videoCodecNames = new string[CodecManager.VideoCodecs.Count];
				_videoCodecConfigurable = new bool[CodecManager.VideoCodecs.Count];
				int i = 0;
				foreach (Codec codec in CodecManager.VideoCodecs)
				{
					_videoCodecNames[i] = codec.Name;
					_videoCodecConfigurable[i] = codec.HasConfigwindow;
					i++;
				}
			}
			if (CodecManager.AudioCodecs.Count > 0)
			{
				_audioCodecNames = new string[CodecManager.AudioCodecs.Count];
				_audioCodecConfigurable = new bool[CodecManager.AudioCodecs.Count];
				int i = 0;
				foreach (Codec codec in CodecManager.AudioCodecs)
				{
					_audioCodecNames[i] = codec.Name;
					_audioCodecConfigurable[i] = codec.HasConfigwindow;
					i++;
				}
			}
			int numAudioDevices = NativePlugin.GetAudioInputDeviceCount();
			if (numAudioDevices > 0)
			{
				_audioDeviceNames = new string[numAudioDevices];
				for (int i = 0; i < numAudioDevices; i++)
				{
					_audioDeviceNames[i] = NativePlugin.GetAudioInputDeviceName(i);
				}
			}

			_movieCapture.SelectVideoCodec();
			_movieCapture.SelectAudioCodec();
			_movieCapture.SelectAudioInputDevice();
		}

		private void OnGUI()
		{
			GUI.skin = _guiSkin;
			GUI.depth = -10;

			if (_tintableBox == null)
			{
				_tintableBox = new GUIStyle(GUI.skin.box);
				_tintableBox.normal.background = Texture2D.whiteTexture;
				_tintableBox.border.left = _tintableBox.border.right = _tintableBox.border.top = _tintableBox.border.bottom = 0;
			}

		#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
			float sf = Screen.dpi > 300.0f ? 2.0f : 1.5f;
		#else
			float sf = 1.0f;
		#endif
			if (Screen.width >= Screen.height)
				GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1920f * sf, Screen.height / 1080f * sf, 1f));
			else
				GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(Screen.width / 1080f * sf, Screen.height / 1920f * sf, 1f));

			if (_showUI)
			{
			#if UNITY_IOS && !UNITY_EDITOR_OSX
				float y = 40.0f;
			#else
				float y = 0.0f;
			#endif
				GUILayout.Window(4, new Rect(0f, y, 480f, 256f), MyWindow, "AVPro Movie Capture UI");
			}
		}

		private void MyWindow(int id)
		{
			if (_movieCapture == null)
			{
				GUILayout.Label("CaptureGUI - No CaptureFrom component set");
				return;
			}

			// NOTE: From Unity 2020.1 onwards it seems this correction isn't needed, but it's needed
			// for older versions of Unity when running in Linear colorspace
			bool sRGBWritePrev = GL.sRGBWrite;
			GL.sRGBWrite = false;

			if (_movieCapture.IsCapturing())
			{
				GUI_RecordingStatus();
				GL.sRGBWrite = sRGBWritePrev;
				return;
			}

			GUILayout.BeginVertical();

			if (_movieCapture != null)
			{
				GUILayout.Label("Resolution:");
				GUILayout.BeginHorizontal();
				_downScaleIndex = GUILayout.SelectionGrid(_downScaleIndex, _downScales, _downScales.Length);
				switch (_downScaleIndex)
				{
					case 0:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Original;
						break;
					case 1:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Half;
						break;
					case 2:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Quarter;
						break;
					case 3:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Eighth;
						break;
					case 4:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Sixteenth;
						break;
					case 5:
						_movieCapture.ResolutionDownScale = CaptureBase.DownScale.Custom;
						break;
				}
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal(GUILayout.Width(256));
				if (_movieCapture.ResolutionDownScale == CaptureBase.DownScale.Custom)
				{
					string maxWidthString = GUILayout.TextField(Mathf.FloorToInt(_movieCapture.ResolutionDownscaleCustom.x).ToString(), 4);
					int maxWidth = 0;
					if (int.TryParse(maxWidthString, out maxWidth))
					{
						_movieCapture.ResolutionDownscaleCustom = new Vector2(Mathf.Clamp(maxWidth, 0, NativePlugin.MaxRenderWidth), _movieCapture.ResolutionDownscaleCustom.y);
					}

					GUILayout.Label("x", GUILayout.Width(20));

					string maxHeightString = GUILayout.TextField(Mathf.FloorToInt(_movieCapture.ResolutionDownscaleCustom.y).ToString(), 4);
					int maxHeight = 0;
					if (int.TryParse(maxHeightString, out maxHeight))
					{
						_movieCapture.ResolutionDownscaleCustom = new Vector2(_movieCapture.ResolutionDownscaleCustom.x, Mathf.Clamp(maxHeight, 0, NativePlugin.MaxRenderHeight));
					}
				}
				GUILayout.EndHorizontal();

				GUILayout.Space(16f);

				GUILayout.Label("Frame Rate: " + _movieCapture.FrameRate.ToString("F2"));
				GUILayout.BeginHorizontal();
				for (int i = 0; i < CommonFrameRateNames.Length; i++)
				{
					if (GUILayout.Toggle(_movieCapture.FrameRate == CommonFrameRateValues[i], CommonFrameRateNames[i]))
					{
						_movieCapture.FrameRate = CommonFrameRateValues[i];
					}
				}
				GUILayout.EndHorizontal();

				GUILayout.Space(16f);

				GUILayout.BeginHorizontal();
				GUILayout.Label("Output:", GUILayout.ExpandWidth(false));
				OutputTarget outputType = (OutputTarget)GUILayout.SelectionGrid((int)_movieCapture.OutputTarget, _outputType, _outputType.Length);
				if (outputType != _movieCapture.OutputTarget)
				{
					_movieCapture.OutputTarget = outputType;
					// TODO: Set this to last used or sensible platform default
					switch (outputType) {
						case OutputTarget.VideoFile:
							break;
						case OutputTarget.ImageSequence:
							break;
						case OutputTarget.NamedPipe:
							break;
					}
				}
				GUILayout.EndHorizontal();

				GUILayout.Space(16f);

				_movieCapture.IsRealTime = GUILayout.Toggle(_movieCapture.IsRealTime, "RealTime");

				GUILayout.Space(16f);


				if (_movieCapture.OutputTarget == OutputTarget.VideoFile)
				{
					// Video Codec
					GUILayout.BeginHorizontal();
					if (_shownSection != Section.VideoCodecs)
					{
						if (GUILayout.Button("+", GUILayout.Width(24)))
						{
							_shownSection = Section.VideoCodecs;
						}
					}
					else
					{
						if (GUILayout.Button("-", GUILayout.Width(24)))
						{
							_shownSection = Section.None;
						}
					}
					GUILayout.Label("Using Video Codec: " + ((_movieCapture.SelectedVideoCodec != null)?_movieCapture.SelectedVideoCodec.Name:"None"));
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
					if (_movieCapture.SelectedVideoCodec != null && _movieCapture.SelectedVideoCodec.HasConfigwindow)
					{
						GUILayout.Space(16f);
						if (GUILayout.Button("Configure Codec"))
						{
							_movieCapture.SelectedVideoCodec.ShowConfigWindow();
						}
					}
#endif
					GUILayout.EndHorizontal();

					if (_videoCodecNames != null && _shownSection == Section.VideoCodecs)
					{
						GUILayout.Label("Select Video Codec:");
						_videoPos = GUILayout.BeginScrollView(_videoPos, GUILayout.Height(100));
						int videoIndex = _movieCapture.NativeForceVideoCodecIndex;
						int newCodecIndex = GUILayout.SelectionGrid(videoIndex, _videoCodecNames, 1);
						GUILayout.EndScrollView();

						if (newCodecIndex != videoIndex)
						{
							_movieCapture.NativeForceVideoCodecIndex = newCodecIndex;
							Codec newCodec = _movieCapture.SelectVideoCodec();
							if (newCodec != null)
							{
								_movieCapture.NativeForceVideoCodecIndex = newCodec.Index;
							}
							newCodec = _movieCapture.SelectAudioCodec();
							if (newCodec != null)
							{
								_movieCapture.NativeForceAudioCodecIndex = newCodec.Index;
							}
							Device newDevice = _movieCapture.SelectAudioInputDevice();
							if (newDevice != null)
							{
								_movieCapture.ForceAudioInputDeviceIndex = newDevice.Index;
							}
							_shownSection = Section.None;
						}
					}
					GUILayout.Space(16f);


					GUILayout.BeginHorizontal();
					GUILayout.Label("Audio Source:", GUILayout.ExpandWidth(false));
					_movieCapture.AudioCaptureSource = (AudioCaptureSource)GUILayout.SelectionGrid((int)_movieCapture.AudioCaptureSource, new string[] { "None", "Unity", "Microphone", "Manual", "Wwise" }, 5);
					GUILayout.EndHorizontal();
					GUILayout.Space(16f);

					GUI.enabled = (_movieCapture.IsRealTime || _movieCapture.AudioCaptureSource == AudioCaptureSource.Manual
							#if AVPRO_MOVIECAPTURE_OFFLINE_AUDIOCAPTURE
								|| _movieCapture.AudioCaptureSource == AudioCaptureSource.Unity
							#endif
								|| _movieCapture.AudioCaptureSource == AudioCaptureSource.Wwise
								);

					if (_movieCapture.AudioCaptureSource == AudioCaptureSource.Microphone && _audioDeviceNames != null)
					{
						// Audio Device
						GUILayout.BeginHorizontal();
						if (_shownSection != Section.AudioInputDevices)
						{
							if (GUILayout.Button("+", GUILayout.Width(24)))
							{
								_shownSection = Section.AudioInputDevices;
							}
						}
						else
						{
							if (GUILayout.Button("-", GUILayout.Width(24)))
							{
								_shownSection = Section.None;
							}
						}

						if (_movieCapture.ForceAudioInputDeviceIndex >= 0 && _movieCapture.ForceAudioInputDeviceIndex < _audioDeviceNames.Length)
						{
							GUILayout.Label("Using Microphone: " + _audioDeviceNames[_movieCapture.ForceAudioInputDeviceIndex]);
						}
						GUILayout.EndHorizontal();

						if (_shownSection == Section.AudioInputDevices)
						{
							GUILayout.Label("Select Microphone:");
							_audioPos = GUILayout.BeginScrollView(_audioPos, GUILayout.Height(100));
							int audioIndex = _movieCapture.ForceAudioInputDeviceIndex;
							int newAudioIndex = GUILayout.SelectionGrid(audioIndex, _audioDeviceNames, 1);
							GUILayout.EndScrollView();

							if (newAudioIndex != audioIndex)
							{
								_movieCapture.ForceAudioInputDeviceIndex = newAudioIndex;
								Device newDevice = _movieCapture.SelectAudioInputDevice();
								if (newDevice != null)
								{
									_movieCapture.ForceAudioInputDeviceIndex = newDevice.Index;
								}
								_shownSection = Section.None;
							}
						}
						GUILayout.Space(16f);
					}
					if (_movieCapture.AudioCaptureSource != AudioCaptureSource.None)
					{
						// Audio Codec
						GUILayout.BeginHorizontal();
						if (_shownSection != Section.AudioCodecs)
						{
							if (GUILayout.Button("+", GUILayout.Width(24)))
							{
								_shownSection = Section.AudioCodecs;
							}
						}
						else
						{
							if (GUILayout.Button("-", GUILayout.Width(24)))
							{
								_shownSection = Section.None;
							}
						}
						GUILayout.Label("Using Audio Codec: " + ((_movieCapture.SelectedAudioCodec != null)?_movieCapture.SelectedAudioCodec.Name:"None"));
						if (_movieCapture.SelectedAudioCodec != null && _movieCapture.SelectedAudioCodec.HasConfigwindow)
						{
							GUILayout.Space(16f);
							if (GUILayout.Button("Configure Codec"))
							{
								_movieCapture.SelectedAudioCodec.ShowConfigWindow();
							}
						}
						GUILayout.EndHorizontal();

						if (_audioCodecNames != null && _shownSection == Section.AudioCodecs)
						{
							GUILayout.Label("Select Audio Codec:");
							_audioCodecPos = GUILayout.BeginScrollView(_audioCodecPos, GUILayout.Height(100));
							int codecIndex = _movieCapture.NativeForceAudioCodecIndex;
							int newCodecIndex = GUILayout.SelectionGrid(codecIndex, _audioCodecNames, 1);
							GUILayout.EndScrollView();
							if (newCodecIndex != codecIndex)
							{
								_movieCapture.NativeForceAudioCodecIndex = newCodecIndex;
								Codec newCodec = _movieCapture.SelectAudioCodec();
								if (newCodec != null)
								{
									_movieCapture.NativeForceAudioCodecIndex = newCodec.Index;
								}
								newCodec = _movieCapture.SelectVideoCodec();
								if (newCodec != null)
								{
									_movieCapture.NativeForceVideoCodecIndex = newCodec.Index;
								}
								newCodec = _movieCapture.SelectAudioCodec();
								if (newCodec != null)
								{
									_movieCapture.NativeForceAudioCodecIndex = newCodec.Index;
								}
								Device newDevice = _movieCapture.SelectAudioInputDevice();
								if (newDevice != null)
								{
									_movieCapture.ForceAudioInputDeviceIndex = newDevice.Index;
								}
								_shownSection = Section.None;
							}

						}
						GUILayout.Space(16f);
					}

					GUI.enabled = true;

					GUILayout.Space(16f);
				}
				else if (_movieCapture.OutputTarget == OutputTarget.ImageSequence)
				{
					// Image Codec
					GUILayout.BeginHorizontal();
					if (_shownSection != Section.ImageCodecs)
					{
						if (GUILayout.Button("+", GUILayout.Width(24)))
						{
							_shownSection = Section.ImageCodecs;
						}
					}
					else
					{
						if (GUILayout.Button("-", GUILayout.Width(24)))
						{
							_shownSection = Section.None;
						}
					}
					GUILayout.Label("Using Image Codec: " + _movieCapture.NativeImageSequenceFormat);
					GUILayout.EndHorizontal();

					if (_shownSection == Section.ImageCodecs)
					{
						GUILayout.Label("Select Image Codec:");
						_imageCodecPos = GUILayout.BeginScrollView(_imageCodecPos, GUILayout.Height(100));
						int newCodecIndex = GUILayout.SelectionGrid(-1, Utils.GetNativeImageSequenceFormatNames(), 1);
						GUILayout.EndScrollView();
						if (newCodecIndex >= 0)
						{
							_movieCapture.NativeImageSequenceFormat = (ImageSequenceFormat)newCodecIndex;
							_shownSection = Section.None;
						}
					}
					GUILayout.Space(16f);
				}

				GUILayout.BeginHorizontal();
				if (_movieCapture.OutputTarget == OutputTarget.VideoFile)
				{
					_movieCapture.AllowManualFileExtension = false;
					GUILayout.Label("Filename Prefix: ");
					_movieCapture.FilenamePrefix = GUILayout.TextField(_movieCapture.FilenamePrefix, 64);
			}
				else if (_movieCapture.OutputTarget == OutputTarget.ImageSequence)
				{
					GUILayout.Label("Filename Prefix: ");
					_movieCapture.FilenamePrefix = GUILayout.TextField(_movieCapture.FilenamePrefix, 64);
				}
				else if (_movieCapture.OutputTarget == OutputTarget.NamedPipe)
				{
					GUILayout.Label("Path: ");
					_movieCapture.NamedPipePath = GUILayout.TextField(_movieCapture.NamedPipePath, 64);
				}


				GUILayout.EndHorizontal();
				GUILayout.Space(16f);
				GUILayout.Space(16f);

				if (_whenRecordingAutoHideUI)
				{
					GUILayout.Label("(Press CTRL-F5 to stop capture)");
				}

				GUILayout.BeginHorizontal();
				if (!_movieCapture.IsCapturing())
				{
					GUI.color = Color.green;
					if (GUILayout.Button(_movieCapture.IsRealTime?"Start Capture":"Start Render"))
					{
						StartCapture();
					}
					GUI.color = Color.white;
				}
				else
				{
					/*if (!_movieCapture.IsPaused())
					{
						if (GUILayout.Button("Pause Capture"))
						{
							PauseCapture();
						}
					}
					else
					{
						if (GUILayout.Button("Resume Capture"))
						{
							ResumeCapture();
						}
					}

					if (GUILayout.Button("Cancel Capture"))
					{
						CancelCapture();
					}
					if (GUILayout.Button("Stop Capture"))
					{
						StopCapture();
					}*/
				}
				GUILayout.EndHorizontal();

				if (_movieCapture.IsCapturing())
				{
					if (!string.IsNullOrEmpty(_movieCapture.LastFilePath))
					{
						GUILayout.Label("Writing file: '" + System.IO.Path.GetFileName(_movieCapture.LastFilePath) + "'");
					}
				}
				else
				{
					if (!string.IsNullOrEmpty(CaptureBase.LastFileSaved))
					{
						GUILayout.Space(16f);
						GUILayout.Label("Last file written: '" + System.IO.Path.GetFileName(CaptureBase.LastFileSaved) + "'");

						GUILayout.BeginHorizontal();
						#if (!UNITY_EDITOR && UNITY_ANDROID)
						#else
						if (GUILayout.Button("Browse"))
						{
							Utils.ShowInExplorer(CaptureBase.LastFileSaved);
						}
						#endif
						Color prevColor = GUI.color;
						GUI.color = Color.cyan;
						if (GUILayout.Button("View Last Capture"))
						{
							#if (!UNITY_EDITOR && UNITY_ANDROID)
							if (CaptureBase.LastFileSaved.ToLower().EndsWith(".mp4"))
							{
								Handheld.PlayFullScreenMovie(CaptureBase.LastFileSaved, Color.black, FullScreenMovieControlMode.CancelOnInput);
							}
							#else
							Utils.OpenInDefaultApp(CaptureBase.LastFileSaved);
							#endif
						}
						GUI.color = prevColor;

						GUILayout.EndHorizontal();
					}
				}
			}

			GUILayout.EndVertical();

			GL.sRGBWrite = sRGBWritePrev;
		}

		private void GUI_RecordingStatus()
		{
			GUILayout.Space(8.0f);
			DrawPauseResumeButtons();
			GUILayout.Label("Output", GUI.skin.box);
			GUILayout.BeginVertical(GUI.skin.box);

			Texture texture = _movieCapture.GetPreviewTexture();
			if (texture != null)
			{
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				float width = (Screen.width / 8f);
				float aspect = (float)texture.width / (float)texture.height;
				Rect textureRect = GUILayoutUtility.GetRect(width, width / aspect, GUILayout.ExpandWidth(false), GUILayout.ExpandHeight(false));
				GUI.DrawTexture(textureRect, texture, ScaleMode.ScaleToFit, false);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
			}

			GUILayout.Label(System.IO.Path.GetFileName(_movieCapture.LastFilePath), GUI.skin.box);
			GUILayout.Space(8.0f);

			GUILayout.Label("Video", GUI.skin.box);
			DrawGuiField("Dimensions", _movieCapture.GetRecordingWidth() + "x" + _movieCapture.GetRecordingHeight() + " @ " + _movieCapture.FrameRate.ToString("F2") + "hz");
			if (_movieCapture.OutputTarget == OutputTarget.VideoFile)
			{
				DrawGuiField("Codec", (_movieCapture.SelectedVideoCodec != null)?_movieCapture.SelectedVideoCodec.Name:"None");
			}
			else if (_movieCapture.OutputTarget == OutputTarget.ImageSequence)
			{
				DrawGuiField("Codec",_movieCapture.NativeImageSequenceFormat.ToString());
			}

			if (_movieCapture.OutputTarget == OutputTarget.VideoFile)
			{
				if (_movieCapture.CaptureStats.AudioCaptureSource != AudioCaptureSource.None)
				{
					GUILayout.Label("Audio", GUI.skin.box);
					if (_movieCapture.AudioCaptureSource == AudioCaptureSource.Unity)
					{
						DrawGuiField("Source", "Unity");
					}
					else if (_movieCapture.AudioCaptureSource == AudioCaptureSource.Microphone)
					{
						DrawGuiField("Source", (_movieCapture.SelectedAudioInputDevice != null)?_movieCapture.SelectedAudioInputDevice.Name:"None");
					}
					DrawGuiField("Codec", (_movieCapture.SelectedAudioCodec != null)?_movieCapture.SelectedAudioCodec.Name:"None");
					if (_movieCapture.AudioCaptureSource == AudioCaptureSource.Unity)
					{
						DrawGuiField("Sample Rate", _movieCapture.CaptureStats.UnityAudioSampleRate.ToString() + "hz");
						DrawGuiField("Channels", _movieCapture.CaptureStats.UnityAudioChannelCount.ToString());
					}
				}
			}

			GUILayout.EndVertical();

			GUILayout.Space(8.0f);

			// Draw progress bar
			if (_movieCapture.StopMode != (int)StopMode.None)
			{
				Rect r = GUILayoutUtility.GetRect(128f, GUI.skin.label.CalcHeight(GUIContent.none, 32f), GUILayout.ExpandWidth(true));
				float progress = _movieCapture.GetProgress();
				Rect progressRect = new Rect(r);
				progressRect.width *= progress;
				GUI.color = new Color(0f, 0.5f, 0f, 1f);
				GUI.Box(progressRect, GUIContent.none, _tintableBox);
				GUI.color = Color.white;
				GUI.backgroundColor = Color.clear;
				GUI.Box(r, (progress * 100f).ToString("F1") + "%", _tintableBox);
				GUI.color = Color.white;
			}

			GUILayout.Label("Stats", GUI.skin.box);
			GUILayout.BeginVertical(GUI.skin.box);

			if (_movieCapture.CaptureStats.FPS > 0f)
			{
				Color originalColor = GUI.color;
				if (_movieCapture.IsRealTime)
				{
					float fpsDelta = (_movieCapture.CaptureStats.FPS - _movieCapture.FrameRate);
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

				DrawGuiField("Capture Rate", string.Format("{0:0.##} / {1:F2} FPS", _movieCapture.CaptureStats.FPS, _movieCapture.FrameRate));

				GUI.color = originalColor;
			}
			else
			{
				DrawGuiField("Capture Rate", string.Format(".. / {0:F2} FPS", _movieCapture.FrameRate));
			}

			DrawGuiField("File Size", ((float)_lastFileSize / (1024f * 1024f)).ToString("F1") + "MB");
			DrawGuiField("Video Length", _lastEncodedMinutes.ToString("00") + ":" + _lastEncodedSeconds.ToString("00") + "." + _lastEncodedFrame.ToString("000"));

			GUILayout.Label("Dropped Frames", GUI.skin.box);
			DrawGuiField("In Unity", _movieCapture.CaptureStats.NumDroppedFrames.ToString());
			DrawGuiField("In Encoder ", _movieCapture.CaptureStats.NumDroppedEncoderFrames.ToString());
			if (_movieCapture.CaptureStats.AudioCaptureSource != AudioCaptureSource.None)
			{
				if (_movieCapture.AudioCaptureSource == AudioCaptureSource.Unity && _movieCapture.UnityAudioCapture != null)
				{
					DrawGuiField("Audio Overflows", _movieCapture.UnityAudioCapture.OverflowCount.ToString());
				}
			}

			GUILayout.EndVertical();
		}

		private void DrawPauseResumeButtons()
		{
			GUILayout.BeginHorizontal();

			if (!_movieCapture.IsPaused())
			{
				GUI.backgroundColor = Color.yellow;
				if (GUILayout.Button("Pause Capture"))
				{
					PauseCapture();
				}
			}
			else
			{
				GUI.backgroundColor = Color.green;
				if (GUILayout.Button("Resume Capture"))
				{
					ResumeCapture();
				}
			}

			GUI.backgroundColor = Color.cyan;
			if (GUILayout.Button("Cancel Capture"))
			{
				CancelCapture();
			}
			GUI.backgroundColor = Color.red;
			if (GUILayout.Button("Stop Capture"))
			{
				StopCapture();
			}
			GUI.backgroundColor = Color.white;

			GUILayout.EndHorizontal();
		}

		private void DrawGuiField(string a, string b)
		{
			GUILayout.BeginHorizontal();
			GUILayout.Label(a);
			GUILayout.FlexibleSpace();
			GUILayout.Label(b);
			GUILayout.EndHorizontal();
		}

		private void StartCapture()
		{
			_lastFileSize = 0;
			_lastEncodedMinutes = _lastEncodedSeconds = _lastEncodedFrame = 0;
			if (_whenRecordingAutoHideUI)
			{
				_showUI = false;
			}
			if (_movieCapture != null)
			{
				_movieCapture.StartCapture();
			}
		}

		private void StopCapture()
		{
			if (_movieCapture != null)
			{
				_movieCapture.StopCapture();
			}
		}

		private void CancelCapture()
		{
			if (_movieCapture != null)
			{
				_movieCapture.CancelCapture();
			}
		}

		private void ResumeCapture()
		{
			if (_movieCapture != null)
			{
				_movieCapture.ResumeCapture();
			}
		}

		private void PauseCapture()
		{
			if (_movieCapture != null)
			{
				_movieCapture.PauseCapture();
			}
		}

		private void Update()
		{
			if (_movieCapture != null)
			{
				if (_whenRecordingAutoHideUI && !_showUI)
				{
					if (!_movieCapture.IsCapturing())
						_showUI = true;
				}

				if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.F5))
				{
					if (_movieCapture.IsCapturing())
					{
						_movieCapture.StopCapture();
					}
				}

				if (_movieCapture.IsCapturing())
				{
					_lastFileSize = _movieCapture.GetCaptureFileSize();
					if (!_movieCapture.IsRealTime)
					{
						_lastEncodedSeconds = (uint)Mathf.FloorToInt((float)_movieCapture.CaptureStats.NumEncodedFrames / _movieCapture.FrameRate);
					}
					else
					{
						_lastEncodedSeconds = _movieCapture.CaptureStats.TotalEncodedSeconds;
					}
					_lastEncodedMinutes = _lastEncodedSeconds / 60;
					_lastEncodedSeconds = _lastEncodedSeconds % 60;
					_lastEncodedFrame = _movieCapture.CaptureStats.NumEncodedFrames % (uint)_movieCapture.FrameRate;
				}
			}
		}
	}
}
