using UnityEngine;
using System.Collections;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture.Demos
{
	public class AmbisonicAudioDemo : MonoBehaviour
	{
		[SerializeField] Transform[] _audioObjects = null;
		[SerializeField] AudioSource[] _audioSources = null;

		struct Instance
		{
			float x, y, z;
			float radius;
		}

		private int index;
		//private List<Instance> _instances = new List<Instance>(4);

		/*void Start()
		{
			foreach (Transform t in _audioObjects)
			{
				Instance instance = new Instance();
				instance.radius = Random.Range(0.5f, 2f);
				instance.x = Random.Range(0f, 100f);
				instance.y = Random.Range(0f, 100f);
				instance.z = Random.Range(0f, 100f);
				_instances.Add(instance);
			}
		}*/

		void Update()
		{
			float[] samples = new float[4];
			foreach (AudioSource audio in _audioSources)
			{
				audio.GetOutputData(samples, 0);
				float sample = Mathf.Abs(samples[2]);
				sample = Mathf.Sqrt(sample);
				float scale = audio.gameObject.transform.localScale.x;
				//audio.GetSpectrumData(samples, 0, FFTWindow.Hanning);

				scale = 0.15f + Mathf.Lerp(scale, sample, Time.deltaTime * 20f) * 0.85f;
				audio.gameObject.transform.localScale = Vector3.one * scale;
			}
			
			int index = 0;
			foreach (Transform t in _audioObjects)
			{
				//Vector3 v = axes[index % axes.Length];
				//t.RotateAround(Vector3.zero, Vector3.up, 40 * Time.deltaTime);
				//Quaternion q = Quaternion.Euler(0f, Time.timeSinceLevelLoad * 50f, 0f);
				//Matrix4x4.TRS(Vector3)

				float time = Time.timeSinceLevelLoad + index * 1.321f;
				float tt = Mathf.PingPong(Mathf.Sin(time * 2.23f) + 1f, 2f) / 2f;
				float r = Mathf.Lerp(0.5f, 3f, tt);
				float x = Mathf.Sin(time * 1f) * r;
				float z = Mathf.Cos(time * 1.13f) * r;
				float y = Mathf.Sin(time * 1.23f) * 1f;
							

				//Vector3 v = t.position.normalized * r;

				t.position = new Vector3(x, y, z);

				//t.position = new Vector3(t.position.x, y, t.position.z);
				index++;
			}

		}
	}
}