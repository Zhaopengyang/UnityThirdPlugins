using UnityEngine;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Demos
{
	/// <summary>
	/// Demo code to create and write frames manually into a movie using the low-level API via scripting
	/// </summary>
	public class ScriptCaptureDemo : MonoBehaviour
	{
		private const string X264CodecName = "x264vfw - H.264/MPEG-4 AVC codec";
		private const string FallbackCodecName = "Uncompressed";

		/*[SerializeField]
		private int _width = 512;

		[SerializeField]
		private int _height = 512;

		[SerializeField]
		private int _frameRate = 30;

		[SerializeField]
		private string _filePath;*/

		// State
		private Codec _videoCodec;
		private int _encoderHandle;

		private void Start()
		{
			if (NativePlugin.Init())
			{
				// Find the index for the video codec
				_videoCodec = CodecManager.FindCodec(CodecType.Video, X264CodecName);
				if (_videoCodec == null)
				{
					_videoCodec = CodecManager.FindCodec(CodecType.Video, FallbackCodecName);
				}
			}
			else
			{
				this.enabled = false;
			}
		}

		private void OnDestroy()
		{
			NativePlugin.Deinit();
		}

		public void CreateVideoFromByteArray(string filePath, int width, int height, int frameRate)
		{
			byte[] frameData = new byte[width * height * 4];
			GCHandle frameHandle = GCHandle.Alloc(frameData, GCHandleType.Pinned);

			// Start the recording session
			int encoderHandle = NativePlugin.CreateRecorderVideo(filePath, (uint)width, (uint)height, frameRate, (int)NativePlugin.PixelFormat.RGBA32, false, false, _videoCodec.Index, AudioCaptureSource.None, 0, 0, -1, -1, true, null);
			if (encoderHandle >= 0)
			{
				NativePlugin.Start(encoderHandle);

				// Write out 100 frames
				int numFrames = 100;
				for (int i = 0; i < numFrames; i++)
				{
					// TODO: fill the byte array with your own data :)


					// Wait for the encoder to be ready for the next frame
					int numAttempts = 32;
					while (numAttempts > 0)
					{
						if (NativePlugin.IsNewFrameDue(encoderHandle))
						{
							// Encode the new frame
							NativePlugin.EncodeFrame(encoderHandle, frameHandle.AddrOfPinnedObject());
							break;
						}
						System.Threading.Thread.Sleep(1);
						numAttempts--;
					}
				}

				// End the session
				NativePlugin.Stop(encoderHandle, false);
				NativePlugin.FreeRecorder(encoderHandle);
			}

			if (frameHandle.IsAllocated)
			{
				frameHandle.Free();
			}
		}
	}
}