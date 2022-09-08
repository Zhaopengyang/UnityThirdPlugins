using UnityEngine;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public class Utils
	{
		public static string[] WindowsImageSequenceFormatNames = new string[] { "PNG (uncompressed)" };
		public static string[] MacOSImageSequenceFormatNames = new string[] { "PNG", "JPEG", "TIFF", "HEIF" };
		public static string[] IOSImageSequenceFormatNames = new string[] { "PNG", "JPEG", "TIFF", "HEIF" };
		public static string[] AndroidImageSequenceFormatNames = new string[] { "PNG", "JPEG" };

		public static string[] GetNativeImageSequenceFormatNames()
		{
			string[] result = null;
			#if UNITY_EDITOR
				#if UNITY_EDITOR_WIN
					result = WindowsImageSequenceFormatNames;
				#elif UNITY_EDITOR_OSX
					result = MacOSImageSequenceFormatNames;
				#endif
			#else
				#if UNITY_STANDALONE_WIN
					result = WindowsImageSequenceFormatNames;
				#elif UNITY_STANDALONE_OSX
					result = MacOSImageSequenceFormatNames;
				#elif UNITY_IOS
					result = IOSImageSequenceFormatNames;
				#elif UNITY_ANDROID
					result = AndroidImageSequenceFormatNames;
				#endif
			#endif
			if (result == null)
			{
				result = new string[0];
			}
			return result;
		}

		public static bool HasAlphaChannel(RenderTextureFormat format)
		{
			bool result = false;
			switch (format)
			{
				case RenderTextureFormat.ARGB32:
				case RenderTextureFormat.BGRA32:
				case RenderTextureFormat.ARGB4444:
				case RenderTextureFormat.ARGB1555:
				case RenderTextureFormat.ARGB2101010:
				case RenderTextureFormat.ARGB64:
				case RenderTextureFormat.RGBAUShort:
				case RenderTextureFormat.ARGBInt:
				case RenderTextureFormat.ARGBFloat:
				case RenderTextureFormat.ARGBHalf:
				#if UNITY_2017_2_OR_NEWER
				case RenderTextureFormat.BGRA10101010_XR:
				#endif
					result = true;
					break;
			}
			return result;
		}

		public static RenderTextureFormat GetBestRenderTextureFormat(bool supportHDR, bool supportTransparency, bool favourSpeedOverQuality)
		{
			RenderTextureFormat result = RenderTextureFormat.Default;
			if (supportTransparency)
			{
				if (supportHDR)
				{
					// HDR Transparent
					result = RenderTextureFormat.DefaultHDR;
					if (favourSpeedOverQuality)
					{
						#if UNITY_2017_2_OR_NEWER
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA10101010_XR)) return RenderTextureFormat.BGRA10101010_XR;
						#endif
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return RenderTextureFormat.ARGBFloat;
					}
					else
					{
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return RenderTextureFormat.ARGBFloat;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
						#if UNITY_2017_2_OR_NEWER
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA10101010_XR)) return RenderTextureFormat.BGRA10101010_XR;
						#endif
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB64)) return RenderTextureFormat.ARGB64;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB2101010)) return RenderTextureFormat.ARGB2101010;
					}
				}
				else
				{
					// SDR Transparent
					if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)) return RenderTextureFormat.ARGB32;
					if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.BGRA32)) return RenderTextureFormat.BGRA32;
				}
			}
			else
			{
				if (supportHDR)
				{
					// HDR non-transparent
					result = RenderTextureFormat.DefaultHDR;
					/*if (favourSpeedOverQuality)
					{
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)) return RenderTextureFormat.RGB111110Float;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB2101010)) return RenderTextureFormat.ARGB2101010;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return RenderTextureFormat.ARGBFloat;
					}
					else
					{
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBFloat)) return RenderTextureFormat.ARGBFloat;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGBHalf)) return RenderTextureFormat.ARGBHalf;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB2101010)) return RenderTextureFormat.ARGB2101010;
						if (SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RGB111110Float)) return RenderTextureFormat.RGB111110Float;
					}*/
				}
			}

			if (!SystemInfo.SupportsRenderTextureFormat(result))
			{
				UnityEngine.Debug.LogError("[AVProMovieCapture] Couldn't find suitable texture format " + result + " for " + supportHDR +  " " +  supportTransparency + " " + favourSpeedOverQuality);
			}

			return result;
		}

		/// <summary>
		/// The "main" camera isn't necessarily the one the gets rendered last to screen,
		/// so we sort all cameras by depth and find the one with no render target
		/// </summary>
		public static Camera GetUltimateRenderCamera()
		{
			Camera result = Camera.main;

			{
				// Iterate all enabled cameras
				float highestDepth = float.MinValue;
				Camera[] enabledCameras = Camera.allCameras;
				for (int cameraIndex = 0; cameraIndex < enabledCameras.Length; cameraIndex++)
				{
					Camera camera = enabledCameras[cameraIndex];
					// Ignore null cameras
					if (camera != null)
					{
						// Ignore cameras that are hidden or have a targetTexture
						bool isHidden = (camera.hideFlags & HideFlags.HideInHierarchy) == HideFlags.HideInHierarchy;
						if (!isHidden && camera.targetTexture == null)
						{
							// Ignore cameras that render nothing
							if (camera.pixelRect.width > 0f && camera.pixelHeight > 0f)
							{
								// Keep the one with highest depth
								// TODO: handle the case where camera depths are equal - which one is first then?
								if (camera.depth >= highestDepth)
								{
									highestDepth = camera.depth;
									result = camera;
								}
							}
						}
					}
				}
			}

			return result;
		}

		public static bool HasContributingCameras(Camera parentCamera)
		{
			bool result = true;

			// If the camera doesn't clear the target completely then it may have other contributing cameras
			if (parentCamera.rect == new Rect(0f, 0f, 1f, 1f))
			{
				if (parentCamera.clearFlags == CameraClearFlags.Skybox ||
					parentCamera.clearFlags == CameraClearFlags.Color)
				{
					result = false;
				}
			}

			return result;
		}

		/// <summary>
		/// Returns a list of cameras sorted in render order from first to last that contribute to the rendering to parentCamera
		/// </summary>
		public static Camera[] FindContributingCameras(Camera parentCamera)
		{
			System.Collections.Generic.List<Camera> validCameras = new System.Collections.Generic.List<Camera>(8);
			{
				// Iterate all enabled/disabled cameras (they may become enabled later on)
				Camera[] allCameras = (Camera[])Resources.FindObjectsOfTypeAll(typeof(Camera));
				for (int cameraIndex = 0; cameraIndex < allCameras.Length; cameraIndex++)
				{
					Camera camera = allCameras[cameraIndex];
					// Ignore null cameras and camera that is the parent
					if (camera != null && camera != parentCamera)
					{
						// Only allow cameras with depth less or equal to parent camera
						if (camera.depth <= parentCamera.depth)
						{
							// Ignore cameras that are hidden or have a targetTexture that doesn't match the parent
							bool isHidden = (camera.hideFlags & HideFlags.HideInHierarchy) == HideFlags.HideInHierarchy;
							if (!isHidden && camera.targetTexture == parentCamera.targetTexture)
							{
								// Ignore cameras that render nothing
								if (camera.pixelRect.width > 0 && camera.pixelHeight > 0)
								{
									validCameras.Add(camera);
								}
							}
						}
					}
				}
			}

			if (validCameras.Count > 1)
			{
				// Sort by depth (render order)
				// TODO: handle the case where camera depths are equal - which one is first then?
				validCameras.Sort(delegate (Camera a, Camera b)
				{
					if (a != b)	// Pre .Net 4.6.2 Sort() can compare an elements with itself
					{
						if (a.depth < b.depth)
						{
							return -1;
						}
						else if (a.depth > b.depth)
						{
							return 1;
						}
						else if (a.depth == b.depth)
						{
							UnityEngine.Debug.LogWarning("[AVProMovieCapture] Cameras '" + a.name + "' and '" + b.name + "' have the same depth value - unable to determine render order: " + a.depth);
						}
					}
					return 0;
				});

				// Starting from the last camera to render, find the first one that clears the screen completely
				for (int i = (validCameras.Count - 1); i >= 0; i--)
				{
					if (validCameras[i].rect == new Rect(0f, 0f, 1f, 1f))
					{
						if (validCameras[i].clearFlags == CameraClearFlags.Skybox ||
							validCameras[i].clearFlags == CameraClearFlags.Color)
						{
							// Remove all cameras before this
							validCameras.RemoveRange(0, i);
							break;
						}
					}
				}
			}

			return validCameras.ToArray();
		}

