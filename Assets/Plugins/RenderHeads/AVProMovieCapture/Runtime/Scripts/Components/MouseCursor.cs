using UnityEngine;
using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// <summary>
	/// Draws a mouse cursor on the screen using IMGUI, allowing the cursor to be captured when using CaptureFromScreen component
	/// </summary>
	[AddComponentMenu("AVPro Movie Capture/Utils/Render Mouse Cursor", 302)]
	public class MouseCursor : MonoBehaviour
	{
		[SerializeField] Texture2D _texture = null;
		[SerializeField] Vector2 _hotspotOffset = Vector2.zero;
		[SerializeField, Range(1, 16)] int _sizeScale = 1;
		[SerializeField] int _depth = -9999;

		// State
		private GUIContent _content;

		void Start()
		{
			SetTexture(_texture);
		}

		public void SetTexture(Texture2D texture)
		{
			if (texture != null)
			{
				_content = new GUIContent(texture);
				_texture = texture;
			}
		}

		private void OnGUI()
		{
			if (_content != null)
			{
				GUI.depth = _depth;

				Vector2 p = Event.current.mousePosition;

				Rect rect = new Rect(p.x - _hotspotOffset.x, p.y - _hotspotOffset.y, _texture.width * _sizeScale, _texture.height * _sizeScale);

				GUI.Label(rect, _content);
			}
		}
	}
}