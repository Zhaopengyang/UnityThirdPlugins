using System.Collections.Generic;
using System.Threading;
using UnityEngine;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	/// Allows the user to monitor a capture instance where the capture has completed,but the file is still being written to asynchronously
	public class FileWritingHandler : System.IDisposable
	{
		private string _path;
		private int _handle;
		private bool _deleteFile;
		private OutputTarget _outputTarget;
		private MP4FileProcessing.Options _postOptions;
		private ManualResetEvent _postProcessEvent;
		private CompletionStatus _completionStatus;

		public enum CompletionStatus
		{
			BusyFileWriting,
			BusyPostProcessing,
			CompletedDeleted,
			Completed,
		}

		public CompletionStatus Status
		{
			get { return _completionStatus; }
		}
		
		public string Path
		{
			get { return _path; }
		}

		// Register for notification of when the final file writing completes
		internal System.Action<FileWritingHandler> CompletedFileWritingAction { get; set; }

		internal FileWritingHandler(OutputTarget outputTarget, string path, int handle, bool deleteFile)
		{
			_outputTarget = outputTarget;
			_path = path;
			_handle = handle;
			_deleteFile = deleteFile;
			_completionStatus = CompletionStatus.BusyFileWriting;
		}

		internal void SetFilePostProcess(MP4FileProcessing.Options postOptions)
		{
			_postOptions = postOptions;
		}

		private bool StartPostProcess()
		{
			UnityEngine.Debug.Assert(_postProcessEvent == null);
			_completionStatus = CompletionStatus.BusyPostProcessing;
			_postProcessEvent = MP4FileProcessing.ProcessFileAsync(_path, false, _postOptions);
			if (_postProcessEvent == null)
			{
				UnityEngine.Debug.LogWarning("[AVProMovieCapture] failed to post-process file "  + _path);
			}
			return true;
		}

		public bool IsFileReady()
		{
			bool result = true;
			if (_handle >= 0)
			{
				result = NativePlugin.IsFileWritingComplete(_handle);
				if (result)
				{
					if (_postOptions.HasOptions())
					{
						result = StartPostProcess();
						_postOptions.ResetOptions();
					}
					if (_postProcessEvent != null)
					{
						result = _postProcessEvent.WaitOne(1);
					}
					if (result)
					{
						Dispose();
					}
				}
			}
			return result;
		}

		public void Dispose()
		{
			_postProcessEvent = null;

			if (_handle >= 0)
			{
				NativePlugin.FreeRecorder(_handle);
				_handle = -1;

				// Issue the free resources plugin event
				NativePlugin.RenderThreadEvent(NativePlugin.PluginEvent.FreeResources, -1);

				if (_deleteFile)
				{
					_completionStatus = CompletionStatus.CompletedDeleted;
					CaptureBase.DeleteCapture(_outputTarget, _path);
				}
				else
				{
					_completionStatus = CompletionStatus.Completed;
				}
			}

			if (CompletedFileWritingAction != null)
			{
				CompletedFileWritingAction.Invoke(this);
				CompletedFileWritingAction = null;
			}

			CaptureBase.ActiveFilePaths.Remove(_path);
		}

		// Helper method for cleaning up a list
		// TODO: add an optional System.Action callback for each time the file writer completes
		public static bool Cleanup(List<FileWritingHandler> list)
		{
			bool anyRemoved = false;
			// NOTE: We iterate in reverse order as we're removing elements from the list
			for (int i = list.Count - 1; i >= 0; i--)
			{
				FileWritingHandler handler = list[i];
				if (handler.IsFileReady())
				{
					list.RemoveAt(i);
					anyRemoved = true;
				}
			}
			return anyRemoved;
		}
	}
}