#if UNITY_EDITOR_OSX || (UNITY_STANDALONE_OSX && !UNITY_EDITOR)
		private static string PhotosScheme { get { return "photos://"; } }
		private static string FileScheme { get { return "file://"; } }
#elif UNITY_IOS && !UNITY_EDITOR
		private static string PhotosScheme { get { return "photos-redirect://"; } }
		private static string FileScheme { get { return "shareddocuments://"; } }
#endif

		private static string URLEscapePathByPercentEncoding(string path)
		{
			return path.Replace("%",  "%25")		// Escape percents first so escaped character's percents aren't themselves escaped
					   .Replace(" ",  "%20")
					   .Replace("\"", "%22")
					   .Replace("#",  "%23")
					   .Replace(";",  "%3B")
					   .Replace("<",  "%3C")
					   .Replace(">",  "%3E")
					   .Replace("?",  "%3F")
					   .Replace("[",  "%5B")
					   .Replace("\\", "%5C")
					   .Replace("]",  "%5D")
					   .Replace("^",  "%5E")
					   .Replace("`",  "%60")
					   .Replace("{",  "%7B")
					   .Replace("|",  "%7C")
					   .Replace("}",  "%7D");
		}

		public static bool ShowInExplorer(string itemPath)
		{
			bool result = false;
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
			string url;
			if (itemPath.StartsWith("avpmc-photolibrary://"))
			{
				// TODO: Find out how to open photos to a specific album/item
				url = PhotosScheme;
			}
			else
			{
				url = FileScheme + URLEscapePathByPercentEncoding(Directory.GetParent(itemPath).FullName);
			}
			UnityEngine.Debug.LogFormat("ShowInExplorer - opening URL: {0}", url);
			UnityEngine.Application.OpenURL(url);
			result = true;
#else
#if !UNITY_WEBPLAYER
			itemPath = Path.GetFullPath(itemPath.Replace(@"/", @"\"));   // explorer doesn't like front slashes
			if (File.Exists(itemPath))
			{
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
				Process.Start("explorer.exe", "/select," + itemPath);
#else

#endif
				result = true;
			}
			else if (Directory.Exists(itemPath))
			{
				// NOTE: We use OpenURL() instead of the explorer process so that it opens explorer inside the folder
				UnityEngine.Application.OpenURL(itemPath);
				result = true;
			}

#endif
#endif // UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX

			return result;
		}

		public static bool OpenInDefaultApp(string itemPath)
		{
			bool result = false;
#if UNITY_EDITOR_OSX || (!UNITY_EDITOR && (UNITY_STANDALONE_OSX || UNITY_IOS))
			string url;
			if (itemPath.StartsWith("avpmc-photolibrary://"))
			{
				// TODO: Find out how to open photos to a specific album/item
				url = PhotosScheme;
			}
			else
			{
				url = FileScheme + URLEscapePathByPercentEncoding(itemPath);
			}
			UnityEngine.Debug.LogFormat("OpenInDefaultApp - opening URL: {0}", url);
			UnityEngine.Application.OpenURL(url);
			result = true;
#else
			itemPath = Path.GetFullPath(itemPath.Replace(@"/", @"\"));
			if (File.Exists(itemPath))
			{
				UnityEngine.Application.OpenURL(itemPath);
				result = true;
			}
			else if (Directory.Exists(itemPath))
			{
				UnityEngine.Application.OpenURL(itemPath);
				result = true;
			}
#endif
			return result;
		}

		public static long GetFileSize(string filename)
		{
#if UNITY_WEBPLAYER
			return 0;
#else
			System.IO.FileInfo fi = new System.IO.FileInfo(filename);
			return fi.Length;
#endif
		}


#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)

		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		[return: MarshalAs(UnmanagedType.Bool)]
		private static extern bool GetDiskFreeSpaceEx(string lpDirectoryName,
														out ulong lpFreeBytesAvailable,
														out ulong lpTotalNumberOfBytes,
														out ulong lpTotalNumberOfFreeBytes);

		public static bool DriveFreeBytes(string folderName, out ulong freespace)
		{
			freespace = 0;
			if (string.IsNullOrEmpty(folderName))
			{
				throw new System.ArgumentNullException("folderName");
			}

			if (!folderName.EndsWith("\\"))
			{
				folderName += '\\';
			}

			ulong free = 0, dummy1 = 0, dummy2 = 0;

			if (GetDiskFreeSpaceEx(folderName, out free, out dummy1, out dummy2))
			{
				freespace = free;
				return true;
			}
			else
			{
				return false;
			}
		}
#endif

		public static string GetImageFileExtension(ImageSequenceFormat format)
		{
			string result = string.Empty;
			switch (format)
			{
				case ImageSequenceFormat.PNG:
					result = "png";
					break;
				case ImageSequenceFormat.JPEG:
					result = "jpg";
					break;
				case ImageSequenceFormat.TIFF:
					result = "tiff";
					break;
				case ImageSequenceFormat.HEIF:
					result = "heif";
					break;
			}
			return result;
		}
	}

}
