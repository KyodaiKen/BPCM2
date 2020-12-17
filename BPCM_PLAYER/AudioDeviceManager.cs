using BPCM_PLAYER.Wave;
using NAudioLitle.Wave;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace BPCM_PLAYER
{
    public static class AudioDeviceManager
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct AudioDevice
        {
            public string Name;
            public Guid DeviceGUID;
        }

        public static List<AudioDevice> AudioDevices
        {
            get
            {
                var ads = new List<AudioDevice>();
                for (int x = 0; x < WaveOut.DeviceCount; x++)
                {
                    WaveOutCapabilities devcap = WaveOut.GetCapabilities(x);
                    ads.Add(new AudioDevice()
                    {
                        Name = devcap.ProductName,
                        DeviceGUID = devcap.ProductGuid
                    });
                }

                return ads;
            }
        }
    }
}
