#define SUPPORT_SPHERICAL_VIDEO
#define SUPPORT_STEREO_VIDEO
//#define SUPPORT_DEBUGLOG
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.IO;
using System;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/*
		NOTE: 	For best flexibility this should be written using classes, but for now since we're only doing specific operations,
				we're just reading chunks on demand, writing them out in order making modifications as we go, keeping track of any
				increase chunk size increase and patching those chunks.  Most chunks are simply byte copied.
				
				This was mainly written this way because we assume either the moov atom is moved before mdat atom, or the moov atom size 
				changes due to injection and it's before mdat, both cases cannot be done in-place and require writing to a second file.
				This method doesn't work well for the case where the moov atom is after mdat, as this could be done in-place.  In the future
				a hierarchy of chunk classes should be used for processing the moov chunk to make the code more flexible and readable.

		NOTE:	Using the term Chunks to mean atoms/boxes

		Order of operations:
		1) The root chunks are read in
		2) Determined whether moov and mdat chunk positions need swapping, update mdatOffset (only if moov < mdat)
		3) Process the moov chunk is, writing out recursively
		4) For each moov/trak chunk:
			a) For each moov/trak/mdia/minf/stbl/stsd/(avc1|hev1|hvc1)
				i) inject visual sample descriptor extension atoms if needed
				ii) patch parent chunk sizes
				iii) update mdatOffset (only if moov < mdat)
			b) For each moov/trak/mdia/minf/stbl/co64 (only if mdatOffset > 0)
				i) write stub, add chunk to list to patch in step 5
			b) For each moov/trak/mdia/minf/stbl/stco (only if mdatOffset > 0)
				i) if adjusting largest offset with mdatOffset <= 0xffffffff:
					1) write out stub co64, add chunk to list to patch in step 5
					2) patch parent chunk sizes
				ii) if adjusting largest offset with mdatOffset < 0xffffffff:
					1) write stub, add chunk to list to patch in step 5
		(NOTE: We only adjust offsets later because there can be multiple tracks, so mdatOffset can still be changing, and also the audio track could come before the vidoe track which would mean moov size grown from visual injection wouldn't need to affect audio offsets)
		5) Go back over any saved stco/co64 chunks
			a) adjust offsets by mdatOffset (if needed)

		NOTE: It is SLIGHTLY possible that some files using 32-bit offsets (stco atom) can grow and require changing to 64-bit offsets (co64 atom), so we account for this.
		NOTE: Because we only have a single video track, after the visual sample descriptor atoms have been injected we're ready to determine whether stco's need to change to co64's, otherwise it would be more complicated
		NOTE: It's HIGHLY unlikely that any chunks within moov will get close to > 32bit, so we will ignore the edge case of the atom injection needing to adjust the atom size
		TODO: test with file just less than 4GB file, which grows to 4GB so can test the logic for 64-bit atom size values
	*/
	/// <summary>
	/// This class is used to rearrange the chunks in an MP4 file to allow 'fast start', 
	/// and also to inject various chunks related to stereo video mode and 360 video layout.
	/// </summary>
	// Reference: https://wiki.multimedia.cx/index.php/QuickTime_container
	// Reference: https://github.com/danielgtaylor/qtfaststart/blob/master/qtfaststart/processor.py
	// Reference: https://developer.apple.com/library/content/documentation/QuickTime/QTFF/QTFFPreface/qtffPreface.html
	// Reference: https://github.com/google/spatial-media/blob/master/docs/spherical-video-v2-rfc.md
	// Reference: https://xhelmboyx.tripod.com/formats/mp4-layout.txt
	public class MP4FileProcessing
	{
		public struct Options
		{
			public bool applyFastStart;

			#if SUPPORT_STEREO_VIDEO
			public bool applyStereoMode;
			public StereoPacking stereoMode;
			#endif

			#if SUPPORT_SPHERICAL_VIDEO
			public bool applySphericalVideoLayout;
			public SphericalVideoLayout sphericalVideoLayout;
			#endif

			public bool applyMoveCaptureFile;
			public string finalCaptureFilePath;

			public bool HasOptions()
			{
				return (RequiresProcessing() || applyFastStart);
			}

			public bool RequiresProcessing()
			{
				return (applyFastStart || applyStereoMode || applySphericalVideoLayout);
			}

			public void ResetOptions()
			{
				applyFastStart = false;
				applyStereoMode = false;
				applySphericalVideoLayout = false;

				applyMoveCaptureFile = false;
				finalCaptureFilePath = null;
			}
		}

		private const int ChunkHeaderSize = 8;
		private const int ExtendedChunkHeaderSize = 16;

		private const int CopyBufferSize = 4096 * 16;

		//private readonly static uint Atom_ftyp = ChunkId("ftyp");		// file type
		private readonly static uint Atom_moov = ChunkId("moov");		// movie header
		private readonly static uint Atom_mdat = ChunkId("mdat");		// movie data
		private readonly static uint Atom_cmov = ChunkId("cmov");		// compressed movie data

		private readonly static uint Atom_trak = ChunkId("trak");		// track header
		private readonly static uint Atom_mdia = ChunkId("mdia");		// media
		private readonly static uint Atom_hdlr = ChunkId("hdlr");		// handler reference
		private readonly static uint Atom_minf = ChunkId("minf");		// media information
		private readonly static uint Atom_stbl = ChunkId("stbl");		// sample table
		private readonly static uint Atom_stco = ChunkId("stco");		// sample table chunk offsets (32-bit)
		private readonly static uint Atom_co64 = ChunkId("co64");		// sample table chunk offsets (64-bit)

		#if SUPPORT_STEREO_VIDEO || SUPPORT_SPHERICAL_VIDEO
		private readonly static uint Atom_stsd = ChunkId("stsd");		// sample table sample description
		private readonly static uint Atom_avc1 = ChunkId("avc1");		// video sample entry for H.264
		private readonly static uint Atom_hev1 = ChunkId("hev1");		// video sample entry for H.265/HEVC
		private readonly static uint Atom_hvc1 = ChunkId("hvc1");		// video sample entry for H.265/HEVC
		#endif

		#if SUPPORT_STEREO_VIDEO
		private readonly static uint Atom_st3d = ChunkId("st3d");		// stereoscopic 3D video
		#endif

		#if SUPPORT_SPHERICAL_VIDEO
		private readonly static uint Atom_uuid = ChunkId("uuid");		// unique id
		private readonly static uint Atom_sv3d = ChunkId("sv3d");		// spherical video
		private readonly static uint Atom_svhd = ChunkId("svhd");		// spherical video header
		private readonly static uint Atom_proj = ChunkId("proj");		// projection
		private readonly static uint Atom_prhd = ChunkId("prhd");		// projection header
		private readonly static uint Atom_equi = ChunkId("equi");		// equirectangular projection
		#endif

		private class Chunk
		{
			public uint id;
			public long size;			// includes the size of the chunk header, so next chunk is at size+offset
			public long offset;         // offset to the start of the chunk header in the source file
			public long headerSize;		// Size of the header (either 8 or 12)
			public long writeOffset;	// offset to the start of the chunk header in the dest file (currently only used for replacing stco with co64)
		};

		private BinaryReader _reader;
		private Stream _writeFile;
		private Options _options;
		private bool _requires64BitOffsets;
		private List<Chunk> _offsetChunks = new List<Chunk>();				// stco / co64 chunks
		private List<Chunk> _offsetUpgradeChunks = new List<Chunk>();		// Chunks that were stco that changed to co64

		public static ManualResetEvent ProcessFileAsync(string filePath, bool keepBackup, Options options)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError("File not found: " + filePath);
				return null;
			}

			ManualResetEvent syncEvent = new ManualResetEvent(false);

			Thread thread = new Thread(
				() =>
				{
					try
					{
						ProcessFile(filePath, keepBackup, options);
					}
					catch (System.Exception e)
					{
						Debug.LogException(e);
					}
					syncEvent.Set();
				}
			);
			thread.Start();

			return syncEvent;
		}

		public static bool ProcessFile(string filePath, bool keepBackup, Options options)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError("File not found: " + filePath);
				return false;
			}

			bool result = true;

			if(options.RequiresProcessing())
			{
				string tempPath = filePath + "-" + System.Guid.NewGuid() + ".temp";
			
				result = ProcessFile(filePath, tempPath, options);
				if (result)
				{
					string backupPath = filePath + "-" + System.Guid.NewGuid() + ".backup";
					File.Move(filePath, backupPath);
					File.Move(tempPath, filePath);
					if (!keepBackup)
					{
						File.Delete(backupPath);
					}
				}

				if (File.Exists(tempPath))
				{
					File.Delete(tempPath);
				}
			}

			if(result)
			{
				// Move the captured video somewhere else?
				if(options.applyMoveCaptureFile && options.finalCaptureFilePath != null)
				{
					File.Move(filePath, options.finalCaptureFilePath);
				}
			}

			return result;
		}

		public static bool ProcessFile(string srcPath, string dstPath, Options options)
		{
			if (!File.Exists(srcPath))
			{
				Debug.LogError("File not found: " + srcPath);
				return false;
			}
			
			using (Stream srcStream = new FileStream(srcPath, FileMode.Open, FileAccess.Read, FileShare.Read))
			{
				using (Stream dstStream = new FileStream(dstPath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None))
				{
					MP4FileProcessing mp4 = new MP4FileProcessing(options);
					bool result = mp4.Process(srcStream, dstStream);
					mp4.Close();
					return result;
				}
			}
		}

		public MP4FileProcessing(Options options)
		{
			_options = options;
		}

		public bool Process(Stream srcStream, Stream dstStream)
		{
			Close();

			_reader = new BinaryReader(srcStream);

			List<Chunk> rootChunks = ReadChildChunks(null);

			Chunk chunk_moov = GetFirstChunkOfType(Atom_moov, rootChunks);
			Chunk chunk_mdat = GetFirstChunkOfType(Atom_mdat, rootChunks);

			if (chunk_moov == null || chunk_mdat == null)
			{
				Debug.LogError("can't find moov or mdat chunks");
				Close();
				return false;
			}

			if (ChunkContainsChildChunkWithId(chunk_moov, Atom_cmov))
			{
				Debug.LogError("moov chunk is compressed - unsupported");
				Close();
				return false;
			}

			uint mdatOffset = 0;

			if (_options.applyFastStart && chunk_moov.offset > chunk_mdat.offset)
			{
				// Swap moov and mdat order
				int index_moov = rootChunks.IndexOf(chunk_moov);
				int index_mdat = rootChunks.IndexOf(chunk_mdat);

				rootChunks[index_mdat] = chunk_moov;
				rootChunks[index_moov] = chunk_mdat;

				mdatOffset = (uint)chunk_moov.size;
			}

			bool is_moov_before_mdat = (rootChunks.IndexOf(chunk_moov) < rootChunks.IndexOf(chunk_mdat));

			_writeFile = dstStream;

			// Make an approximate worst case calculation of whether 64-bit offsets are required do to
			// possible moov size growth.  In this case all stco chunks are rewritten as co64 chunks.
			// Our injected chunks are tiny, so just add 1024 as a safe approximation.
			_requires64BitOffsets = ((srcStream.Length + 1024) > 0xffffffff);

			// Copy and inject chunks
			foreach (Chunk chunk in rootChunks)
			{
				if (chunk != chunk_moov)
				{
					WriteChunk(chunk);
				}
				else
				{
					uint sizeIncrease = WriteChunkRecursive_moov(chunk_moov);
					if (is_moov_before_mdat)
					{
						// Only if moov is before mdat does moov size increase affect offsets
						mdatOffset += sizeIncrease;
					}
				}
			}

			DebugLog("total offset: " + mdatOffset);

			// Write and adjust offsets
			{
				foreach (Chunk chunk in _offsetChunks)
				{
					_writeFile.Position = chunk.writeOffset;
					if (chunk.id == Atom_stco)
					{
						WriteChunk_stco(chunk, mdatOffset);
					}
					else if (chunk.id == Atom_co64)
					{
						WriteChunk_co64(chunk, mdatOffset);
					}
				}
				foreach (Chunk chunk in _offsetUpgradeChunks)
				{
					_writeFile.Position = chunk.writeOffset;
					DebugLog("write offset: " + chunk.writeOffset);
					WriteChunk_co64_from_stco(chunk, mdatOffset);
				}
			}

			Close();

			Debug.Log("[AVProMovieCapture] File processing complete");

			return true;
		}

		public void Close()
		{
			_offsetChunks = new List<Chunk>();
			_offsetUpgradeChunks = new List<Chunk>();
			_writeFile = null;
			if (_reader != null)
			{
				_reader.Close();
				_reader = null;
			}
		}

		private static Chunk GetFirstChunkOfType(uint id, List<Chunk> chunks)
		{
			Chunk result = null;
			foreach (Chunk chunk in chunks)
			{
				if (chunk.id == id)
				{
					result = chunk;
					break;
				}
			}
			return result;
		}

		private List<Chunk> ReadChildChunks(Chunk parentChunk)
		{
			// Offset to start of parent chunk
			{
				long fileOffset = 0;
				if (parentChunk != null)
				{
					fileOffset = parentChunk.offset + parentChunk.headerSize;
				}
				_reader.BaseStream.Seek(fileOffset, SeekOrigin.Begin);
			}

			long chunkEnd = _reader.BaseStream.Length;
			if (parentChunk != null)
			{
				chunkEnd = parentChunk.offset + parentChunk.size;
			}

			return ReadChildChunks(chunkEnd);
		}

		private List<Chunk> ReadChildChunks(long chunkEndPosition)
		{
			List<Chunk> result = new List<Chunk>();
			if (_reader.BaseStream.Position < chunkEndPosition)
			{
				Chunk chunk = ReadChunkHeader();
				while (chunk != null && _reader.BaseStream.Position < chunkEndPosition)
				{
					result.Add(chunk);
					_reader.BaseStream.Seek(chunk.offset + chunk.size, SeekOrigin.Begin);
					chunk = ReadChunkHeader();
				}
			}
			return result;
		}

		private Chunk ReadChunkHeader()
		{
			Chunk chunk = null;

			// Make sure the minimum amount of data is available
			if ((_reader.BaseStream.Length - _reader.BaseStream.Position) >= ChunkHeaderSize)
			{
				chunk = new Chunk();
				chunk.offset = _reader.BaseStream.Position;
				chunk.headerSize = ChunkHeaderSize;
				chunk.size = ReadUInt32();
				chunk.id = _reader.ReadUInt32();
				
				if (chunk.size == 1)
				{
					// NOTE: '1' indicates we need to read the extended 64-bit size
					chunk.size = (long)ReadUInt64();
					chunk.headerSize = ExtendedChunkHeaderSize;
				}
				if (chunk.size == 0)
				{
					// NOTE: '0' indicates that this is the last chunk, so the size is the remainder of the file
					chunk.size = _reader.BaseStream.Length - chunk.offset;
				}
			}

			return chunk;
		}

		private bool ChunkContainsChildChunkWithId(Chunk chunk, uint id)
		{
			bool result = false;
			long endChunkPos = chunk.size + chunk.offset;
			_reader.BaseStream.Seek(chunk.offset, SeekOrigin.Begin);
			Chunk childChunk = ReadChunkHeader();
			while (childChunk != null && _reader.BaseStream.Position < endChunkPos)
			{
				if (childChunk.id == id)
				{
					result = true;
					break;
				}

				_reader.BaseStream.Seek(childChunk.offset + childChunk.size, SeekOrigin.Begin);
				childChunk = ReadChunkHeader();
			}
			return result;
		}

		private static string ChunkDesc(Chunk chunk)
		{
			string size = chunk.size + ((chunk.size > UInt32.MaxValue) ? "^" : "");
			string offset = chunk.offset + ((chunk.offset > UInt32.MaxValue) ? "^" : "");
			string end = chunk.size + chunk.offset + (((chunk.size + chunk.offset) > UInt32.MaxValue) ? "^" : "");
			string woffset = chunk.writeOffset + ((chunk.writeOffset > UInt32.MaxValue) ? "^" : "");
			return "<color=green><b>" + ChunkIdToString(chunk.id) + "</b></color> size:" + size + " offset:" + offset + " end:" + end + " write:" + woffset;
		}

		private void WriteChunk(Chunk chunk)
		{
			DebugLog("WriteChunk " + ChunkDesc(chunk));
			//if (chunk.id == Atom_mdat) return;
			_reader.BaseStream.Seek(chunk.offset, SeekOrigin.Begin);
			CopyBytes(chunk.size);
		}

		private void CopyChunkHeader(Chunk chunk)
		{
			DebugLog("CopyChunkHeader " + ChunkDesc(chunk));
			_reader.BaseStream.Seek(chunk.offset, SeekOrigin.Begin);
			CopyBytes(chunk.headerSize);
		}

		private void InjectChunkHeader(Chunk chunk)
		{
			DebugLog("InjectChunkHeader " + ChunkDesc(chunk));
			if (chunk.size < UInt32.MaxValue)
			{
				WriteUInt32((uint)chunk.size);
			}
			else
			{
				WriteUInt32(1);
			}
			WriteChunkId(chunk.id);
			if (chunk.size >= UInt32.MaxValue)
			{
				WriteUInt64((ulong)chunk.size);
			}
		}

		private void CopyBytes(long numBytes)
		{
			DebugLog(string.Format("Copying {0} bytes from {1} to {2}", numBytes, _reader.BaseStream.Position, _writeFile.Position));
			byte[] buffer = new byte[CopyBufferSize];
			long remaining = numBytes;
			Stream readStream = _reader.BaseStream;
			while (remaining > 0)
			{
				int byteCount = buffer.Length;
				if (remaining < buffer.Length)
				{
					byteCount = (int)remaining;
				}
				readStream.Read(buffer, 0, byteCount);
				_writeFile.Write(buffer, 0, byteCount);
				remaining -= byteCount;
			}
		}

		private void WriteZeros(long numBytes)
		{
			DebugLog(string.Format("Writing zero {0} bytes to {1}", numBytes, _writeFile.Position));

			byte[] buffer = new byte[CopyBufferSize];
			long remaining = numBytes;
			while (remaining > 0)
			{
				int byteCount = buffer.Length;
				if (remaining < buffer.Length)
				{
					byteCount = (int)remaining;
				}
				_writeFile.Write(buffer, 0, byteCount);
				remaining -= byteCount;
			}
		}

		private uint WriteChunkRecursive_moov(Chunk parentChunk)
		{
			uint childChunkSizeIncrease = 0;
			long chunkWritePosition = _writeFile.Position;
			CopyChunkHeader(parentChunk);
			DebugLog("write chunk " + ChunkIdToString(parentChunk.id) + " " + parentChunk.size);

			List<Chunk> children = ReadChildChunks(parentChunk);

			_reader.BaseStream.Seek(parentChunk.offset + parentChunk.headerSize, SeekOrigin.Begin);

			foreach (Chunk chunk in children)
			{
				if (chunk.id == Atom_stco)
				{
					DebugLog("stco " + ChunkDesc(chunk));
					// Just write a placeholder as it's updated later
					// May also convert the stco into a co64
					chunk.writeOffset = _writeFile.Position;
					if (!_requires64BitOffsets)
					{
						WriteZeros(chunk.size);
						_offsetChunks.Add(chunk);
					}
					else
					{
						childChunkSizeIncrease += InjectChunkStub_co64_from_stco(chunk);
						DebugLog("Increase InjectChunkStub_co64_from_stco " + childChunkSizeIncrease);
						_offsetUpgradeChunks.Add(chunk);
					}
				}
				else if (chunk.id == Atom_co64)
				{
					// Just write a placeholder as it's updated later
					chunk.writeOffset = _writeFile.Position;
					WriteZeros(chunk.size);
					_offsetChunks.Add(chunk);
				}
				#if SUPPORT_STEREO_VIDEO || SUPPORT_SPHERICAL_VIDEO
				else if (chunk.id == Atom_stsd)
				{
					childChunkSizeIncrease += WriteChunk_stsd(chunk);
					DebugLog("Increase WriteChunk_stsd " + childChunkSizeIncrease);
				}
				#endif
				// Hierarchy of atoms we're interested in:
				// [moov > trak > mdia > minf > stbl] >> [stco | co64]
				// [moov > trak > mdia > minf > stbl >> stsd] >> [avc1 | hev1 | hvc1]
				else if (chunk.id == Atom_trak ||
						chunk.id == Atom_mdia ||
						chunk.id == Atom_minf ||
						chunk.id == Atom_stbl)
				{
					// Recurse these chunks searching for interesting chunks
					childChunkSizeIncrease += WriteChunkRecursive_moov(chunk);
					DebugLog("Increase WriteChunkRecursive_moov " + childChunkSizeIncrease);

				}
				else
				{
					// We don't care about this chunk so just copy it
					WriteChunk(chunk);
				}
			}

			if (parentChunk.id == Atom_trak && _options.applySphericalVideoLayout && _options.sphericalVideoLayout == SphericalVideoLayout.Equirectangular360)
			{
				if (IsVideoTrack(parentChunk))
				{
					childChunkSizeIncrease += InjectChunk_uuid_GoogleSphericalVideoV1();
					DebugLog("Increase InjectChunk_uuid_GoogleSphericalVideoV1 " + childChunkSizeIncrease);
				}
			}

			if (childChunkSizeIncrease > 0)
			{
				DebugLog("> " + childChunkSizeIncrease);
				parentChunk.size += childChunkSizeIncrease;
				OverwriteChunkSize(parentChunk, chunkWritePosition);
			}

			return childChunkSizeIncrease;
		}

		private bool IsVideoTrack(Chunk trackChunk)
		{
			bool result = false;

			List<Chunk> chunks = ReadChildChunks(trackChunk);
			Chunk chunk_mdia = GetFirstChunkOfType(Atom_mdia, chunks);
			if (chunk_mdia != null)
			{
				chunks = ReadChildChunks(chunk_mdia);
				Chunk chunk_hdlr = GetFirstChunkOfType(Atom_hdlr, chunks);
				if (chunk_hdlr != null)
				{
					_reader.BaseStream.Position = chunk_hdlr.offset + chunk_hdlr.headerSize + 8;

					uint componentSubtype = ReadUInt32();
					result = (0x76696465 == componentSubtype);
				}
			}

			return result;
		}

		private void WriteChunk_stco(Chunk chunk, uint mdatByteOffset)
		{
			DebugLog("WriteChunk_stco");
			CopyChunkHeader(chunk);

			// Version & Flags
			CopyBytes(4);

			uint chunkOffsetCount = ReadUInt32();
			WriteUInt32(chunkOffsetCount);

			// Apply offsets
			for (int i = 0; i < chunkOffsetCount; i++)
			{
				long offset = ReadUInt32();
				offset += mdatByteOffset;
				WriteUInt32((uint)offset);
			}
		}

		private void WriteChunk_co64_from_stco(Chunk chunk, uint mdatByteOffset)
		{
			DebugLog("WriteChunk_co64_from_stco " + mdatByteOffset);
			InjectChunkHeader(chunk);
			_reader.BaseStream.Position = chunk.offset + chunk.headerSize;

			// Version & Flags
			CopyBytes(4);

			uint chunkOffsetCount = ReadUInt32();
			DebugLog("offsets: " + chunkOffsetCount);
			WriteUInt32(chunkOffsetCount);

			// Apply offsets
			for (int i = 0; i < chunkOffsetCount; i++)
			{
				ulong offset = ReadUInt32();
				offset += mdatByteOffset;
				WriteUInt64(offset);
			}
		}

		private void WriteChunk_co64(Chunk chunk, uint mdatByteOffset)
		{
			DebugLog("WriteChunk_co64");
			CopyChunkHeader(chunk);

			// Version & Flags
			CopyBytes(4);

			uint chunkOffsetCount = ReadUInt32();
			WriteUInt32(chunkOffsetCount);

			// Apply offsets
			for (int i = 0; i < chunkOffsetCount; i++)
			{
				ulong offset = ReadUInt64();
				offset += mdatByteOffset;
				WriteUInt64(offset);
			}
		}

		private uint InjectChunkStub_co64_from_stco(Chunk chunk)
		{
			chunk.id = Atom_co64;
			chunk.writeOffset = _writeFile.Position;
			CopyChunkHeader(chunk);

			// Version & Flags
			CopyBytes(4);
			
			// Stub count
			uint chunkOffsetCount = ReadUInt32();
			WriteUInt32(chunkOffsetCount);

			// Stub offsets
			long offsetsSize = chunkOffsetCount * sizeof(UInt64);
			WriteZeros(offsetsSize);

			long sizeIncrease = (offsetsSize / 2);

			// Calculate new size
			chunk.size += sizeIncrease;

			OverwriteChunkSize(chunk, chunk.writeOffset);

			return (uint)(sizeIncrease);
		}

