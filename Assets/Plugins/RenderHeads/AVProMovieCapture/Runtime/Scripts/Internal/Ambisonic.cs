using UnityEngine;
using System;
using System.Runtime.InteropServices;
#if UNITY_IOS || UNITY_TVOS || ENABLE_IL2CPP
using AOT;
#endif

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public enum AmbisonicOrder : int
	{
		//Zero = 0,
		First = 1,
		Second = 2,
		Third = 3,
	}

	public enum AmbisonicFormat
	{
		FuMa,		// FuMa channel ordering and normalisation
		ACN_SN3D,	// ACN channel ordering with SN3D normalisation
	}

	public enum AmbisonicChannelOrder : int
	{
		FuMa,
		ACN,
	}

	public enum AmbisonicNormalisation : int
	{
		FuMa,
		SN3D,
	}

	public partial class NativePlugin
	{
		//////////////////////////////////////////////////////////////////////////
		// Ambisonic

		[DllImport(PluginName, EntryPoint="AVPMC_AddAmbisonicSourceInstance")]
		public static extern IntPtr AddAmbisonicSourceInstance(int maxCoefficients);

		[DllImport(PluginName, EntryPoint="AVPMC_RemoveAmbisonicSourceInstance")]
		public static extern void RemoveAmbisonicSourceInstance(IntPtr instance);

		[DllImport(PluginName, EntryPoint = "AVPMC_UpdateAmbisonicWeights")]
		public static extern void UpdateAmbisonicWeights(IntPtr instance, float azimuth, float elevation, AmbisonicOrder order, AmbisonicChannelOrder channelOrder, float[] normalisationWeights);

		[DllImport(PluginName, EntryPoint = "AVPMC_EncodeMonoToAmbisonic")]
		public static extern void EncodeMonoToAmbisonic(IntPtr instance, float[] inSamples, int inSamplesOffset, int inFrameCount, int inChannelCount, float[] outSamples, int outSamplesOffset, int outSamplesLength, AmbisonicOrder order);
	}

	/// <summary>
	/// Internal helper class for encoding ambisonic audio
	/// </summary>
	public static class Ambisonic
	{
		public const int MaxCoeffs = 16;
		static float[] _weightsFuMa = null;
		static float[] _weightsSN3D = null;

		public static float[] GetNormalisationWeights(AmbisonicNormalisation normalisation)
		{
			return (normalisation == AmbisonicNormalisation.FuMa) ? _weightsFuMa : _weightsSN3D;
		}

		public static int GetCoeffCount(AmbisonicOrder order)
		{
			if (order == AmbisonicOrder.First) { return 4; }
			else if (order == AmbisonicOrder.Second) { return 9; }
			else if (order == AmbisonicOrder.Third) { return 16; }
			return 0;
		}

		public static AmbisonicChannelOrder GetChannelOrder(AmbisonicFormat format)
		{
			return (format == AmbisonicFormat.FuMa) ? AmbisonicChannelOrder.FuMa : AmbisonicChannelOrder.ACN;
		}

		public static AmbisonicNormalisation GetNormalisation(AmbisonicFormat format)
		{
			return (format == AmbisonicFormat.FuMa) ? AmbisonicNormalisation.FuMa : AmbisonicNormalisation.SN3D;
		}

		static Ambisonic()
		{
			_weightsFuMa = BuildWeightsFuMa();
			_weightsSN3D = BuildWeightsSN3D();
		}

		static float[] BuildWeightsFuMa()
		{
			float[] w = new float[MaxCoeffs];
			w[0] = 1f / Mathf.Sqrt(2f);

			w[1] = 1f;
			w[2] = 1f;
			w[3] = 1f;

			w[4] = 1f;
			w[5] = 2f / Mathf.Sqrt(3f);
			w[6] = 2f / Mathf.Sqrt(3f);
			w[7] = 2f / Mathf.Sqrt(3f);
			w[8] = 2f / Mathf.Sqrt(3f);

			w[9] = 1f;
			w[10] = Mathf.Sqrt(45f / 32f);
			w[11] = Mathf.Sqrt(45f / 32f);
			w[12] = 3f / Mathf.Sqrt(5f);
			w[13] = 3f / Mathf.Sqrt(5f);
			w[14] = Mathf.Sqrt(8f / 5f);
			w[15] = Mathf.Sqrt(8f / 5f);

			return w;
		}

		// Returns N which is the same as the order
		static int GetN(int acn)
		{
			return Mathf.FloorToInt(Mathf.Sqrt(acn));
		}

		// Returns M which is the signed delta offset from the middle of the pyramid
		static int GetM(int acn)
		{
			int n = GetN(acn);
			return acn - (n * n) - n;
		}

		static int Factorial(int x)
		{
			int result = 1;
			for (int i = 2; i <= x; i++)
			{
				result *= i;
			}
			return result;
		}

		static float GetNormalisationSN3D(int acn)
		{
			int n = GetN(acn);
			int m = GetM(acn);
			return GetNormalisationSN3D(n, m);
		}

		static float GetNormalisationSN3D(int n, int m)
		{
			float dm = (m == 0) ? 1f : 0f;
			float l1 = (2f - dm);

			float a = Factorial(n - Mathf.Abs(m));
			float b = Factorial(n + Mathf.Abs(m));
			float l2 = a / b;

			return Mathf.Sqrt(l1 * l2);
		}

		static float GetNormalisationN3D(int n, int m)
		{
			return GetNormalisationSN3D(n, m) * Mathf.Sqrt(2f * n + 1f);
		}

		static float[] BuildWeightsSN3D()
		{
			float[] w = new float[MaxCoeffs];
			for (int acn = 0; acn < w.Length; acn++)
			{
				w[acn] = GetNormalisationSN3D(acn);
			}
			return w;
		}

		/// <summary>
		/// The coordinate system used in Ambisonics follows the right hand rule convention with
		/// positive X pointing forwards,
		/// positive Y pointing to the left and
		/// positive Z pointing upwards
		/// Horizontal angles run anticlockwise from due front and
		/// vertical angles are positive above the horizontal, negative below.
		/// </summary>
		internal struct PolarCoord
		{
			/// Azimuth (horizontal) angle in radians, 0..2PI
			public float azimuth;

			/// Elevation (vertical) angle in radians, -PI..PI
			public float elevation;

			//public float distance;

			public void FromCart(Vector3 position)
			{
				// Convert from Unity's left-hand system to Ambisonics right-hand system
				float x = position.z;
				float y = -position.x;
				float z = position.y;

				// The azimuth angle is zero straight ahead and increases counter-clockwise.
				azimuth = Mathf.Rad2Deg * Mathf.Atan2(y, x);

				// Clamp
				if (azimuth < 0f)
				{
					azimuth += 360f;
				}

				// The elevation angle  is zero on the horizontal plane and positive in the upper hemisphere.
				elevation = Mathf.Rad2Deg * Mathf.Atan2(z, Mathf.Sqrt(x * x + y * y));
				elevation = Mathf.Clamp(elevation, -90f, 90f);

				// NOTE: Distance is not currently used, but there may be scope in the future
				//distance = Mathf.Sqrt( x * x + y * y + z * z );

				// Back to radians
				azimuth = Mathf.Deg2Rad * azimuth;
				elevation = Mathf.Deg2Rad * elevation;
			}
		};
	}
}