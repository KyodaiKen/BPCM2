using BPCM_CODEC;
using BPCM_PLAYER.Wave;
using NAudioLitle.Wave;
using System;
using System.IO;
using System.Threading;

namespace BPCM_PLAYER
{
    public class Player : IDisposable
    {
        private double _pos;

        private bool _playing;
        private bool _stopping;
        private bool _seeking;

        private FileStream _BPCMFile;
        private BitstreamReader _BPCMStream;
        private BPCMWaveProvider _BPCMWaveProvider;
        private WaveOutEvent _WaveOut;

        public enum PlaybackStoppedReason : byte
        {
            BufferEmpty = 0xFF
            , SomeError = 0xF7
            , NoMoreData = 0x01
            , StopCalled = 0x00
        }

        public struct ConfigurationBean
        {
            public int WaveOutDevice;
            public int WaveOutBufferSize;
            public int WaveOutBufferCount;
            public float Volume;
            public double PlaybackRate;
            public bool EnableDithering;
            public dgAnalysisUpdate AnalysisUpdateEvent;
            public dgFileOpened FileOpenedEvent;
        }

        public struct PlaybackUpdateInfo
        {
            public Frame CurrentFrame { get; set; }
            public double Volume { get; set; }
            public double PlaybackRate { get; set; }
        }

        private ConfigurationBean _config;

        public delegate void dgFileOpened(Decoder.Info info);
        public delegate void dgWaveOutInitialized(WaveOutCapabilities devcaps);
        public delegate void dgPlaybackStopped(PlaybackStoppedReason Reason);
        public delegate void dgPlaybackUpdate(PlaybackUpdateInfo Info);
        public delegate void dgAnalysisUpdate(float progress);

        public dgPlaybackStopped PlaybackStoppedEvent { get; set; }
        public dgPlaybackUpdate PlaybackUpdateEvent { get; set; }
        public dgWaveOutInitialized WaveOutInitializedEvent { get; set; }

        public double Duration
        {
            get
            {
                if (_BPCMStream != null)
                    return _BPCMStream.Analysis.Duration;
                else
                    return double.NaN;
            }
        }

        public double Position
        {
            get
            {
                return _pos;
            }

            set
            {
                __INTERNAL_Seek(value);
            }
        }

        public float Volume
        {
            get
            {
                return _config.Volume;
            }

            set
            {
                if (_WaveOut != null)
                {
                    float mvol = value;
                    if (mvol > 1) mvol = 1f;
                    if (mvol < 0) mvol = 0f;
                    _WaveOut.Volume = mvol;
                    _config.Volume = mvol;
                }
            }
        }

        public double PlaybackRate
        {
            get
            {
                return _config.PlaybackRate;
            }

            set
            {
                _config.PlaybackRate = value;
                __INTERNAL_ChangeRate();
            }
        }

        public bool Playing
        {
            get
            {
                return _playing;
            }

            set
            {
                if (_WaveOut != null)
                {
                    if (_playing == true && value == false)
                    {
                        _stopping = true;
                        _WaveOut.Stop();
                        _playing = false;
                    }
                    else if (_playing == false && value == true)
                    {
                        _stopping = false;
                        _WaveOut.Play();
                        _playing = true;
                    }
                }
            }
        }

        public Stats Stats
        {
            get
            {
                if (_BPCMStream != null)
                    return _BPCMStream.Analysis;
                else
                    return new Stats();
            }
        }

        private void __INTERNAL_WaveOutInit()
        {
            _BPCMWaveProvider = new BPCMWaveProvider(_BPCMStream, _config.PlaybackRate);
            _BPCMWaveProvider.volume = 1;
            _BPCMWaveProvider.readDone = __INTERNAL_UpdatePosition;
            _WaveOut = new WaveOutEvent();
            _WaveOut.DeviceNumber = _config.WaveOutDevice;
            _WaveOut.DesiredLatency = _config.WaveOutBufferSize * _config.WaveOutBufferCount;
            _WaveOut.NumberOfBuffers = _config.WaveOutBufferCount;
            _WaveOut.Volume = _config.Volume;
            _WaveOut.PlaybackStopped += __INTERNAL_PlaybackStopped;
            _WaveOut.Init(_BPCMWaveProvider);
            Volume = _config.Volume;
        }

        private void __INTERNAL_UpdatePosition(Frame frame)
        {
            //Set position property memory value
            _pos = frame.TimeStamp;

            //Invoke position change event
            PlaybackUpdateEvent?.Invoke(new PlaybackUpdateInfo()
            {
                CurrentFrame = frame,
                PlaybackRate = PlaybackRate,
                Volume = Volume
            });

            _stopping = false;
            _seeking = false;
        }

        private void __INTERNAL_Seek(double pos)
        {
            _seeking = true;
            _BPCMStream.Seek(pos);
            _BPCMWaveProvider.DropRingBuffer();
        }

        private void __INTERNAL_ChangeRate()
        {
            /*** Bugfix for random crashes and bitstream errors --> */
            _WaveOut.PlaybackStopped -= __INTERNAL_PlaybackStopped;
            //_WaveOut.Stop();
            /* <-- */
            _WaveOut.Dispose();
            _WaveOut = null;
            _BPCMWaveProvider = null;
            /*** Bugfix for random crashes and bitstream errors --> */
            //GC.Collect();

#warning async is a better thing to use for this (probably)
            Thread.Sleep(10); //Wait 10ms for things to settle
            _BPCMStream.Seek(_BPCMStream.FramesDecoded - 2); //Compensate jump
            /* <-- */
            __INTERNAL_WaveOutInit();
            _WaveOut.Play();
            _playing = true;
        }

