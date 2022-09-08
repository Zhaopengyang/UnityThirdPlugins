using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Demos
{
	public class ScreenCaptureDemo : MonoBehaviour
	{
		[SerializeField] AudioClip _audioBG = null;
		[SerializeField] AudioClip _audioHit = null;
		[SerializeField] float _speed = 1.0f;
		[SerializeField] CaptureBase _capture = null;
		[SerializeField] GUISkin _guiSkin = null;
		[SerializeField] bool _spinCamera = true;

		// State
		private float _timer;
		private List<FileWritingHandler> _fileWritingHandlers = new List<FileWritingHandler>(4);

		private IEnumerator Start()
		{
#if UNITY_IOS
			Application.targetFrameRate = 60;
#endif
			// Play music track
			if (_audioBG != null)
			{
				// AudioSource.PlayClipAtPoint(_audioBG, Vector3.zero);
				AudioSource source = gameObject.AddComponent<AudioSource>();
				source.clip = _audioBG;
				source.loop = true;
				source.Play();
			}
			if (_capture != null)
			{
				_capture.BeginFinalFileWritingAction += OnBeginFinalFileWriting;
				_capture.CompletedFileWritingAction += OnCompleteFinalFileWriting;

#if (UNITY_STANDALONE_OSX || UNITY_IOS) && !UNITY_EDITOR
				CaptureBase.PhotoLibraryAccessLevel photoLibraryAccessLevel = CaptureBase.PhotoLibraryAccessLevel.AddOnly;

				// If we're trying to write to the photo library, make sure we have permission
				if (_capture.OutputFolder == CaptureBase.OutputPath.PhotoLibrary)
				{
					// Album creation (album name is taken from the output folder path) requires read write access.
					if (_capture.OutputFolderPath != null && _capture.OutputFolderPath.Length > 0)
						photoLibraryAccessLevel = CaptureBase.PhotoLibraryAccessLevel.ReadWrite;

					switch (CaptureBase.HasUserAuthorisationToAccessPhotos(photoLibraryAccessLevel))
					{
						case CaptureBase.PhotoLibraryAuthorisationStatus.Authorised:
							// All good, nothing to do
							break;

						case CaptureBase.PhotoLibraryAuthorisationStatus.Unavailable:
							Debug.LogWarning("The photo library is unavailable, will use RelativeToPeristentData instead");
							_capture.OutputFolder = CaptureBase.OutputPath.RelativeToPeristentData;
							break;

						case CaptureBase.PhotoLibraryAuthorisationStatus.Denied:
							// User has denied access, change output path
							Debug.LogWarning("User has denied access to the photo library, will use RelativeToPeristentData instead");
							_capture.OutputFolder = CaptureBase.OutputPath.RelativeToPeristentData;
							break;

						case CaptureBase.PhotoLibraryAuthorisationStatus.NotDetermined:
							// Need to ask permission
							yield return CaptureBase.RequestUserAuthorisationToAccessPhotos(photoLibraryAccessLevel);
							// Nested switch, everbodies favourite
							switch (CaptureBase.HasUserAuthorisationToAccessPhotos(photoLibraryAccessLevel))
							{
								case CaptureBase.PhotoLibraryAuthorisationStatus.Authorised:
									// All good, nothing to do
									break;

								case CaptureBase.PhotoLibraryAuthorisationStatus.Denied:
									// User has denied access, change output path
									Debug.LogWarning("User has denied access to the photo library, will use RelativeToPeristentData instead");
									_capture.OutputFolder = CaptureBase.OutputPath.RelativeToPeristentData;
									break;

								case CaptureBase.PhotoLibraryAuthorisationStatus.NotDetermined:
									// We were unable to request access for some reason, check the logs for any error information
									Debug.LogWarning("Authorisation to access the photo library is still undetermined, will use RelativeToPeristentData instead");
									_capture.OutputFolder = CaptureBase.OutputPath.RelativeToPeristentData;
									break;
							}
							break;
					}
				}
#endif

#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS || UNITY_ANDROID))
				// Make sure we're authorised for using the microphone. On iOS the OS will forcibly
				// close the application if authorisation has not been granted. Make sure the
				// "Microphone Usage Description" field has been filled in the player settings.
				// Todo: handle late selection of microphone
				if (_capture.AudioCaptureSource == AudioCaptureSource.Microphone)
				{
					Debug.Log("Checking user has authorization to use the Microphone");
					switch (CaptureBase.HasUserAuthorisationToCaptureAudio())
					{
						case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Unavailable:
							Debug.LogWarning("Audio capture is unavailable, no audio will captured");
							break;
						case CaptureBase.AudioCaptureDeviceAuthorisationStatus.NotDetermined:
							Debug.Log("Audio capture status is not determined, requesting access");
							yield return CaptureBase.RequestAudioCaptureDeviceUserAuthorisation();
							switch (CaptureBase.HasUserAuthorisationToCaptureAudio())
							{
								case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Unavailable:
									Debug.LogWarning("Audio capture is unavailable, no audio will captured");
									break;

								case CaptureBase.AudioCaptureDeviceAuthorisationStatus.NotDetermined:
									Debug.LogWarning("Audio capture status is still not determined, no audio will captured");
									break;

								case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Denied:
									Debug.LogWarning("Audio capture status denied, no audio will be captured");
									break;

								case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Authorised:
									Debug.Log("Audio capture is authorised");
									break;
							}
							break;
						case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Denied:
							Debug.LogWarning("Audio capture status denied, no audio will be captured");
							break;
						case CaptureBase.AudioCaptureDeviceAuthorisationStatus.Authorised:
							Debug.Log("Audio capture is authorised");
							break;
					}
				}
#endif
			}
			yield return null;
		}

		private void OnBeginFinalFileWriting(FileWritingHandler handler)
		{
			_fileWritingHandlers.Add(handler);
		}

		private void OnCompleteFinalFileWriting(FileWritingHandler handler)
		{
			Debug.Log("Completed capture '" + handler.Path + "' with status: " + handler.Status.ToString());
		}

		private void Update()
		{
			#if (!ENABLE_INPUT_SYSTEM || ENABLE_LEGACY_INPUT_MANAGER)
			// Press the S key to trigger audio and background color change - useful for testing A/V sync
#if (UNITY_IOS || UNITY_ANDROID) && !UNITY_EDITOR
			bool bTouch = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended);
			if (bTouch)
#else
			if (Input.GetKeyDown(KeyCode.S))
#endif
			{
				if (_audioHit != null && _capture != null && _capture.IsCapturing())
				{
					AudioSource.PlayClipAtPoint(_audioHit, Vector3.zero);
					Camera.main.backgroundColor = new Color(Random.value, Random.value, Random.value, 0);
				}
			}

			// ESC to stop capture and quit
			if (Input.GetKeyDown(KeyCode.Escape))
			{
				if (_capture != null && _capture.IsCapturing())
				{
					_capture.StopCapture();
				}
				else
				{
					Application.Quit();
				}
			}
			#endif

			// Spin the camera around
			if (_spinCamera && Camera.main != null)
			{
				Camera.main.transform.RotateAround(Vector3.zero, Vector3.up, 20f * Time.deltaTime * _speed);
			}

			if (FileWritingHandler.Cleanup(_fileWritingHandlers))
			{
				if (_fileWritingHandlers.Count == 0)
				{
					Debug.Log("All pending file writes completed");
				}
			}
		}

		void OnDestroy()
		{
			foreach (FileWritingHandler handler in _fileWritingHandlers)
			{
				handler.Dispose();
			}
		}

		private void OnGUI()
		{
			GUI.skin = _guiSkin;
			Rect r = new Rect(Screen.width - 108, 64, 128, 28);
			GUI.Label(r, "Frame " + Time.frameCount);
		}
	}
}