#if SUPPORT_STEREO_VIDEO || SUPPORT_SPHERICAL_VIDEO
		private uint WriteChunk_stsd(Chunk chunk)
		{
			uint chunkSizeIncrease = 0;
			long chunkWritePosition = _writeFile.Position;

			CopyChunkHeader(chunk);

			// Version & Flags
			CopyBytes(4);

			uint sampleDescCount = ReadUInt32();
			WriteUInt32(sampleDescCount);

			for (int i = 0; i < sampleDescCount; i++)
			{
				Chunk sampleDescriptor = ReadChunkHeader();
				DebugLog("header: " + ChunkIdToString(sampleDescriptor.id) + " " +  sampleDescriptor.size);
				if (sampleDescriptor.id == Atom_avc1 ||
					sampleDescriptor.id == Atom_hev1 ||
					sampleDescriptor.id == Atom_hvc1)
				{
#if true
					_reader.BaseStream.Seek(4 + 6 + 2, SeekOrigin.Current);
					ushort version = ReadUInt16();
					DebugLog("version: " + version);
					if (version == 0)
					{
						uint sampleDescriptorSizeIncrease = 0;
						long sampleDescriptorWritePosition = _writeFile.Position;
						CopyChunkHeader(sampleDescriptor);

						CopyBytes(78);

						long chunkEndPosition = sampleDescriptor.offset + sampleDescriptor.size;

						List<Chunk> sampleDescriptorExtensions = ReadChildChunks(chunkEndPosition);
						DebugLog("sampleDescriptorExtensions: " + sampleDescriptorExtensions.Count);

						bool hasWrittenST3D = false;
						bool hasWrittenSV3D = false;
						for (int j = 0; j < sampleDescriptorExtensions.Count; j++)
						{
							DebugLog("sampleDescriptorExtensions: " + ChunkIdToString(sampleDescriptorExtensions[j].id) + " > " + sampleDescriptorExtensions[j].size);
							if (sampleDescriptorExtensions[i].id == Atom_st3d)
							{
								/*
								// Modify existing chunk
								if (_options.applyStereoMode)
								{
									InjectChunk_st3d(Convert(_options.stereoMode));
								}*/
								Debug.LogWarning("st3d atom already exists");
								hasWrittenST3D = true;
							}
							else if (sampleDescriptorExtensions[i].id == Atom_sv3d)
							{
								Debug.LogWarning("sv3d atom already exists");
								hasWrittenSV3D = true;
							}
							else
							{
								WriteChunk(sampleDescriptorExtensions[j]);
							}
						}

						#if SUPPORT_STEREO_VIDEO
						if (!hasWrittenST3D && _options.applyStereoMode)
						{
							sampleDescriptorSizeIncrease += InjectChunk_st3d(Convert(_options.stereoMode));
	
							hasWrittenST3D = true;
						}
						#endif
						#if SUPPORT_SPHERICAL_VIDEO
						if (!hasWrittenSV3D && _options.applySphericalVideoLayout)
						{
							sampleDescriptorSizeIncrease += InjectChunk_sv3d(_options.sphericalVideoLayout);
							hasWrittenSV3D = true;
						}
						#endif

						if (sampleDescriptorSizeIncrease > 0)
						{
							sampleDescriptor.size += sampleDescriptorSizeIncrease;
							OverwriteChunkSize(sampleDescriptor, sampleDescriptorWritePosition);

							chunkSizeIncrease += sampleDescriptorSizeIncrease;
							DebugLog("Increasing size by " + sampleDescriptorSizeIncrease);
						}

					}
					else
#endif
					{
						WriteChunk(sampleDescriptor);
					}
				}
				else
				{
					// We don't care about this chunk so just copy it
					WriteChunk(sampleDescriptor);
				}
			}
			DebugLog(chunk.offset + chunk.size + " left " + _reader.BaseStream.Position);

			if (chunkSizeIncrease > 0)
			{
				chunk.size += chunkSizeIncrease;
				OverwriteChunkSize(chunk, chunkWritePosition);
			}

			return chunkSizeIncrease;
		}
