#if UNITY_EDITOR && UNITY_2017_1_OR_NEWER

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	public class PBXProjectHandlerException : System.Exception
	{
		public PBXProjectHandlerException(string message)
		:	base(message)
		{

		}
	}

	public class PBXProjectHandler
	{
		private static System.Type _PBXProjectType;
		private static System.Type PBXProjectType
		{
			get
			{
				if (_PBXProjectType == null)
				{
					_PBXProjectType = System.Type.GetType("UnityEditor.iOS.Xcode.PBXProject, UnityEditor.iOS.Extensions.Xcode, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
					if (_PBXProjectType == null)
					{
						throw new PBXProjectHandlerException("Failed to get type \"PBXProject\"");
					}
				}
				return _PBXProjectType;
			}
		}

		private static Dictionary<string, MethodInfo> _PBXProjectTypeMethods;
		private static Dictionary<string, MethodInfo> PBXProjectTypeMethods
		{
			get
			{
				if (_PBXProjectTypeMethods == null)
				{
					_PBXProjectTypeMethods = new Dictionary<string, MethodInfo>();
				}
				return _PBXProjectTypeMethods;
			}
		}

		private static MethodInfo GetMethod(string name, System.Type[] types)
		{
			string lookup = name + types.ToString();
			MethodInfo method;
			if (!PBXProjectTypeMethods.TryGetValue(lookup, out method))
			{
				method = _PBXProjectType.GetMethod(name, types);
				if (method != null)
				{
					_PBXProjectTypeMethods[lookup] = method;
				}
				else
				{
					throw new PBXProjectHandlerException(string.Format("Unknown method \"{0}\"", name));
				}
			}
			return method;
		}

		private object _project;

		public PBXProjectHandler()
		{
			_project = System.Activator.CreateInstance(PBXProjectType);
		}

		public void ReadFromFile(string path)
		{
			MethodInfo method = GetMethod("ReadFromFile", new System.Type[] { typeof(string) });
			Debug.LogFormat("[AVProMovieCapture] Reading Xcode project at: {0}", path);
			method.Invoke(_project, new object[] { path });
		}

		public void WriteToFile(string path)
		{
			MethodInfo method = GetMethod("WriteToFile", new System.Type[] { typeof(string) });
			Debug.LogFormat("[AVProMovieCapture] Writing Xcode project to: {0}", path);
			method.Invoke(_project, new object[] { path });
		}

		public string TargetGuidByName(string name)
		{
			MethodInfo method = GetMethod("TargetGuidByName", new System.Type[] { typeof(string) });
			string guid = (string)method.Invoke(_project, new object[] { name });
			Debug.LogFormat("[AVProMovieCapture] Target GUID for '{0}' is '{1}'", name, guid);
			return guid;
		}

		public void SetBuildProperty(string guid, string property, string value)
		{
			MethodInfo method = GetMethod("SetBuildProperty", new System.Type[] { typeof(string), typeof(string), typeof(string) });
			Debug.LogFormat("[AVProMovieCapture] Setting build property '{0}' to '{1}' for target with guid '{2}'", property, value, guid);
			method.Invoke(_project, new object[] { guid, property, value });
		}
	}

	public class UnknownTypeException : System.Exception
	{
		public UnknownTypeException(string message) : base(message) { }
	}

	public class PlistDocumentProxy
	{
		private static System.Type _PlistDocumentType;
		private static System.Type PlistDocumentType
		{
			get
			{
				if (_PlistDocumentType == null)
				{
					_PlistDocumentType = System.Type.GetType("UnityEditor.iOS.Xcode.PlistDocument, UnityEditor.iOS.Extensions.Xcode, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
					if (_PlistDocumentType == null)
					{
						throw new UnknownTypeException("Unknown type \"PlistDocument\"");
					}
				}
				return _PlistDocumentType;
			}
		}

		private object _plist;

		public PlistDocumentProxy()
		{
			_plist = System.Activator.CreateInstance(PlistDocumentType);
		}

		public void ReadFromFile(string path)
		{
			PlistDocumentType.GetMethod("ReadFromFile").Invoke(_plist, new object[] { path });
		}

		public string WriteToString()
		{
			return (string)PlistDocumentType.GetMethod("WriteToString").Invoke(_plist, null);
		}

		public PlistElementDictProxy root
		{
			get { return new PlistElementDictProxy(_PlistDocumentType.InvokeMember("root", BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetField, null, _plist, null)); }
		}
	}

	public class PlistElementDictProxy
	{
		private static System.Type _PlistElementDictType;
		public static System.Type PlistElementDictType
		{
			get
			{
				if (_PlistElementDictType == null)
				{
					_PlistElementDictType = System.Type.GetType("UnityEditor.iOS.Xcode.PlistElementDict, UnityEditor.iOS.Extensions.Xcode, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
					if (_PlistElementDictType == null)
					{
						throw new UnknownTypeException("Unknown type \"PlistElementDict\"");
					}
				}
				return _PlistElementDictType;
			}
		}

		private object _element;

		public PlistElementDictProxy(object element)
		{
			_element = element;
		}

		public void SetString(string key, string value)
		{
			PlistElementDictType.GetMethod("SetString").Invoke(_element, new object[] { key, value });
		}
	}

	public class PostProcessBuild_macOS
	{
		private static bool ActualModifyProjectAtPath(string path)
		{
			if (!Directory.Exists(path))
			{
				Debug.LogWarningFormat("[AVProMovieCapture] Failed to find Xcode project with path: {0}", path);
				return false;
			}

			Debug.LogFormat("[AVProMovieCapture] Modifying Xcode project at: {0}", path);
			string projectPath = Path.Combine(path, "project.pbxproj");
			try
			{
				PBXProjectHandler handler = new PBXProjectHandler();
				handler.ReadFromFile(projectPath);
				// string guid = handler.TargetGuidByName(Application.productName);

				// Modify project here

				handler.WriteToFile(projectPath);
				return true;
			}
			catch (PBXProjectHandlerException ex)
			{
				Debug.LogErrorFormat("[AVProMovieCapture] {0}", ex);
			}

			return false;
		}

		[PostProcessBuild]
		public static void ModifyProject(BuildTarget target, string path)
		{
			if (target != BuildTarget.StandaloneOSX)
				return;

			string projectPath = Path.Combine(path, Path.GetFileName(path) + ".xcodeproj");
			if (ActualModifyProjectAtPath(projectPath))
			{

			}
		}

		[PostProcessBuild(100)]
		public static void ModfifyPlist(BuildTarget buildTarget, string path)
		{
			if (buildTarget != BuildTarget.StandaloneOSX)
				return;

			// Check if we need to update Info.plist
			SerializedObject settings = Settings.GetSerializedSettings();

			SerializedProperty propPhotoLibraryUsageDescription = settings.FindProperty("_photoLibraryUsageDescription");
			string photoLibraryUsageDescription = propPhotoLibraryUsageDescription.stringValue;

			SerializedProperty propPhotoLibraryAddUsageDescription = settings.FindProperty("_photoLibraryAddUsageDescription");
			string photoLibraryAddUsageDescription = propPhotoLibraryAddUsageDescription.stringValue;

			if ((photoLibraryUsageDescription == null || photoLibraryUsageDescription.Length == 0)
			&&	(photoLibraryAddUsageDescription == null || photoLibraryAddUsageDescription.Length == 0))
			{
				// No, nothing to see here
				return;
			}

			bool buildingApp = false;

			// Locate the Info.plist file
			string plistPath = null;
			if (path.EndsWith(".app"))
			{
				plistPath = Path.Combine(path, "Contents");
				buildingApp = true;
			}
			else
			{
				plistPath = Path.Combine(path, PlayerSettings.productName);
			}

			plistPath = Path.Combine(plistPath, "Info.plist");

			if (!File.Exists(plistPath))
			{
				Debug.LogWarning("Unable to locate Info.plist, you may need to add the following keys yourself:\n\tNSPhotoLibraryUsageDescription,\n\tNSPhotoLibraryAddUsageDescription");
				return;
			}

			Debug.Log("Modifying the Info.plist file at: " + plistPath);

			PlistDocumentProxy plist = new PlistDocumentProxy();
			plist.ReadFromFile(plistPath);

			if (photoLibraryUsageDescription != null && photoLibraryUsageDescription.Length > 0)
			{
				Debug.Log("  Adding 'NSPhotoLibraryUsageDescription' to Info.plist");
				plist.root.SetString("NSPhotoLibraryUsageDescription", photoLibraryUsageDescription);
			}

			if (photoLibraryAddUsageDescription != null && photoLibraryAddUsageDescription.Length > 0)
			{
				Debug.Log("  Adding 'NSPhotoLibraryAddUsageDescription' to Info.plist");
				plist.root.SetString("NSPhotoLibraryAddUsageDescription", photoLibraryAddUsageDescription);
			}

			File.WriteAllText(plistPath, plist.WriteToString());

			Debug.Log("  Finished modifying the Info.plist");

			if (buildingApp)
			{
				Debug.Log("Codesigning...");
				CodeSignAppBundle(path);
			}
		}

		// Code signs the app bundle.
		private static void CodeSignAppBundle(string path)
		{
#if UNITY_EDITOR_OSX
	#if UNITY_2020_3_OR_NEWER
			UnityEditor.OSXStandalone.MacOSCodeSigning.CodeSignAppBundle(path);
	#else
			string cmd = string.Format("codesign -f -s - --deep {0}", path);
			string args = string.Format("-c \"{0}\"", cmd.Replace("\"", "\\\""));

			System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo("/bin/sh");
			startInfo.Arguments = args;
			startInfo.RedirectStandardOutput = true;
			startInfo.RedirectStandardError = true;
			startInfo.UseShellExecute = false;
			startInfo.CreateNoWindow = true;

			System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo);
			string result = process.StandardOutput.ReadToEnd();
			string error = process.StandardError.ReadToEnd();
			process.WaitForExit();

			if (error != null && error.Length > 0)
			{
				Debug.LogErrorFormat("[AVProMovieCapture] Failed to codesign app bundle, error: {0}", error);
			}
			else if (result != null && result.Length > 0)
			{
				Debug.LogFormat("[AVProMovieCapture] {0}", result);
			}
	#endif
#else
			Debug.LogWarning("- Unable to codesign application");
#endif
		}
	}

}   // namespace RenderHeads.Media.AVProMovieCapture.Editor

#endif  // UNITY_EDITOR && UNITY_2017_1_OR_NEWER