        private void __INTERNAL_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            PlaybackStoppedReason rsn;
            if (!_seeking && !_stopping)
            {
                rsn = PlaybackStoppedReason.NoMoreData;
                Position = 0;
                _playing = false;
                __INTERNAL_UpdatePosition(_BPCMStream.Analysis.FrameSet[0]);
            }
            else if (!_stopping)
            {
                rsn = PlaybackStoppedReason.BufferEmpty;
            }
            else
            {
                rsn = PlaybackStoppedReason.StopCalled;
            }
            _stopping = true;
            if (e.Exception != null) rsn = PlaybackStoppedReason.SomeError;
            PlaybackStoppedEvent?.Invoke(rsn);
        }

        public Player(string bpcmFile, ConfigurationBean config)
        {
            //First of all check the configs and if in doubt, use default values
            if (config.WaveOutDevice > WaveOut.DeviceCount || config.WaveOutDevice < 0) config.WaveOutDevice = 0;
            if (config.WaveOutBufferCount < 2) config.WaveOutBufferCount = 2;
            if (config.WaveOutBufferSize == 0) config.WaveOutBufferSize = 100;
            if (config.Volume > 1) config.Volume = 1f;
            if (config.Volume < 0) config.Volume = 0f;
            if (config.PlaybackRate == 0) config.PlaybackRate = 1;
            _config = config;

            //Open the BPCM file
            _BPCMFile = new FileStream(bpcmFile, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, false);
            if (!(_config.AnalysisUpdateEvent is null))
            {
                void AnalysisUpdtEvt(float progress)
                {
                    _config.AnalysisUpdateEvent.Invoke(progress);
                }
                _BPCMStream = new BitstreamReader(_BPCMFile, aupevt: AnalysisUpdtEvt);
            }
            else
                _BPCMStream = new BitstreamReader(_BPCMFile);

            _BPCMStream.EnableDither = _config.EnableDithering;

            if (!(_config.FileOpenedEvent is null))
            {
                double duration = (double)_BPCMStream.Analysis.DurationSampleCount / _BPCMStream.Analysis.FrameSet[0].SamplingRate;
                TimeSpan dur = TimeSpan.FromSeconds(duration);
                string strDuration = string.Format("{0:00}d {1:00}h {2:00}m {3:00}s {4:000.000}ms", dur.Days, dur.Hours, dur.Minutes, dur.Seconds, (duration - Math.Floor(duration)) * 1000);

                _config.FileOpenedEvent.Invoke(new Decoder.Info()
                {
                    NumberOfChannels = _BPCMStream.Analysis.FrameSet[0].Channels
                    ,
                    SamplingRate = _BPCMStream.Analysis.FrameSet[0].SamplingRate
                    ,
                    Duration = _BPCMStream.Analysis.Duration
                    ,
                    DurationSampleCount = _BPCMStream.Analysis.DurationSampleCount
                    ,
                    DurationString = strDuration
                    ,
                    BitrateMin = _BPCMStream.Analysis.BitrateMinimum
                    ,
                    BitrateAvg = _BPCMStream.Analysis.BitrateAverage
                    ,
                    BitrateMax = _BPCMStream.Analysis.BitrateMaximum
                    ,
                    BlockSizeNominal = _BPCMStream.Analysis.BlockSizeNominal
                    ,
                    BlockSizeAverage = _BPCMStream.Analysis.BlockSizeAverage
                    ,
                    BlockSizeMaximum = _BPCMStream.Analysis.BlockSizeMaximum
                    ,
                    BlockSizeMinimum = _BPCMStream.Analysis.BlockSizeMinimum
                    ,
                    FrameSampleCountHistogram = _BPCMStream.Analysis.FrameSampleCountHistogram
                    ,
                    FrameCompressionHistogram = _BPCMStream.Analysis.CompressionHistogram
                    ,
                    FrameSet = _BPCMStream.Analysis.FrameSet
                    ,
                    CompressionUsed = _BPCMStream.Analysis.CompressionUsed
                    ,
                    CompressionUsedString = string.Join(", ", _BPCMStream.Analysis.CompressionUsed.ToArray())
                    ,
                    FileSize = _BPCMStream.BPCMStream.Length
                });
            }

            __INTERNAL_WaveOutInit();

            //Determine the used device and fire the init event once.
            WaveOutCapabilities devcaps = WaveOut.GetCapabilities(0);
            for (int x = 0; x < WaveOut.DeviceCount; x++) if (x == _WaveOut.DeviceNumber || x != 0) devcaps = WaveOut.GetCapabilities(x);
            WaveOutInitializedEvent?.Invoke(devcaps);
        }

        public void Play()
        {
            if (!Playing) Playing = true;
        }

        public void Stop()
        {
            if (Playing) Playing = false;
        }

        #region IDisposable Support

        private bool disposedValue = false;

        public void Dispose()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _WaveOut.Stop();
                    _WaveOut.Dispose();
                    _WaveOut = null;
                    _BPCMFile.Dispose();
                    _BPCMFile = null;
                }

                _BPCMStream = null;
                _BPCMWaveProvider = null;

                disposedValue = true;
            }
        }

        ~Player()
        {
            Dispose(false);
        }

        void IDisposable.Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion IDisposable Support
    }
}
