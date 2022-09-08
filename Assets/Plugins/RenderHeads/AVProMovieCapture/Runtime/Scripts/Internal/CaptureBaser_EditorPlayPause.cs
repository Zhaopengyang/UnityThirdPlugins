using UnityEngine;

#if UNITY_EDITOR

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public partial class CaptureBase : MonoBehaviour
	{
#region Play/Pause Support for Unity Editor
		// This code handles the pause/play buttons in the editor
		private static void SetupEditorPlayPauseSupport()
		{
			#if UNITY_2017_2_OR_NEWER
			UnityEditor.EditorApplication.pauseStateChanged -= OnUnityPauseModeChanged;
			UnityEditor.EditorApplication.pauseStateChanged += OnUnityPauseModeChanged;
			#else
			UnityEditor.EditorApplication.playmodeStateChanged -= OnUnityPlayModeChanged;
			UnityEditor.EditorApplication.playmodeStateChanged += OnUnityPlayModeChanged;
			#endif
		}

		#if UNITY_2017_2_OR_NEWER
		private static void OnUnityPauseModeChanged(UnityEditor.PauseState state)
		{
			OnUnityPlayModeChanged();
		}
		#endif

		private static void OnUnityPlayModeChanged()
		{
			if (UnityEditor.EditorApplication.isPlaying)
			{
				bool isPaused = UnityEditor.EditorApplication.isPaused;
				CaptureBase[] captures = Resources.FindObjectsOfTypeAll<CaptureBase>();
				foreach (CaptureBase capture in captures)
				{
					if (isPaused)
					{
						capture.EditorPause();
					}
					else
					{
						capture.EditorUnpause();
					}
				}
			}
		}

		private void EditorPause()
		{
			if (this.isActiveAndEnabled)
			{
				_capturePrePauseTotalTime += (Time.realtimeSinceStartup - _captureStartTime);
				PauseCapture();
			}
		}

		private void EditorUnpause()
		{
			if (this.isActiveAndEnabled)
			{
				ResumeCapture();

				_captureStartTime = Time.realtimeSinceStartup;
			}
		}
#endregion // Play/Pause Support for Unity Editor
	}
}

#endif