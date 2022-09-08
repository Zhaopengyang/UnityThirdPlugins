using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Capture from a WebCamTexture object
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Capture From WebCamTexture", 3)]
	public class CaptureFromWebCamTexture : CaptureFromTexture
	{
#if AVPRO_MOVIECAPTURE_WEBCAMTEXTURE_SUPPORT
		private WebCamTexture _webcam = null;

		public WebCamTexture WebCamTexture
		{
			get { return _webcam; }
			set { _webcam = value; SetSourceTexture(_webcam); }
		}

		public override void UpdateFrame()
		{
			// WebCamTexture doesn't update every Unity frame
			if (_webcam != null && _webcam.didUpdateThisFrame)
			{
				UpdateSourceTexture();
			}

			base.UpdateFrame();
		}
#else
		public override void Start()
		{
			Debug.LogError("[AVProMovieCapture] To use WebCamTexture capture component/demo you must add the string AVPRO_MOVIECAPTURE_WEBCAMTEXTURE_SUPPORT must be added to `Scriping Define Symbols` in `Player Settings > Other Settings > Script Compilation`");
		}
#endif // AVPRO_MOVIECAPTURE_WEBCAMTEXTURE_SUPPORT
	}
}