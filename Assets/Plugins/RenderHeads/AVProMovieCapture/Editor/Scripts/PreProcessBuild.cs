#if UNITY_2018_1_OR_NEWER
	#define UNITY_SUPPORTS_BUILD_REPORT
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEditor.Build;
#if UNITY_SUPPORTS_BUILD_REPORT
using UnityEditor.Build.Reporting;
#endif

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	public class PreProcessBuild :
		#if UNITY_SUPPORTS_BUILD_REPORT
		IPreprocessBuildWithReport
		#else
		IPreprocessBuild
		#endif
	{
		public int callbackOrder { get { return 0; } }

	#if UNITY_SUPPORTS_BUILD_REPORT
		public void OnPreprocessBuild(BuildReport report)
		{
			OnPreprocessBuild(report.summary.platform, report.summary.outputPath);
		}
	#endif

		public void OnPreprocessBuild(BuildTarget target, string path)
		{
			if (target == BuildTarget.iOS)
			{
				int indexMetal = GetGraphicsApiIndex(target, GraphicsDeviceType.Metal);
				int indexOpenGLES2 = GetGraphicsApiIndex(target, GraphicsDeviceType.OpenGLES2);
				int indexOpenGLES3 = GetGraphicsApiIndex(target, GraphicsDeviceType.OpenGLES3);

				if (indexMetal < 0)
				{
					string message = "Metal graphics API is required by AVPro Movie Capture.";
					message += "\n\nPlease go to Player Settings > Auto Graphics API and add Metal to the top of the list.";
					ShowAbortDialog(message);
				}

				if (indexOpenGLES2 >= 0 && indexMetal >= 0 && indexOpenGLES2 < indexMetal)
				{
					string message = "OpenGLES 2.0 graphics API is not supported by AVPro Movie Capture.";
					message += "\n\nPlease go to Player Settings > Auto Graphics API and add Metal to the top of the list.";
					ShowAbortDialog(message);
				}

				if (indexOpenGLES3 >= 0 && indexMetal >= 0 && indexOpenGLES3 < indexMetal)
				{
					string message = "OpenGLES 3.0 graphics API is not supported by AVPro Movie Capture.";
					message += "\n\nPlease go to Player Settings > Auto Graphics API and add Metal to the top of the list.";
					ShowAbortDialog(message);
				}
			}
			else if (target == BuildTarget.Android)
			{
				int indexVulkan = GetGraphicsApiIndex(target, GraphicsDeviceType.Vulkan);
				if (indexVulkan > 0)
				{
					string message = "Vulkan graphics API is not supported by AVPro Movie Capture.";
					message += "\n\nPlease go to Player Settings > Auto Graphics API and add OpenGLES to the top of the list.";
					ShowAbortDialog(message);
				}
			}
		}

		static void ShowAbortDialog(string message)
		{
			if (!EditorUtility.DisplayDialog("Continue Build?", message, "Continue", "Cancel"))
			{
				throw new BuildFailedException(message);
			}
		}

		static int GetGraphicsApiIndex(BuildTarget target, GraphicsDeviceType api)
		{
			int result = -1;
			GraphicsDeviceType[] devices = UnityEditor.PlayerSettings.GetGraphicsAPIs(target);
			for (int i = 0; i < devices.Length; i++)
			{
				if (devices[i] == api)
				{
					result = i;
					break;
				}
			}
			return result;
		}
	}
}
