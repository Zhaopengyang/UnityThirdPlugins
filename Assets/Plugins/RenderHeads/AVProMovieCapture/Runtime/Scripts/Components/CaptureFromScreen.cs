using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Capture from the screen (backbuffer).  Everything is captured as it appears on the screen, including IMGUI rendering.
	/// This component waits for the frame to be completely rendered and then captures it.
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Capture From Screen", 0)]
	public class CaptureFromScreen : CaptureBase
	{
		//private const int NewFrameSleepTimeMs = 6;
		[SerializeField] bool _captureMouseCursor = false;
		[SerializeField] MouseCursor _mouseCursor = null;

		private System.IntPtr _targetNativePointer = System.IntPtr.Zero;
		private RenderTexture _resolveTexture = null;
		private CommandBuffer _commandBuffer = null;

		public bool CaptureMouseCursor
		{
			get { return _captureMouseCursor; }
			set { _captureMouseCursor = value; }
		}

		public MouseCursor MouseCursor
		{
			get { return _mouseCursor; }
			set { _mouseCursor = value; }
		}

		public override bool PrepareCapture()
		{
			if (_capturing)
			{
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
				Debug.LogError("[AVProMovieCapture] OpenGL not yet supported for CaptureFromScreen component, please use Direct3D11 instead. You may need to switch your build platform to Windows.");
				return false;
			}
#endif

			if (_mouseCursor != null)
			{
				_mouseCursor.enabled = _captureMouseCursor;
			}

#if UNITY_EDITOR
			if (Display.displays.Length > 1)
			{
				bool isSecondDisplayActive = false;
				for (int i = 1; i < Display.displays.Length; i++)
				{
					if (Display.displays[i].active)
					{
						isSecondDisplayActive = true;
						break;
					}
				}
				if (isSecondDisplayActive)
				{
					Debug.LogError("[AVProMovieCapture] CaptureFromScreen doesn't work correctly (can cause stretching or incorrect display capture) when there are multiple displays are active.  Use CaptureFromCamera instead.");
				}				
			}
#endif

			SelectRecordingResolution(Screen.width, Screen.height);

			_pixelFormat = NativePlugin.PixelFormat.RGBA32;
			if (SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL") && !SystemInfo.graphicsDeviceVersion.Contains("emulated"))
			{
				// TODO: add this back in once we have fixed opengl support
				_pixelFormat = NativePlugin.PixelFormat.BGRA32;
				_isTopDown = true;
			}
			else
			{
				_isTopDown = false;

				if (_isDirectX11)
				{
					_isTopDown = false;
				}
			}

			GenerateFilename();

			return base.PrepareCapture();
		}

		private void CopyRenderTargetToTexture()
		{
#if false
			// RJT TODO: If using D3D12 we need to read the current 'Display.main.colorBuffer', pass it down
			// to native and extract the texture using 'IUnityGraphicsD3D12v5::TextureFromRenderBuffer()'
			// - Although, as is, this doesn't work: https://forum.unity.com/threads/direct3d12-native-plugin-render-to-screen.733025/
			if (_targetNativePointer == System.IntPtr.Zero)
			{
				_targetNativePointer = Display.main.colorBuffer.GetNativeRenderBufferPtr();
//				_targetNativePointer = Graphics.activeColorBuffer.GetNativeRenderBufferPtr();
				NativePlugin.SetColourBuffer(_handle, _targetNativePointer);
			}
#endif
#if true
			if ((_targetNativePointer == System.IntPtr.Zero) ||
				(_resolveTexture && ((_resolveTexture.width != Screen.width) || (_resolveTexture.height != Screen.height)))
			)
			{
				FreeRenderResources();

				// Create RT matching screen extents
				_resolveTexture = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB, 1);
				_resolveTexture.Create();
				_targetNativePointer = _resolveTexture.GetNativeTexturePtr();
				NativePlugin.SetTexturePointer(_handle, _targetNativePointer);

				// Create command buffer
				_commandBuffer = new CommandBuffer();
				_commandBuffer.name = "AVPro Movie Capture copy";
				_commandBuffer.Blit(BuiltinRenderTextureType.CurrentActive, _resolveTexture);
			}
#endif

			Graphics.ExecuteCommandBuffer(_commandBuffer);
		}

		private void FreeRenderResources()
		{
			// Command buffer
			if (_commandBuffer != null)
			{
				_commandBuffer.Release();
				_commandBuffer = null;
			}

			// Resolve texture
			_targetNativePointer = System.IntPtr.Zero;
			if (_resolveTexture)
			{
				RenderTexture.ReleaseTemporary(_resolveTexture);
				_resolveTexture = null;
			}
		}

		public override void UnprepareCapture()
		{
			if (_handle != -1)
			{
				#if false
				NativePlugin.SetColourBuffer(_handle, System.IntPtr.Zero);
				#endif
				NativePlugin.SetTexturePointer(_handle, System.IntPtr.Zero);
			}

			FreeRenderResources();

			if (_mouseCursor != null)
			{
				_mouseCursor.enabled = false;
			}

			base.UnprepareCapture();
		}

		private IEnumerator FinalRenderCapture()
		{
			yield return _waitForEndOfFrame;

			TickFrameTimer();

			bool canGrab = true;

			if (IsUsingMotionBlur())
			{
				// If the motion blur is still accumulating, don't grab this frame
				canGrab = _motionBlur.IsFrameAccumulated;
			}

			if (canGrab && CanOutputFrame())
			{
				// Grab final RenderTexture into texture and encode
				EncodeUnityAudio();

				// RJT NOTE: Separate D3D12 path for now as it can't grab native RT
				if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12)
				{
					CopyRenderTargetToTexture();
					RenderThreadEvent(NativePlugin.PluginEvent.CaptureFrameBuffer);
				}
				else
				{
					RenderThreadEvent(NativePlugin.PluginEvent.CaptureFrameBuffer);

					// RJT NOTE: Causes screen flickering under D3D12, even if we're not doing any rendering at native level
					if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
					{
						GL.InvalidateState();
					}
				}

				UpdateFPS();
			}

			RenormTimer();

			//yield return null;
		}

		public override void UpdateFrame()
		{
			if (_capturing && !_paused)
			{
				StartCoroutine(FinalRenderCapture());
			}
			base.UpdateFrame();
		}
	}
}