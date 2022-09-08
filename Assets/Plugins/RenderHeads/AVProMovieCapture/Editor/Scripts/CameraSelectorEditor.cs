#if UNITY_EDITOR
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
	[CanEditMultipleObjects]
	[CustomEditor(typeof(CameraSelector))]
	public class CameraSelectorEditor : UnityEditor.Editor
	{
		private SerializedProperty _propSelectBy;
		private SerializedProperty _propScanFrequency;
		private SerializedProperty _propScanHiddenCameras;
		private SerializedProperty _propTag;
		private SerializedProperty _propName;
		private SerializedProperty _propCamera;

		void OnEnable()
		{
			_propSelectBy = serializedObject.AssertFindProperty("_selectBy");
			_propScanFrequency = serializedObject.AssertFindProperty("_scanFrequency");
			_propScanHiddenCameras = serializedObject.AssertFindProperty("_scanHiddenCameras");
			_propTag = serializedObject.AssertFindProperty("_tag");
			_propName = serializedObject.AssertFindProperty("_name");
			_propCamera = serializedObject.AssertFindProperty("_camera");
		}

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.PropertyField(_propScanFrequency);
			EditorGUILayout.PropertyField(_propScanHiddenCameras);
			EditorGUILayout.PropertyField(_propSelectBy);
			if (_propSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Name)
			{
				EditorGUILayout.PropertyField(_propName);
			}
			else if (_propSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Tag)
			{
				EditorGUILayout.PropertyField(_propTag);
			}
			else if (_propSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.Manual)
			{
				EditorGUILayout.PropertyField(_propCamera);
			}
#if !SUPPORT_SCENE_VIEW_GIZMOS_CAPTURE
			else if (_propSelectBy.enumValueIndex == (int)CameraSelector.SelectByMode.EditorSceneView)
			{
				GUI.color = Color.yellow;
				GUILayout.TextArea("Warning: Scene View capture only currently supports gizmo capture up to Unity 2018.2.x");
				GUI.color = Color.white;
			}
#endif
			if (Application.isPlaying)
			{
				EditorGUILayout.Separator();

				GUILayout.Label("Cameras:");

				CameraSelector selector = (this.target) as CameraSelector;

				// Display buttons for all possible cameras
				for (int i = 0; i < selector.CameraCacheCount; i++)
				{
					if (selector.Camera != selector.CameraCache[i])
					{
						if (GUILayout.Button(selector.CameraCache[i].name))
						{
							selector.Camera = selector.CameraCache[i];
						}
					}
					else
					{
						GUI.color = Color.green;
						GUILayout.Button(selector.Camera.name);
						GUI.color = Color.white;
					}
				}

				EditorGUILayout.Separator();
				if (_propScanFrequency.enumValueIndex != (int)CameraSelector.ScanFrequencyMode.Frame)
				{
					if (GUILayout.Button("Update Camera List"))
					{
						selector.UpdateCameraCache();
					}
				}
			}

			serializedObject.ApplyModifiedProperties();
		}
	}
}
#endif