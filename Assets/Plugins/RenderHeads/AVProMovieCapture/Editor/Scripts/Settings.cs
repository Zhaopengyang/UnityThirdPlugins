using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	internal class Settings : ScriptableObject
	{
		const string SettingsPath = "Assets/Plugins/RenderHeads/AVProMovieCapture/Editor/Settings.asset";
		#pragma warning disable 0414   // "field is assigned but its value is never used"
		[SerializeField] string _photoLibraryUsageDescription = string.Empty;
		[SerializeField] string _photoLibraryAddUsageDescription = string.Empty;
		#pragma warning restore 0414

		internal static Settings GetOrCreateSettings()
		{
			Settings settings = AssetDatabase.LoadAssetAtPath<Settings>(SettingsPath);
			if (settings == null)
			{
				settings = ScriptableObject.CreateInstance<Settings>();
				AssetDatabase.CreateAsset(settings, SettingsPath);
				AssetDatabase.SaveAssets();
			}
			return settings;
		}

		internal static SerializedObject GetSerializedSettings()
		{
			return new SerializedObject(GetOrCreateSettings());
		}
	}

	internal static class SettingsIMGUIRegister
	{
#if UNITY_2018_3_OR_NEWER
		private class MySettingsProvider : SettingsProvider
		{
			public MySettingsProvider(string path, SettingsScope scope)
			: base(path, scope)
			{
				this.keywords = new HashSet<string>(new[] { "Photo" });
			}

			public override void OnGUI(string searchContext)
			{
				SettingsGUI();
			}
		}

		[SettingsProvider]
		static SettingsProvider CreateSettingsProvider()
		{
			return new MySettingsProvider("Project/AVPro Movie Capture", SettingsScope.Project);
		}

#elif UNITY_5_6_OR_NEWER
		[PreferenceItem("AVPro Movie Capture")]
#endif
		private static void SettingsGUI()
		{
			SerializedObject settings = Settings.GetSerializedSettings();
			SerializedProperty propPhotoLibraryUsageDescription = settings.FindProperty("_photoLibraryUsageDescription");
			SerializedProperty propPhotoLibraryAddUsageDescription = settings.FindProperty("_photoLibraryAddUsageDescription");

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("macOS / iOS", EditorStyles.boldLabel);

			// Photo library usage description
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(
				new GUIContent(
					"Photo Library Usage Description",
					"Adds the NSPhotoLibraryUsageDescription key with the text provided to the generated apps Info.plist file."
				),
				GUILayout.MaxWidth(250.0f)
			);
			propPhotoLibraryUsageDescription.stringValue = EditorGUILayout.TextField(propPhotoLibraryUsageDescription.stringValue);
			EditorGUILayout.EndHorizontal();

			// Photo library add usage description
			EditorGUILayout.BeginHorizontal();
			EditorGUILayout.LabelField(
				new GUIContent(
					"Photo Library Add Usage Description",
					"Adds the NSPhotoLibraryAddUsageDescription key with the text provided to the generated apps Info.plist file."
				),
				GUILayout.MaxWidth(250.0f)
			);
			propPhotoLibraryAddUsageDescription.stringValue = EditorGUILayout.TextField(propPhotoLibraryAddUsageDescription.stringValue);
			EditorGUILayout.EndHorizontal();

			settings.ApplyModifiedPropertiesWithoutUndo();
		}
	}
}
