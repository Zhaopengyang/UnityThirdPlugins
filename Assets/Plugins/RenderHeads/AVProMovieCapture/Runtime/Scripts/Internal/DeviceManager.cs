using System.Collections;

//-----------------------------------------------------------------------------
// Copyright 2012-2022 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProMovieCapture
{
	public enum DeviceType
	{
		AudioInput,
	}

	public interface IMediaApiItem
	{
		int Index { get; }
		string Name { get; }
		MediaApi MediaApi { get; }
	}

	public class Device : IMediaApiItem
	{
		private DeviceType _deviceType;
		private int _index;
		private string _name;
		private MediaApi _api;

		public DeviceType DeviceType { get { return _deviceType; } }
		public int Index { get { return _index; } }
		public string Name { get { return _name; } }
		public MediaApi MediaApi { get { return _api; } }

		internal Device(DeviceType deviceType, int index, string name, MediaApi api)
		{
			_deviceType = deviceType;
			_index = index;
			_name = name;
			_api = api;
		}
	}

	public class DeviceList : IEnumerable
	{
		internal DeviceList(Device[] devices)
		{
			_devices = devices;
		}

		public Device FindDevice(string name, MediaApi mediaApi = MediaApi.Unknown)
		{
			Device result = null;
			foreach (Device device in _devices)
			{
				if (device.Name == name)
				{
					if (mediaApi == MediaApi.Unknown || mediaApi == device.MediaApi)
					{
						result = device;
						break;
					}
				}
			}
			return result;
		}

		public Device GetFirstWithMediaApi(MediaApi api)
		{
			Device result = null;
			foreach (Device device in _devices)
			{
				if (device.MediaApi == api)
				{
					result = device;
					break;
				}
			}
			return result;
		}

		public IEnumerator GetEnumerator()
		{
			return _devices.GetEnumerator();
		}

		public Device[] Devices { get { return _devices; } }
		public int Count { get{ return _devices.Length; } }

		private Device[] _devices = new Device[0];
	}

	public static class DeviceManager
	{
		public static Device FindDevice(DeviceType deviceType, string name)
		{
			CheckInit();
			Device result = null;
			DeviceList devices = GetDevices(deviceType);
			result = devices.FindDevice(name);
			return result;
		}

		public static int GetDeviceCount(DeviceType deviceType)
		{
			CheckInit();
			return GetDevices(deviceType).Count;
		}

		private static void CheckInit()
		{
			if (!_isEnumerated)
			{
				if (NativePlugin.Init())
				{
					EnumerateDevices();
				}
			}
		}

		private static DeviceList GetDevices(DeviceType deviceType)
		{
			DeviceList result = null;
			switch (deviceType)
			{
				case DeviceType.AudioInput:
					result = _audioInputDevices;
					break;
			}
			return result;
		}		

		private static void EnumerateDevices()
		{
			{
				Device[] audioInputDevices = new Device[NativePlugin.GetAudioInputDeviceCount()];
				for (int i = 0; i < audioInputDevices.Length; i++)
				{
					audioInputDevices[i] = new Device(DeviceType.AudioInput, i, NativePlugin.GetAudioInputDeviceName(i), NativePlugin.GetAudioInputDeviceMediaApi(i));
				}
				_audioInputDevices = new DeviceList(audioInputDevices);
			}

			_isEnumerated = true;
		}

		public static DeviceList AudioInputDevices { get { CheckInit(); return _audioInputDevices; } }

		private static bool _isEnumerated = false;

		private static DeviceList _audioInputDevices = new DeviceList(new Device[0]);
	}
}