#endif

#if SUPPORT_STEREO_VIDEO
		internal enum StereoMode_st3d
		{
			Monoscopic = 0,
			Stereoscopic_TopBottom = 1,
			Stereoscopic_LeftRight = 2,
			Stereoscopic_Custom = 3,
			Stereoscopic_RightLeft = 4,
		}

		private static StereoMode_st3d Convert(StereoPacking mode)
		{
			StereoMode_st3d result = StereoMode_st3d.Monoscopic;
			switch (mode)
			{
				case StereoPacking.None:
					break;
				case StereoPacking.LeftRight:
					result = StereoMode_st3d.Stereoscopic_LeftRight;
					break;
				case StereoPacking.TopBottom:
					result = StereoMode_st3d.Stereoscopic_TopBottom;
					break;
			}
			return result;
		}

		private uint InjectChunk_st3d(StereoMode_st3d stereoMode)
		{
			DebugLog("InjectChunk_st3d");
			uint chunkSize = ChunkHeaderSize + 4 + sizeof(byte);
			WriteUInt32(chunkSize);
			WriteChunkId(Atom_st3d);

			// Version & Flags
			WriteUInt32(0);

			_writeFile.WriteByte((byte)stereoMode);
			return chunkSize;
		}
#endif

#if SUPPORT_SPHERICAL_VIDEO
		// sv3d/svhd
		// sv3d/proj/prhd
		// sv3d/proj/equi
		private uint InjectChunk_sv3d(SphericalVideoLayout layout)
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_sv3d;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);

			chunk.size += InjectChunk_svhd("AVProMovieCapture");
			chunk.size += InjectChunk_proj(layout);

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}

		private uint InjectChunk_uuid_GoogleSphericalVideoV1()
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_uuid;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);

			WriteUInt32(0xffcc8263);
			WriteUInt32(0xf8554a93);
			WriteUInt32(0x8814587a);
			WriteUInt32(0x02521fdd);

			chunk.size += 4 * sizeof(UInt32);

			string xml = "<rdf:SphericalVideo xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\" xmlns:GSpherical=\"http://ns.google.com/videos/1.0/spherical/\"> <GSpherical:Spherical>true</GSpherical:Spherical> <GSpherical:Stitched>true</GSpherical:Stitched> <GSpherical:ProjectionType>equirectangular</GSpherical:ProjectionType> <GSpherical:StitchingSoftware>AVPro Movie Capture</GSpherical:StitchingSoftware> <GSpherical:StereoMode>{StereoMode}</GSpherical:StereoMode></rdf:SphericalVideo>";

			if (_options.applyStereoMode)
			{
				switch (_options.stereoMode)
				{
					case StereoPacking.None:
					xml = xml.Replace("{StereoMode}", "mono");
					break;
					case StereoPacking.LeftRight:
					xml = xml.Replace("{StereoMode}", "left-right");
					break;
					case StereoPacking.TopBottom:
					xml = xml.Replace("{StereoMode}", "top-bottom");
					break;
				}
			}
			else
			{
				xml = xml.Replace("{StereoMode}", "mono");
			}

			byte[] bytes = System.Text.Encoding.UTF8.GetBytes(xml);
			_writeFile.Write(bytes, 0, bytes.Length);

			chunk.size += bytes.Length;

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}

		private uint InjectChunk_svhd(string toolname)
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_svhd;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);

			// Version & Flags
			WriteUInt32(0);
			chunk.size += 4;

			foreach (char c in toolname)
			{
				_writeFile.WriteByte((byte)c);
			}
			_writeFile.WriteByte(0);	// Null terminate
			chunk.size += (sizeof(byte) * (toolname.Length + 1));

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}

		private uint InjectChunk_proj(SphericalVideoLayout layout)
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_proj;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);

			chunk.size += InjectChunk_prhd();
			if (layout == SphericalVideoLayout.Equirectangular360)
			{
				chunk.size += InjectChunk_equi();
			}
			// TODO: add cubemap32 support here

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}

		private uint InjectChunk_prhd()
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_prhd;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);

			WriteUInt32(0);		// Version & Flags

			WriteUInt32(0);		// Yaw
			WriteUInt32(0);		// Pitch
			WriteUInt32(0);		// Roll

			chunk.size += sizeof(UInt32) * 4;

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}

		private uint InjectChunk_equi()
		{
			Chunk chunk = new Chunk();
			chunk.offset = _writeFile.Position;
			chunk.id = Atom_equi;
			chunk.size = ChunkHeaderSize;
			InjectChunkHeader(chunk);
			
			WriteUInt32(0);		// Version & Flags

			WriteUInt32(0);		// Bounds top
			WriteUInt32(0);		// Bounds bottom
			WriteUInt32(0);		// Bounds left
			WriteUInt32(0);		// Bounds right

			chunk.size += sizeof(UInt32) * 5;

			OverwriteChunkSize(chunk, chunk.offset);

			return (uint)chunk.size;
		}
