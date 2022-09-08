using System.IO;

//-----------------------------------------------------------------------------
// Copyright 2014-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public class WavWriter : System.IDisposable
	{
		private static byte[] RIFF_HEADER = new byte[] { 0x52, 0x49, 0x46, 0x46 };
		private static byte[] FORMAT_WAVE = new byte[] { 0x57, 0x41, 0x56, 0x45 };
		private static byte[] FORMAT_TAG  = new byte[] { 0x66, 0x6d, 0x74, 0x20 };
		private static byte[] AUDIO_FORMAT_PCM = new byte[] { 0x01, 0x00 };
		private static byte[] AUDIO_FORMAT_FLOAT = new byte[] { 0x03, 0x00 };
		private static byte[] SUBCHUNK_ID  = new byte[] { 0x64, 0x61, 0x74, 0x61 };
		private static byte[] FACTCHUNK_ID  = new byte[] { 0x66, 0x61, 0x63, 0x74 };

		private const int BufferDuration = 4;

		public enum SampleFormat
		{
			PCM16 = 2,
			Float32 = 4,
		}

		private FileStream _stream;
		private byte[] _outBytes;
		private int _byteCount;
		private int _byteCountTotal;
		private int _channelCount;
		private int _sampleRate;
		private SampleFormat _sampleFormat;
		private int _headerSize;

		public WavWriter(string path, int channelCount, int sampleRate, SampleFormat sampleFormat = SampleFormat.Float32)
		{
			int bytesPerSample = (int)sampleFormat;
			_outBytes = new byte[sampleRate * bytesPerSample * channelCount * BufferDuration];

			_channelCount = channelCount;
			_sampleRate = sampleRate;
			_sampleFormat = sampleFormat;

			_stream = new FileStream(path, System.IO.FileMode.Create);
			WriteHeader(0);
		}

		public void Dispose()
		{
			_stream.Seek(0, SeekOrigin.Begin);
			WriteHeader(_byteCountTotal);
			_stream.Close();
			_stream.Dispose();
			_stream = null;
		}

		public void WriteInterleaved(float[] data, int dataLength = -1)
		{
			if (dataLength < 0)
			{
				dataLength = data.Length;
			}
			if (_sampleFormat == SampleFormat.PCM16)
			{
				// Convert to s16le
				_byteCount = 0;
				for (int i = 0; i < dataLength; i++)
				{
					short shortVal = System.Convert.ToInt16(data[i] * 32767f);
					_outBytes[_byteCount + 0] = (byte)(shortVal & 0xff);
					_outBytes[_byteCount + 1] = (byte)((shortVal >> 8) & 0xff);
					_byteCount += 2;
				}
			}
			else
			{
				_byteCount = dataLength * sizeof(float);
				System.Buffer.BlockCopy(data, 0, _outBytes, 0, _byteCount);
			}

			// Write to stream
			if (_byteCount > 0)
			{
				_stream.Write(_outBytes, 0, _byteCount);
				_byteCountTotal += _byteCount;
				_byteCount = 0;
			}
		}

		public void WriteHeader(int byteStreamSize)
		{
			int bytesPerSample = (int)_sampleFormat;
			int byteRate = _sampleRate * _channelCount * bytesPerSample;
			int blockAlign = _channelCount * bytesPerSample;
			
			_stream.Write(RIFF_HEADER, 0, RIFF_HEADER.Length);
			_stream.Write(PackageInt(byteStreamSize + _headerSize, 4), 0, 4);
			
			_stream.Write(FORMAT_WAVE, 0, FORMAT_WAVE.Length);

			// 'fmt' chunk
			{
				_stream.Write(FORMAT_TAG, 0, FORMAT_TAG.Length);

				if (_sampleFormat == SampleFormat.PCM16)
				{
					_stream.Write(PackageInt(16, 4), 0, 4);
					_stream.Write(AUDIO_FORMAT_PCM, 0, AUDIO_FORMAT_PCM.Length);
				}
				else
				{
					_stream.Write(PackageInt(18, 4), 0, 4);
					_stream.Write(AUDIO_FORMAT_FLOAT, 0, AUDIO_FORMAT_FLOAT.Length);
				}
				_stream.Write(PackageInt(_channelCount, 2), 0, 2);
				_stream.Write(PackageInt(_sampleRate, 4), 0, 4);
				_stream.Write(PackageInt(byteRate, 4), 0, 4);
				_stream.Write(PackageInt(blockAlign, 2), 0, 2);
				_stream.Write(PackageInt(bytesPerSample * 8), 0, 2);
				if (_sampleFormat == SampleFormat.Float32)
				{
					_stream.Write(PackageInt(0, 2), 0, 2);	// Extension size
				}
			}
			
			if (_sampleFormat == SampleFormat.Float32)
			{
				// 'fact' chunk
				{
					_stream.Write(FACTCHUNK_ID, 0, FACTCHUNK_ID.Length);
					_stream.Write(PackageInt(4, 4), 0, 4);
					int samplesPerChannel = (byteStreamSize / bytesPerSample);
					_stream.Write(PackageInt(samplesPerChannel, 4), 0, 4);
				}
			}

			// 'data' chunk
			{
				_stream.Write(SUBCHUNK_ID, 0, SUBCHUNK_ID.Length);
				_stream.Write(PackageInt(byteStreamSize, 4), 0, 4);
			}

			_headerSize = (int)_stream.Position - 8;
			//UnityEngine.Debug.Log("Header size: " + _headerSize);
		}

		private static byte[] PackageInt(int source, int length = 2)
		{
			if((length!=2)&&(length!=4))
				throw new System.ArgumentException("length must be either 2 or 4", "length");
			var retVal = new byte[length];
			retVal[0] = (byte)(source & 0xFF);
			retVal[1] = (byte)((source >> 8) & 0xFF);
			if (length == 4)
			{
				retVal[2] = (byte) ((source >> 0x10) & 0xFF);
				retVal[3] = (byte) ((source >> 0x18) & 0xFF);
			}
			return retVal;
		}
	}
}