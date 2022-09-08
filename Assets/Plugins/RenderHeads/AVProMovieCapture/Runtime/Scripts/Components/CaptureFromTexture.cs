using UnityEngine;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Capture from a Texture object (including RenderTexture, WebcamTexture)
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Capture From Texture", 3)]
	public class CaptureFromTexture : CaptureBase
	{
		[Tooltip("If enabled the method the encoder will only process frames each time UpdateSourceTexture() is called. This is useful if the texture is updating at a different rate compared to Unity, eg for webcam capture.")]
		[SerializeField] bool _manualUpdate = false;

		public bool IsManualUpdate
		{
			get { return _manualUpdate; }
			set { _manualUpdate = value; }
		}

		private Texture _sourceTexture;
		private RenderTexture _resolveTexture;
		private System.IntPtr _targetNativePointer = System.IntPtr.Zero;
		private bool _isSourceTextureChanged = false;

		public void SetSourceTexture(Texture texture)
		{
			_sourceTexture = texture;
		}

		private bool RequiresResolve(Texture texture)
		{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX || (UNITY_IOS && !UNITY_EDITOR) || (UNITY_ANDROID)
			// Texture resolve wholly unnecessary on macOS and iOS
			return false;
#else
			bool result = false;

			if (texture is RenderTexture)
			{
				RenderTexture rt = texture as RenderTexture;

				// Linear textures require resolving to sRGB
				result = !rt.sRGB;

				if (!result &&
					(rt.format != RenderTextureFormat.ARGB32) &&
					(rt.format != RenderTextureFormat.Default) &&
					(rt.format != RenderTextureFormat.BGRA32)
					)
				{
					// Exotic texture formats require resolving
					result = true;
				}
			}
			else
			{
				// Any other texture type needs to be resolve to RenderTexture
				result = true;
			}

			return result;
#endif
		}

		public void UpdateSourceTexture()
		{
			_isSourceTextureChanged = true;
		}

		private bool ShouldCaptureFrame()
		{
			return (_capturing && !_paused && _sourceTexture != null);
		}

		private bool HasSourceTextureChanged()
		{
			return (!_manualUpdate || (_manualUpdate && _isSourceTextureChanged));
		}

		public override void UpdateFrame()
		{
			if (_useWaitForEndOfFrame)
			{
				StartCoroutine(FinalRenderCapture());
				base.UpdateFrame();
			}
			else
			{
				Capture();
				base.UpdateFrame();
			}
		}

		private IEnumerator FinalRenderCapture()
		{
			yield return _waitForEndOfFrame;

			Capture();
		}

		private void Capture()
		{
			TickFrameTimer();

			AccumulateMotionBlur();

			if (ShouldCaptureFrame())
			{
				bool hasSourceTextureChanged = HasSourceTextureChanged();

				// If motion blur is enabled, wait until all frames are accumulated
				if (IsUsingMotionBlur())
				{
					// If the motion blur is still accumulating, don't grab this frame
					hasSourceTextureChanged = _motionBlur.IsFrameAccumulated;
				}

				_isSourceTextureChanged = false;
				if (hasSourceTextureChanged)
				{
					if ((_manualUpdate /*&& NativePlugin.IsNewFrameDue(_handle)*/) || CanOutputFrame())
					{
						// If motion blur is enabled, use the motion blur result
						Texture sourceTexture = _sourceTexture;
						if (IsUsingMotionBlur())
						{
							sourceTexture = _motionBlur.FinalTexture;
						}

						// If the texture isn't suitable then blit it to the Rendertexture so the native plugin can grab it
						if (RequiresResolve(sourceTexture))
						{
							CreateResolveTexture(_targetWidth, _targetHeight);
							_resolveTexture.DiscardContents();

							// Between Unity 2018.1.0 and 2018.3.0 Unity doesn't seem to set the correct sRGBWrite state and keeps it as false
#if (UNITY_2018_1_OR_NEWER && !UNITY_2018_3_OR_NEWER)
							bool sRGBWritePrev = GL.sRGBWrite;
							GL.sRGBWrite = true;
#endif

							Graphics.Blit(sourceTexture, _resolveTexture);
							sourceTexture = _resolveTexture;

#if (UNITY_2018_1_OR_NEWER && !UNITY_2018_3_OR_NEWER)
							GL.sRGBWrite = sRGBWritePrev;
#endif
						}

						if (_targetNativePointer == System.IntPtr.Zero || _supportTextureRecreate)
						{
							// NOTE: If support for captures to survive through alt-tab events, or window resizes where the GPU resources are recreated
							// is required, then this line is needed.  It is very expensive though as it does a sync with the rendering thread.
							_targetNativePointer = sourceTexture.GetNativeTexturePtr();
						}

						NativePlugin.SetTexturePointer(_handle, _targetNativePointer);

						RenderThreadEvent(NativePlugin.PluginEvent.CaptureFrameBuffer);

						if (!IsUsingMotionBlur())
						{
							_isSourceTextureChanged = false;
						}
						EncodeUnityAudio();

						UpdateFPS();
					}
				}
			}

			RenormTimer();
		}

		private void CreateResolveTexture(int width, int height)
		{
			if (_resolveTexture != null)
			{
				if (_resolveTexture.width != width ||
					_resolveTexture.height != height)
				{
					RenderTexture.ReleaseTemporary(_resolveTexture);
					_resolveTexture = null;
				}
			}
			if (_resolveTexture == null)
			{
				RenderTextureReadWrite readWriteMode = RenderTextureReadWrite.sRGB;
#if AVPRO_MOVIECAPTURE_WEBCAMTEXTURE_SUPPORT
				if (QualitySettings.activeColorSpace == ColorSpace.Linear && _sourceTexture is WebCamTexture)
				{
					// WebCamTexture has odd behaviour, and we're found that we need to resolve without the sRGB conversion
					// It's still not 100% correct though - dark colours seem to get crushed to black - we suspect
					// this is due to it using limited range instead of full...

					// NOTE: This is now commented out as the behaviour isn't correct in Unity 2020
					//	readWriteMode = RenderTextureReadWrite.Linear;
				}
#endif
				_resolveTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, readWriteMode);
				_resolveTexture.Create();
				_targetNativePointer = _resolveTexture.GetNativeTexturePtr();
			}
		}

		private void AccumulateMotionBlur()
		{
			if (_motionBlur != null)
			{
				if (ShouldCaptureFrame() && HasSourceTextureChanged())
				{
					_motionBlur.Accumulate(_sourceTexture);
					_isSourceTextureChanged = false;
				}
			}
		}

		public override Texture GetPreviewTexture()
		{
			if (IsUsingMotionBlur())
			{
				return _motionBlur.FinalTexture;
			}
			if (_resolveTexture != null)
			{
				return _resolveTexture;
			}
			if (_sourceTexture is RenderTexture)
			{
				return _sourceTexture;
			}
#if AVPRO_MOVIECAPTURE_WEBCAMTEXTURE_SUPPORT
			if (_sourceTexture is WebCamTexture)
			{
				return _sourceTexture;
			}
#endif
			return Texture2D.whiteTexture;
		}

		public override bool PrepareCapture()
		{
			if (_capturing)
			{
				return false;
			}

			if (_sourceTexture == null)
			{
				Debug.LogError("[AVProMovieCapture] No texture set to capture");
				return false;
			}
#if UNITY_EDITOR_WIN || (!UNITY_EDITOR && UNITY_STANDALONE_WIN)
			if (SystemInfo.graphicsDeviceVersion.StartsWith("Direct3D 9"))
			{
				Debug.LogError("[AVProMovieCapture] Direct3D9 not yet supported, please use Direct3D11 instead.");
				return false;
			}
			else if (SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL") && !SystemInfo.graphicsDeviceVersion.Contains("emulated"))
			{
				Debug.LogError("[AVProMovieCapture] OpenGL not yet supported for CaptureFromTexture component, please use Direct3D11 instead. You may need to switch your build platform to Windows.");
				return false;
			}
#endif
			_pixelFormat = NativePlugin.PixelFormat.RGBA32;
			_isSourceTextureChanged = false;

			SelectRecordingResolution(_sourceTexture.width, _sourceTexture.height);

			if (_resolveTexture != null)
			{
				RenderTexture.ReleaseTemporary(_resolveTexture);
				_resolveTexture = null;
			}

			GenerateFilename();

			return base.PrepareCapture();
		}

		public override void UnprepareCapture()
		{
			_targetNativePointer = System.IntPtr.Zero;

			if (_handle != -1)
			{
				NativePlugin.SetTexturePointer(_handle, System.IntPtr.Zero);
			}

			if (_resolveTexture != null)
			{
				RenderTexture.ReleaseTemporary(_resolveTexture);
				_resolveTexture = null;
			}

			base.UnprepareCapture();
		}
	}
}
