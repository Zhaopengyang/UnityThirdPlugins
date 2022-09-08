#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(CaptureFromScreen))]
	public class CaptureFromScreenEditor : CaptureBaseEditor
	{
		private SerializedProperty _propCaptureMouseCursor;
		private SerializedProperty _propMouseCursor;

		protected override void GUI_Misc()
		{
			GUI_MouseCursor();
			base.GUI_Misc();
		}

		protected void GUI_MouseCursor()
		{
			EditorGUILayout.PropertyField(_propCaptureMouseCursor);
			if (_propCaptureMouseCursor.boolValue)
			{
				EditorGUILayout.PropertyField(_propMouseCursor);
			}
		}

		protected override void OnEnable()
		{
			base.OnEnable();
			_propCaptureMouseCursor = serializedObject.AssertFindProperty("_captureMouseCursor");
			_propMouseCursor = serializedObject.AssertFindProperty("_mouseCursor");
		}
	}
}
#endif