#endif
		private void OverwriteChunkSize(Chunk chunk, long writePosition)
		{
			long restoreWritePosition = _writeFile.Position;

			_writeFile.Position = writePosition;
			DebugLog("patch size " + ChunkIdToString(chunk.id) + " " + chunk.size + "@ " + _writeFile.Position);
			// TODO: Fix bug here if original size was < 32bit but the new size is more
			// This is HIGHLY unlikely though, as moov chunks should be nowhere near that large
			// and we aren't adjusting the size of mdat chunks
			WriteUInt32((uint)chunk.size);

			_writeFile.Seek(restoreWritePosition, SeekOrigin.Begin);
		}

		private UInt16 ReadUInt16()
		{
			byte[] data = _reader.ReadBytes(2);
			Array.Reverse(data);
			return BitConverter.ToUInt16(data, 0);
		}

		private UInt32 ReadUInt32()
		{
			byte[] data = _reader.ReadBytes(4);
			Array.Reverse(data);
			return BitConverter.ToUInt32(data, 0);
		}

		private UInt64 ReadUInt64()
		{
			byte[] data = _reader.ReadBytes(8);
			Array.Reverse(data);
			return BitConverter.ToUInt64(data, 0);
		}

		private void WriteUInt16(UInt16 value)
		{
			byte[] data = BitConverter.GetBytes(value);
			Array.Reverse(data);
			_writeFile.Write(data, 0, data.Length);
		}

		private void WriteChunkId(uint id)
		{
			WriteUInt32(id, false);
		}

		private void WriteUInt32(UInt32 value, bool isBigEndian = true)
		{
			byte[] data = BitConverter.GetBytes(value);
			if (isBigEndian)
			{
				Array.Reverse(data);
			}
			_writeFile.Write(data, 0, data.Length);
		}

		private void WriteUInt64(UInt64 value)
		{
			byte[] data = BitConverter.GetBytes(value);
			Array.Reverse(data);
			_writeFile.Write(data, 0, data.Length);
		}

		private static string ChunkIdToString(UInt32 id)
		{
			char a = (char)((id >> 0) & 255);
			char b = (char)((id >> 8) & 255);
			char c = (char)((id >> 16) & 255);
			char d = (char)((id >> 24) & 255);
			return string.Format("{0}{1}{2}{3}", a, b, c, d);
		}

		private static uint ChunkId(string id)
		{
			uint a = id[3];
			uint b = id[2];
			uint c = id[1];
			uint d = id[0];
			return (a << 24) | (b << 16) | (c << 8) | d;
		}

		[System.Diagnostics.Conditional("SUPPORT_DEBUGLOG")]
		private static void DebugLog(string message)
		{
			Debug.Log(message);
		}

#if false && UNITY_EDITOR
		[UnityEditor.MenuItem("RenderHeads/Test MP4 Processing")]
		static void Test_Mp4Processing()
		{
			string path = "d:/video/video.mov";
			DateTime time = DateTime.Now;
			Options options = new Options();
			options.applyFastStart = true;
			options.applyStereoMode = true;
			options.stereoMode = StereoPacking.TopBottom;
			options.applySphericalVideoLayout = true;
			options.sphericalVideoLayout = SphericalVideoLayout.Equirectangular360;
			if (MP4FileProcessing.ProcessFile(path, true, options))
			{
				DateTime time2 = DateTime.Now;
				Debug.Log("success!");
				Debug.Log("Took: " + (time2 - time).TotalMilliseconds + "ms");
			}
			else
			{
				Debug.LogWarning("Did not modify file");
			}
		}
#endif
	}
}