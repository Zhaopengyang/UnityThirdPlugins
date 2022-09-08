using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Editor
{
	public class EditorScreenshot : MonoBehaviour
	{
		internal enum ImageFormat
		{
			PNG,
			JPG,
			TGA,
			EXR,
		}

		internal enum ExrPrecision
		{
			Half,
			Float,
		}

		internal enum ExrCompression
		{
			None,
			ZIP,
			RLE,
			PIZ,
		}

		[Serializable]
		internal class Options
		{
			[SerializeField, Range(1, 100)]
			internal int jpgQuality = 75;

			[SerializeField]
			internal ExrPrecision exrPrecision;

			[SerializeField]
			internal ExrCompression exrCompression;

			internal Texture2D.EXRFlags GetExrFlags()
			{
				Texture2D.EXRFlags result = Texture2D.EXRFlags.None;
				if (exrPrecision == ExrPrecision.Float) result |= Texture2D.EXRFlags.OutputAsFloat;
				if (exrCompression == ExrCompression.ZIP) result |= Texture2D.EXRFlags.CompressZIP;
				else if (exrCompression == ExrCompression.RLE) result |= Texture2D.EXRFlags.CompressRLE;
				else if (exrCompression == ExrCompression.PIZ) result |= Texture2D.EXRFlags.CompressPIZ;
				return result;
			}
		}

		private const string SceneCameraName = "SceneCamera";

		internal static RenderTexture GetSceneViewTexture()
		{
			RenderTexture result = null;
			Camera[] cameras = FindAllCameras();
			if (cameras != null)
			{
				Camera camera = FindCameraByName(cameras.Length, cameras, SceneCameraName);
				if (camera != null)
				{
					if (camera.targetTexture != null)
					{
						// Note we have to force a render
						camera.Render();
						result = camera.targetTexture;
					}
				}
			}
			return result;
		}

		private static RenderTexture GetSceneViewTexture2()
		{
			RenderTexture result = null;
			RenderTexture[] rts = Resources.FindObjectsOfTypeAll<RenderTexture>();
			foreach (RenderTexture rt in rts)
			{
				if (rt.name == "SceneView RT")
				{
					result = rt;
					break;
				}
			}
			return result;
		}

		internal static void SceneViewToFile(string fileNamePrefix, string folderPath, ImageFormat format, Options options)
		{
			RenderTexture cameraTexture = GetSceneViewTexture();
			if (cameraTexture != null)
			{
				Texture2D texture = GetReadableTexture(cameraTexture, format == ImageFormat.EXR);
				if (texture != null)
				{
					string filePath = EditorScreenshot.GenerateFilename(fileNamePrefix, format, texture.width, texture.height);
					filePath = GenerateFilePath(folderPath, filePath);
					TextureToFile(texture, filePath, format, options);
					if (Application.isPlaying) 
					{
						Destroy(texture);
					}
					else
					{
						DestroyImmediate(texture);
					}
				}
			}
			else
			{
				Debug.LogError("SceneView texture isn't available, make sure the view is visible");
			}
		}

		internal static Texture2D GetReadableTexture(RenderTexture texture, bool supportHDR)
		{
			var oldRT = RenderTexture.active;
			TextureFormat format = TextureFormat.RGBA32;
			if (supportHDR)
			{
				format = TextureFormat.RGBAFloat;
			}
			Texture2D destTex = new Texture2D(texture.width, texture.height, format, false, supportHDR);
			RenderTexture.active = texture;
			destTex.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
			destTex.Apply();
			RenderTexture.active = oldRT;
			return destTex;
		}

		internal static bool SupportsTGA()
		{
			#if UNITY_2018_3_OR_NEWER
			return true;
			#else
			return false;
			#endif
		}

		internal static bool SupportsGameViewJPGTGAEXR()
		{
			#if UNITY_2017_3_OR_NEWER
			return Application.isPlaying;
			#else
			return false;
			#endif
		}		

		internal static bool SupportsGameViewEXR()
		{
			#if UNITY_2019_1_OR_NEWER
			return Application.isPlaying;
			#else
			return false;
			#endif
		}		

		internal static void TextureToFile(Texture2D texture, string filePath, ImageFormat format, Options options)
		{
			byte[] data = null;
			#if UNITY_2017_1_OR_NEWER
			switch (format)
			{
				case ImageFormat.PNG:
				data = ImageConversion.EncodeToPNG(texture);
				break;
				case ImageFormat.JPG:
				data = ImageConversion.EncodeToJPG(texture, options.jpgQuality);
				break;
				case ImageFormat.TGA:
				#if UNITY_2018_3_OR_NEWER
				data = ImageConversion.EncodeToTGA(texture);
				#endif
				break;
				case ImageFormat.EXR:
				data = ImageConversion.EncodeToEXR(texture, options.GetExrFlags());
				break;
			}
			#else
			switch (format)
			{
				case ImageFormat.PNG:
				data = texture.EncodeToPNG();
				break;
				case ImageFormat.JPG:
				data = texture.EncodeToJPG(options.jpgQuality);
				break;
				case ImageFormat.EXR:
				data = texture.EncodeToEXR(options.GetExrFlags());
				break;
			}
			#endif
			if (data != null)
			{
				System.IO.File.WriteAllBytes(filePath, data);
				OnFileWritten(filePath);
			}
		}

		internal static void GameViewToPNG(string filePath, int superSize = 1)
		{
			#if UNITY_2017_1_OR_NEWER
			ScreenCapture.CaptureScreenshot(filePath, superSize);
			#else
			Application.CaptureScreenshot(filePath, superSize);
			#endif

			// The screenshot will not be generated until the frame has finished (at least in Application.CaptureScreenshot())
			if (!Application.isPlaying)
			{
				UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
			}

			OnFileWritten(filePath);
		}

		internal static void OnFileWritten(string filePath)
		{
			Debug.Log("[AVProMovieCapture] File written: " + filePath);
			CaptureBase.LastFileSaved = filePath;
		}


		internal static void RenderTextureToFile(string filePath, ImageFormat format, Options options, RenderTexture rt)
		{
			Texture2D texture = GetReadableTexture(rt, format == ImageFormat.EXR);
			if (texture != null)
			{
				TextureToFile(texture, filePath, format, options);
				if (Application.isPlaying) 
				{
					Destroy(texture);
				}
				else
				{
					DestroyImmediate(texture);
				}
			}
		}

		internal static void GameViewToFile(string filePath, ImageFormat format, Options options, int superSize = 1)
		{
			// Coroutines aren't supported in editor mode, so we fake it using a GameObject with EditorCoroutine component
			GameObject go = new GameObject("temp-screenshot");
			go.hideFlags = HideFlags.HideAndDontSave;
			EditorCoroutine co = go.AddComponent<EditorCoroutine>();
			co.RunCoroutine(EditorScreenshot.GameViewToFileCoroutine(filePath, format, options, go, superSize));
		}

		internal static IEnumerator GameViewToFileCoroutine(string filePath, ImageFormat format, Options options, GameObject go, int superSize = 1)
		{
			yield return new WaitForEndOfFrame();
			Texture2D texture = null;
#if UNITY_2017_3_OR_NEWER
			if (format != ImageFormat.EXR)
			{
				texture = ScreenCapture.CaptureScreenshotAsTexture(superSize);
			}
			else
			{
				// For EXR we want floating point textures which CaptureScreenshotAsTexture() doesn't provide
				RenderTextureFormat rtFormat = (options.exrPrecision == ExrPrecision.Float) ? RenderTextureFormat.ARGBFloat : RenderTextureFormat.ARGBHalf;
				RenderTexture rt = new RenderTexture(Screen.width * superSize, Screen.height * superSize, 24, rtFormat);
				rt.Create();
#if UNITY_2019_1_OR_NEWER
				ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
#endif
				texture = GetReadableTexture(rt, true);
				Destroy(rt);
			}
#endif

			if (texture != null)
			{
				TextureToFile(texture, filePath, format, options);
				if (Application.isPlaying) 
				{
					Destroy(texture);
					Destroy(go);
				}
				else
				{
					DestroyImmediate(texture);
					DestroyImmediate(go);
				}
			}
		}

		internal static Camera[] FindAllCameras()
		{
			return Resources.FindObjectsOfTypeAll<Camera>();
		}

		static Camera FindCameraByName(int cameraCount, Camera[] cameras, string name)
		{
			Camera result = null;
			for (int i = 0; i < cameraCount; i++)
			{
				Camera c = cameras[i];
				if (c.name == name)
				{
					result = c;
					break;
				}
			}
			return result;
		}

		internal static string GetExtension(ImageFormat format)
		{
			switch (format)
			{
				case ImageFormat.PNG:
				return "png";
				case ImageFormat.JPG:
				return "jpg";
				case ImageFormat.TGA:
				return "tga";
				case ImageFormat.EXR:
				return "exr";
			}
			throw new Exception("Unknown image format");
		}

		internal static Vector2 GetGameViewSize()
		{
			Vector2 result = Vector2.zero;
			string[] res = UnityStats.screenRes.Split('x');
			if (res.Length == 2)
			{
				result.x = int.Parse(res[0]);
				result.y = int.Parse(res[1]);
			}
			return result;
		}

		internal static string GenerateFilename(string filenamePrefix, ImageFormat format, int width, int height)
		{
			string filenameExtension = GetExtension(format);
			string dateTime = DateTime.Now.ToString("yyyyMMdd-HHmmss");
			string filename = string.Format("{0}-{1}-{2}x{3}.{4}", filenamePrefix, dateTime, width, height, filenameExtension);
			return filename;
		}

		internal static string GenerateFilePath(string folderPath, string fileName)
		{
			if (!System.IO.Directory.Exists(folderPath))
			{
				System.IO.Directory.CreateDirectory(folderPath);
			}
			return System.IO.Path.Combine(folderPath, fileName);
		}		
	}
}