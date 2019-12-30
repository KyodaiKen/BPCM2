using BPCM;
using BPCM.CompressionHelper;
using BPCM.Easy;
using BPCM.Wave;
using NAudioLitle.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace BPCM_CLI
{
    public static class Program
    {
        private static int PrintUsage()
        {
            Console.WriteLine("");
            Console.WriteLine(String.Concat(Enumerable.Repeat("-", 24)) + " Usage " + String.Concat(Enumerable.Repeat("-", 24)));
            Console.WriteLine("Encode:   bpcm input.wav output.bpcm");
            Console.WriteLine("Decode:   bpcm input.bpcm output.wav");
            Console.WriteLine("Playback: bpcm input.bpcm");
            Console.WriteLine("You can place your option parameters anywhere you like!");
            Console.WriteLine("");
            Console.WriteLine("== Encoding options ==");
            Console.WriteLine("-c     string    Use specific compression method");
            Console.WriteLine("                   ► none");
            Console.WriteLine("                   ► ac (Arithmetic order0)");
            Console.WriteLine("                   ► BZIP2");
            Console.WriteLine("                   ► LZMA");
            Console.WriteLine("                   ► fast  (use best result of LZMA and ac [DEFAULT] [RECOMMENDED!]),");
            Console.WriteLine("                   ► brute (use best result of all)");
            Console.WriteLine("-bs    number    Sets the block size in milliseconds, default is 100 ms");
            Console.WriteLine("");
            Console.WriteLine("== Player options ==");
            Console.WriteLine("-vol   decimal   Sets the initial playback volume from 0 ... 1 (float). Default is 0.12.");
            Console.WriteLine("-r     decimal   Sets the initial playback speed as a factor value (float). Default is 1.");
            Console.WriteLine("-od    number    Audio output device (0 = system default)");
            for (int x = 0; x < WaveOut.DeviceCount; x++) Console.WriteLine("                   ► " + x + ": " + WaveOut.GetCapabilities(x).ProductName);
            Console.WriteLine("");
            Console.WriteLine("== Tools ==");
            Console.WriteLine("-a               Analyzes a bpcm2 file and outputs a JSON to the console.");
            Console.WriteLine("");
            Console.WriteLine("== Tewaks ==");
            Console.WriteLine("= Encoding =");
            Console.WriteLine("-sltrh number    Sets the silence threshold from 0 ... 32747 (integer). Default is 4");
            Console.WriteLine("= Decoding / Playback =");
            Console.WriteLine("-dither          Enables random dithering on decoding, makes decoding slower and produces");
            Console.WriteLine("                 a different decoding result on each run.");
            return 0x7F;
        }

        private static int Main(string[] args)
        {
            Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-us");
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-us");
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.TreatControlCAsInput = true;

            string infile = string.Empty, outfile = string.Empty;
            int blockSize = 100;
            short SilenceThreshold = 4;
            bool enableDithering = false;
            bool enc = false;

            int oldCurTop = 0;
            string titleAndVersionInfo = "";

            Algorithm compression = Algorithm.fast;

            #region Parse Parameters

            //Check for parameter for the analysis and output a JSON string into the console
            int i;
            bool analyze = false;
            for (i = 0; i < args.Length; i++) if (args[i] == "-ajson" || args[i] == "-a") analyze = true;

            if (!analyze)
            {
                Console.CursorVisible = true;
                Console.CursorSize = 100;
                Console.Title = "BPCM";

                Assembly assembly = Assembly.GetExecutingAssembly();
                var descriptionAttribute = assembly
                    .GetCustomAttributes(typeof(AssemblyDescriptionAttribute), false)
                    .OfType<AssemblyDescriptionAttribute>()
                    .FirstOrDefault();

                string AssemblyName = assembly.GetName().Name.ToUpper();
                string AssemblyVersion = assembly.GetName().Version.ToString();

                titleAndVersionInfo = $"{AssemblyName}: {descriptionAttribute.Description} version {AssemblyVersion} \"Feline\"";

                Console.WriteLine("");
                oldCurTop = Console.CursorTop;
                Console.WriteLine(titleAndVersionInfo);
                Console.WriteLine(string.Concat(Enumerable.Repeat("\x2550", 80)));
                if (args?.Length == 0) return PrintUsage();
            }

            //Check for input file and then check the type
            //BPCM (decode)
            int bpcm = int.MaxValue;
            int wave = int.MaxValue;
            for (i = 0; i < args.Length; i++)
            {
                if (File.Exists(args[i]) && (Path.GetExtension(args[i]).ToLower() == ".bpcm" || Path.GetExtension(args[i]).ToLower() == ".bpcm2"))
                {
                    enc = false;
                    bpcm = i;
                    infile = args[i];
                }
            }

            //WAVE (encode)
            for (i = 0; i < args.Length; i++)
            {
                if (File.Exists(args[i]) && Path.GetExtension(args[i]).ToLower() == ".wav" && i < bpcm)
                {
                    enc = true;
                    wave = i;
                    infile = args[i];
                }
            }

            for (i = 0; i < args.Length; i++)
            {
                //Check for output file
                if ((Path.GetExtension(args[i]) == ".bpcm" || Path.GetExtension(args[i]).ToLower() == ".bpcm2") && i > wave)
                    outfile = args[i];

                if (Path.GetExtension(args[i]) == ".wav" && i > bpcm)
                    outfile = args[i];
            }

            if (!enc)
            {
                //Extra parameters
                for (i = 0; i < args.Length; i++)
                    if (args[i] == "-dither") enableDithering = true;
            }

            //If no output file was not given, launch player mode
            if (outfile == "" && infile != "" && (Path.GetExtension(infile).ToLower() == ".bpcm" || Path.GetExtension(infile).ToLower() == ".bpcm2"))
            {
                if (analyze)
                {
                    Decoder.Info inf = Decoder.AnalyzeFile(infile);
                    string JSON =
                       "{\"SamplingRate\":" + inf.SamplingRate + ","
                     + "\"NumberOfChannels\":" + inf.NumberOfChannels + ","
                     + "\"BitrateAvg\":" + inf.BitrateAvg + ","
                     + "\"BitrateMin\":" + inf.BitrateMin + ","
                     + "\"BitrateMax\":" + inf.BitrateMax + ","
                     + "\"BlockSizeNominal\":" + inf.BlockSizeNominal + ","
                     + "\"BlockSizeAverage\":" + inf.BlockSizeAverage + ","
                     + "\"BlockSizeMinimum\":" + inf.BlockSizeMinimum + ","
                     + "\"BlockSizeMaximum\":" + inf.BlockSizeMaximum + ","
                     + "\"CompressionUsedString\":\"" + inf.CompressionUsedString + "\","
                     + "\"Duration\":" + inf.Duration.ToString() + ","
                     + "\"DurationSampleCount\":" + inf.DurationSampleCount + ","
                     + "\"DurationString\":\"" + inf.DurationString + "\","
                     + "\"FrameCount\":" + inf.FrameSet.Count + ","
                     + "\"Frames\":{";
                    foreach (Frame f in inf.FrameSet)
                    {
                        JSON = string.Concat(new string[] {
                            JSON, "\"", f.FrameNumber.ToString(), "\":"
                         , "{\"Channels\":", f.Channels.ToString(), ","
                         , "\"CompressionType\":\"", f.CompressionTypeDescr, "\","
                         , "\"DataLength\":", f.DataLength.ToString(), ","
                         , "\"DataOffset\":", f.DataOffset.ToString(), ","
                         , "\"SampleCount\":", f.SampleCount.ToString(), ","
                         , "\"Duration\":", f.Duration.ToString(), ","
                         , "\"HederLength\":", f.HederLength.ToString(), ","
                         , "\"TimeStamp\":", f.TimeStamp.ToString(), "},"});
                    }
                    JSON = JSON.Substring(0, JSON.Length - 1);
                    JSON = JSON + "},\"FrameLengthHistogram\":{";
                    foreach (KeyValuePair<int, long> p in inf.FrameSampleCountHistogram)
                        JSON = string.Concat(new string[] { JSON, "\"", p.Key.ToString(), "\":", p.Value.ToString(), "," });
                    JSON = JSON.Substring(0, JSON.Length - 1);
                    JSON = JSON + "}}";
                    Console.Write(JSON);
                    return 0;
                }

                float vol = 0.12f;
                double rate = 1;
                int output_device = 0;

                //Check for parameters
                for (i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-vol" && i != args.Length - 1) //Volume parameter
                    {
                        if (!float.TryParse(args[i + 1], out vol)) return PrintUsage();
                    }
                    if (args[i] == "-r" && i != args.Length - 1) //Playback rate parameter
                    {
                        if (!double.TryParse(args[i + 1], out rate)) return PrintUsage();
                    }
                    if (args[i] == "-od" && i != args.Length - 1) //Output device parameter
                    {
                        if (!int.TryParse(args[i + 1], out output_device)) return PrintUsage();
                    }
                }

                play(infile, (float)vol, rate, output_device, enableDithering);
                return 0;
            }

            //When we are in encoding mode, check for parameters
            if (enc)
            {
                //Compression algorithm
                for (i = 0; i < args.Length; i++)
                {
                    if (args[i] == "-c" && i != args.Length - 1)
                    {
                        switch (args[i + 1].ToLower())
                        {
                            case "none":
                                compression = Algorithm.none;
                                break;

                            case "bzip2":
                                compression = Algorithm.BZIP2;
                                break;

                            case "lzma":
                                compression = Algorithm.lzma;
                                break;

                            case "ac":
                                compression = Algorithm.arithmetic;
                                break;

                            case "fast":
                                compression = Algorithm.fast;
                                break;

                            case "brute":
                            case "bruteforce":
                                compression = Algorithm.bruteForce;
                                break;
                        }
                    }
                }

                //Block size
                for (i = 0; i < args.Length; i++)
                    if (args[i] == "-bs" && i != args.Length - 1)
                        if (!int.TryParse(args[i + 1], out blockSize)) return PrintUsage();

                //Fix block size
                if (blockSize < 10) blockSize = 10;
                if (blockSize > 1000) blockSize = 1000;

                //Extra parameters
                for (i = 0; i < args.Length; i++)
                    if (args[i] == "-sltrh" && i != args.Length - 1)
                        if (!short.TryParse(args[i + 1], out SilenceThreshold)) return PrintUsage();
            }

            #endregion Parse Parameters

            if (infile == "" || outfile == "") return PrintUsage();

            TimeSpan e_start;

            //check wether encoding or decoding is requested
            if (enc)
            {
                Console.WriteLine("Encoding file:         " + infile);
                Console.WriteLine("to:                    " + outfile);
                Console.WriteLine("with block size:       " + blockSize.ToString());
                Console.WriteLine("Compression algorithm: " + compression.ToString());
                Console.WriteLine("SilenceThreshold:      " + SilenceThreshold.ToString());
                Console.WriteLine(String.Concat(Enumerable.Repeat("\x2509", 80)));

                e_start = TimeSpan.FromTicks(DateTime.Now.Ticks);

                try
                {
                    Encoder.EncodeWaveFile(
                        infile
                      , outfile, new Encoder.Parameters()
                      {
                          BlockSize = blockSize
                          ,
                          Compression = compression
                          ,
                          SilenceThreshold = SilenceThreshold
                      }
                      , updateStatus
                      , 1000 / 7.5
                    );
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                    return 127;
                }

                void updateStatus(Encoder.Status stts)
                {
                    string progress = "Encoding: " + stts.PositionString + " ("
                                    + ((double)stts.PositionSamples / stts.DurationSamples * 100).ToString("F2")
                                    + "%) at average " + String.Format("{0:0 bits/s}", stts.AvgBitrate) + " at "
                                    + String.Format("{0:0.000}x speed.", stts.Position / (TimeSpan.FromTicks(DateTime.Now.Ticks) - e_start).TotalSeconds);

                    Console.Write(progress.PadRight(Console.WindowWidth - 1, Convert.ToChar(" ")));
                    Console.CursorLeft = 0;
                }
            }
            else
            {
                e_start = TimeSpan.FromTicks(DateTime.Now.Ticks);
                Decoder.DecodeBPCMFile(infile, outfile, updateStatus, 1000 / 7.5, true, initialized, enableDithering);

                void initialized(Decoder.Info inf)
                {
                    Console.WriteLine("{0,-20} {1}", "Filename:", infile);
                    Console.WriteLine("{0,-20} {1}", "to:", outfile);
                    Console.WriteLine("{0,-20} {1}", "File size:", BPCM.Helpers.ByteFormatter.FormatBytes(new FileInfo(infile).Length));
                    PrintInfo(inf);
                    Console.WriteLine(String.Concat(Enumerable.Repeat("\x2509", 80)));
                }

                void updateStatus(Decoder.Status stts)
                {
                    Console.Write(("Decoding: " + stts.PositionString + " (" + stts.PercentageDone.ToString("F2") + "%)"
                                    + " at " + String.Format("{0:0.000}x speed.", stts.Position / (TimeSpan.FromTicks(DateTime.Now.Ticks) - e_start).TotalSeconds)).PadRight(Console.WindowWidth - 1, Convert.ToChar(" ")));
                    Console.CursorLeft = 0;
                }
            }

            return 0;
        }

        private static void PrintInfo(Decoder.Info inf)
        {
            Console.WriteLine("{0,-20} {1}", "Compression:", inf.CompressionUsedString);
            Console.WriteLine("{0,-20} {1} Hz", "Sampling rate:", inf.SamplingRate);
            Console.WriteLine("{0,-20} {1}", "Channels:", inf.NumberOfChannels);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Nominal block size:", Math.Round(((double)inf.BlockSizeNominal / inf.SamplingRate) * 1000, 3), inf.BlockSizeNominal);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Minimum block size:", Math.Round(((double)inf.BlockSizeMinimum / inf.SamplingRate) * 1000, 3), inf.BlockSizeMinimum);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Average block size:", Math.Round(((double)inf.BlockSizeAverage / inf.SamplingRate) * 1000, 3), inf.BlockSizeAverage);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Maximum block size:", Math.Round(((double)inf.BlockSizeMaximum / inf.SamplingRate) * 1000, 3), inf.BlockSizeMaximum);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Minimum Bitrate:", inf.BitrateMin);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Average Bitrate:", inf.BitrateAvg);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Maximum Bitrate:", inf.BitrateMax);
            Console.WriteLine("{0,-20} {1}", "Number of frames:", inf.FrameSet.Count);
            Console.WriteLine("{0,-20} {1}", "Duration:", inf.DurationString);
        }

        private static void printInfo(BitstreamReader.Stats s)
        {
            TimeSpan dur = TimeSpan.FromSeconds(s.Duration);
            Console.WriteLine("{0,-20} {1}", "Compression:", string.Join(", ", s.CompressionUsed.ToArray()));
            Console.WriteLine("{0,-20} {1} Hz", "Sampling rate:", s.FrameSet[0].SamplingRate);
            Console.WriteLine("{0,-20} {1}", "Channels:", s.FrameSet[0].Channels);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Nominal block size:", Math.Round(((double)s.BlockSizeNominal / s.FrameSet[0].SamplingRate) * 1000, 3), s.BlockSizeNominal);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Minimum block size:", Math.Round(((double)s.BlockSizeMinimum / s.FrameSet[0].SamplingRate) * 1000, 3), s.BlockSizeMinimum);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Average block size:", Math.Round(((double)s.BlockSizeAverage / s.FrameSet[0].SamplingRate) * 1000, 3), s.BlockSizeAverage);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Maximum block size:", Math.Round(((double)s.BlockSizeMaximum / s.FrameSet[0].SamplingRate) * 1000, 3), s.BlockSizeMaximum);
            Console.WriteLine("{0,-20} {2} ({1,0:0.000} ms)", "Longest silent blck:", Math.Round(((double)s.LongestSilentFrame / s.FrameSet[0].SamplingRate) * 1000, 3), s.LongestSilentFrame);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Minimum Bitrate:", s.BitrateMinimum);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Average Bitrate:", s.BitrateAverage);
            Console.WriteLine("{0,-20} {1,-6} Bit/s", "Maximum Bitrate:", s.BitrateMaximum);
            Console.WriteLine("{0,-20} {1}", "Number of frames:", s.FrameSet.Count);
            Console.WriteLine("{0,-20} {1:00}d {2:00}h {3:00}m {4:00}s {5:000.000}ms", "Duration:", dur.Days, dur.Hours, dur.Minutes, dur.Seconds, (s.Duration - Math.Floor(s.Duration)) * 1000);
        }

        private static void play(string bpcmfile, float volume, double rate, int output_device = 0, bool enDither = true)
        {
            Console.WriteLine("{0,-20} {1}", "File path:", new FileInfo(bpcmfile).Directory.FullName);
            Console.WriteLine("{0,-20} {1}", "File name:", new FileInfo(bpcmfile).Name);

            //Analyse stream
            FileStream s = new FileStream(bpcmfile, FileMode.Open, FileAccess.Read, FileShare.Read, 1048576, false);

            void AnalyisisProgress(float percentDone)
            {
                Console.CursorLeft = 0;
                Console.Write(String.Concat(Enumerable.Repeat(" ", Console.BufferWidth - 1)));
                Console.CursorLeft = 0;
                Console.Write("{0,-20} {1, -4:0.00} percent done.", "Analyzing file:", percentDone);
            }

            BitstreamReader p = new BitstreamReader(s, aupevt: AnalyisisProgress);
            p.EnableDither = enDither;

            Console.CursorLeft = 0;
            Console.Write(String.Concat(Enumerable.Repeat(" ", Console.BufferWidth - 1)));
            Console.CursorLeft = 0;

            //Print info
            Console.WriteLine("{0,-20} {1}", "File size:", BPCM.Helpers.ByteFormatter.FormatBytes(s.Length));
            printInfo(p.Analysis);
            Console.WriteLine(String.Concat(Enumerable.Repeat("\x2509", 80)));

            //Box line and corner as well as connection chars
            const string strFcF = "\x250C", strFc7 = "\x2510", strFcL = "\x2514", strFcJ = "\x2518"
                       , strFbI = "\x2502", strFbL = "\x2500", strFbT = "\x252C", strFbUMT = "\x2534"
                       , strFbLT = "\x251C", strFbMMT = "\x253C", strFbRT = "\x2524", strAUD = "\x2195", strALR = "\x2194";

            //VU meter chars
            string[,] arrVU = { { " ", "\x2580" }, { " ", "\x2584" } };
            //string[,] arrVU = { { " ", "\x25AE" }, { " ", "\x25AE" } };

            int nBuffers = 16;
            int fnumLen = p.Analysis.FrameSet.Count.ToString().Length;
            if (fnumLen < 6) fnumLen = 6;
            int currFrame = 0;
            double currTS = 0;

            double speed = rate;
            bool dontExit = false;
            WaveOutEvent wavOut;
            BPCMWaveProvider bpcmWP = new BPCMWaveProvider(p, speed);

            //Prepare lines
            string strTsLine = String.Concat(Enumerable.Repeat(strFbL, 22))
                 , strFNumLine = String.Concat(Enumerable.Repeat(strFbL, fnumLen))
                 , strBitRateLine = String.Concat(Enumerable.Repeat(strFbL, 6))
                 , strComprLine = String.Concat(Enumerable.Repeat(strFbL, 10))
                 , strVolumeLine = String.Concat(Enumerable.Repeat(strFbL, 6))
                 , strRateLine = String.Concat(Enumerable.Repeat(strFbL, 5))
                 , strVULine = String.Concat(Enumerable.Repeat(strFbL, 58))
                 , strVUScale = "-\x221e\x2508"
                               + "-60" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-50" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-40" + string.Concat(Enumerable.Repeat("\x2508", 7))
                               + "-30" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-20" + string.Concat(Enumerable.Repeat("\x2508", 4))
                               + "-12" + string.Concat(Enumerable.Repeat("\x2508", 2))
                               + "-6\x2508-3\x25080"
                 , strVUBlank = String.Concat(Enumerable.Repeat(" ", 58));

            void readDone(Frame CurrentFrame)
            {
                currFrame = CurrentFrame.FrameNumber;
                PCM.ADPCM.ADPCM4BIT.VolumeInfo vi = CurrentFrame.VolumeInfo;

                currTS = CurrentFrame.TimeStamp;
                TimeSpan tsnPos = TimeSpan.FromSeconds(currTS);
                int days = tsnPos.Days; if (days > 9) days = 9; //Clamp days never to be more than 9.
                string strPos = String.Format("{0}d {1:00}h {2:00}m {3:00}s {4:000}ms", days, tsnPos.Hours, tsnPos.Minutes, tsnPos.Seconds, tsnPos.Milliseconds);
                //Debug.WriteLine(strPos);

                //Refresh the status line
                Console.CursorLeft = 0;
                Console.Write(strFbI + strALR + "{0,21}" + strFbI + "{1," + fnumLen + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + strAUD + "{4,4}%" + strFbI + "x{5,4}" + strFbI,
                                strPos, currFrame + 1, Math.Round(((CurrentFrame.DataLength + CurrentFrame.HederLength) / CurrentFrame.Duration) * 8, 0), CurrentFrame.CompressionTypeDescr, Math.Round(wavOut.Volume * 100, 1), speed);

                //Calculate bar length from dB value.
                const int max = 57, lowpoint = 62; //lowpoint is the positive value of the minus dB the scale will start
                int L = (int)Math.Round(((vi.dbPeakL + lowpoint) / lowpoint) * max);
                if (L < 0) L = 0; //clamp

                int R = (int)Math.Round(((vi.dbPeakR + lowpoint) / lowpoint) * max);
                if (R < 0) R = 0; //clamp

                string strVUMeterL = arrVU[0, 1] + String.Concat(Enumerable.Repeat(arrVU[0, 1], L)) + String.Concat(Enumerable.Repeat(arrVU[0, 0], max - L));
                string strVUMeterR = arrVU[1, 1] + String.Concat(Enumerable.Repeat(arrVU[1, 1], R)) + String.Concat(Enumerable.Repeat(arrVU[1, 0], max - R));

                Console.CursorLeft = 0;
                Console.CursorTop += 2;
                Console.WriteLine(strFbI + "L " + strVUMeterL + strFbI);

                Console.CursorLeft = 0;
                Console.CursorTop += 1;
                Console.WriteLine(strFbI + "R " + strVUMeterR + strFbI);

                Console.CursorLeft = 0;
                Console.CursorTop -= 5;
            }

            void woutInit()
            {
                wavOut = new WaveOutEvent();
                wavOut.DeviceNumber = output_device;
                wavOut.DesiredLatency = 100 * wavOut.NumberOfBuffers;
                wavOut.PlaybackStopped += stopped;
                wavOut.Init(bpcmWP);
                wavOut.Volume = volume;
                nBuffers = wavOut.NumberOfBuffers;
                Console.CursorVisible = false;
            }

            woutInit();

            bpcmWP.readDone = readDone;
            bpcmWP.volume = 1;

            void changeRate()
            {
                if (WaveOut.GetCapabilities(wavOut.DeviceNumber).SupportsPlaybackRateControl && 1 == 2)
                {
                    wavOut.Rate = (float)speed;
                }
                else
                {
                    reinitBPCMWaveSrc();
                }
            }

            void reinitBPCMWaveSrc()
            {
                dontExit = true;
                volume = wavOut.Volume;
                wavOut.Stop();
                wavOut.Dispose();
                wavOut = null;
                bpcmWP = null;
                GC.Collect();
                bpcmWP = new BPCMWaveProvider(p, speed);
                bpcmWP.volume = 1;
                bpcmWP.readDone = readDone;
                p.Seek(currFrame - 1); //fix for skipping
                woutInit();
                wavOut.Play();
            }

            string DeviceName = "";
            for (int x = 0; x < WaveOut.DeviceCount; x++)
            {
                if (x == wavOut.DeviceNumber)
                {
                    DeviceName = "[" + x + "] " + WaveOut.GetCapabilities(x).ProductName;
                    break;
                }
            }

            //Playback stopped event handler method
            void stopped(object sender, StoppedEventArgs e) { if (!dontExit) exitNow(); else dontExit = false; }

            //Drawing status box
            Console.WriteLine("{0,-20} {1}", "Playing on:", DeviceName);
            Console.WriteLine("");

            //Upper corner of box
            Console.WriteLine(strFcF + strTsLine + strFbT + strFNumLine + strFbT + strBitRateLine + strFbT + strComprLine + strFbT + strVolumeLine + strFbT + strRateLine + strFc7);
            //Descriptions
            Console.WriteLine(strFbI + "{0,-22}" + strFbI + "{1,-" + fnumLen + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + "{4,-6}" + strFbI + "{5,-5}" + strFbI, "timestamp", "frame#", "bit/s", "compr.", "volume", "rate");
            //Divider lines
            Console.WriteLine(strFbLT + strTsLine + strFbMMT + strFNumLine + strFbMMT + strBitRateLine + strFbMMT + strComprLine + strFbMMT + strVolumeLine + strFbMMT + strRateLine + strFbRT);
            //Status line (empty here)
            Console.WriteLine(strFbI + strALR + "{0,21}" + strFbI + "{1," + fnumLen + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + strAUD + "{4,4}%" + strFbI + "{5,5}" + strFbI, "N/A", "N/A", "N/A", "N/A", "N/A", "N/A");
            //Bottom corner of box
            Console.WriteLine(strFbLT + strTsLine + strFbUMT + strFNumLine + strFbUMT + strBitRateLine + strFbUMT + strComprLine + strFbUMT + strVolumeLine + strFbUMT + strRateLine + strFbRT);

            //VU top
            //Console.WriteLine(strFcF + strFbL + strFbL + strVULine + strFc7);
            Console.WriteLine(strFbI + "L " + strVUBlank + strFbI);
            //VU middle
            Console.WriteLine(strFbI + " " + strVUScale + strFbI);
            Console.WriteLine(strFbI + "R " + strVUBlank + strFbI);
            //VU bottom
            Console.WriteLine(strFcL + strFbL + strFbL + strVULine + strFcJ);

            Console.WriteLine("");
            Console.WriteLine("Volume:  Up/Down 1% steps, Hold CTRL 0.1% steps, PgUp/PgDwn 10% steps");
            Console.WriteLine("Seeking: Left/Right 5 secs, Hold CTRL 1 sec steps, Home for beginning");
            Console.WriteLine("Speed:   S, D and F. You'll figure it out. ;) You can also use CTRL!");

            //Set cursor top position to the status line.
            Console.CursorTop -= 10;

            bool exit = false;

            void exitNow()
            {
                try
                {
                    Console.CursorTop += 10;
                    Console.CursorLeft = 0;
                    Console.CursorVisible = true;
                    wavOut?.Dispose();
                    p = null;
                }
                catch
                {
                }
                Environment.Exit(0);
            }

            //Start playback
            wavOut.Play();

            //Crude key scanning
            while (!exit)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Modifiers == ConsoleModifiers.Control)
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.VolumeUp:
                            if (wavOut.Volume + 0.001f >= 1.0f) wavOut.Volume = 1.0f; else wavOut.Volume += 0.001f;
                            break;

                        case ConsoleKey.DownArrow:
                        case ConsoleKey.VolumeDown:
                            if (wavOut.Volume - 0.001f <= 0.0f) wavOut.Volume = 0.0f; else wavOut.Volume -= 0.001f;
                            break;

                        case ConsoleKey.LeftArrow:
                            p.Seek(currTS - 1);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.RightArrow:
                            p.Seek(currTS + 1);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.S:
                            if (speed - 0.01 <= 0.01f) speed = 0.01; else speed -= 0.01;
                            changeRate();
                            break;

                        case ConsoleKey.F:
                            if (speed + 0.01 >= 4.0f) speed = 4.0; else speed += 0.01;
                            changeRate();
                            break;
                    }
                }
                else if (key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.LeftArrow:
                            p.Seek(currTS - 60);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.RightArrow:
                            p.Seek(currTS + 60);
                            bpcmWP.DropRingBuffer();
                            break;
                    }
                }
                else
                {

                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.VolumeUp:
                            if (wavOut.Volume + 0.01f >= 1.0f) wavOut.Volume = 1.0f; else wavOut.Volume += 0.01f;
                            break;

                        case ConsoleKey.DownArrow:
                        case ConsoleKey.VolumeDown:
                            if (wavOut.Volume - 0.01f <= 0.0f) wavOut.Volume = 0.0f; else wavOut.Volume -= 0.01f;
                            break;

                        case ConsoleKey.PageUp:
                            if (wavOut.Volume + 0.1f >= 1.0f) wavOut.Volume = 1.0f; else wavOut.Volume += 0.1f;
                            break;

                        case ConsoleKey.PageDown:
                            if (wavOut.Volume - 0.1f <= 0.0f) wavOut.Volume = 0.0f; else wavOut.Volume -= 0.1f;
                            break;

                        case ConsoleKey.LeftArrow:
                            p.Seek(currTS - 5d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.RightArrow:
                            p.Seek(currTS + 5d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D1:
                            p.Seek(60d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D2:
                            p.Seek(120d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D3:
                            p.Seek(180d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D4:
                            p.Seek(240d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D5:
                            p.Seek(300d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D6:
                            p.Seek(360d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D7:
                            p.Seek(420d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D8:
                            p.Seek(480d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D9:
                            p.Seek(540d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.D0:
                            p.Seek(600d);
                            bpcmWP.DropRingBuffer();
                            break;

                        case ConsoleKey.S:
                            if (speed - 0.1 <= 0.01f) speed = 0.01; else speed -= 0.1;
                            changeRate();
                            break;

                        case ConsoleKey.D:
                            speed = 1f;
                            changeRate();
                            break;

                        case ConsoleKey.F:
                            if (speed + 0.1 >= 4.0f) speed = 4.0; else speed += 0.1;
                            changeRate();
                            break;

                        case ConsoleKey.Home:
                            p.Seek(0);
                            break;

                        case ConsoleKey.Spacebar:
                            switch (wavOut.PlaybackState)
                            {
                                case PlaybackState.Playing:
                                    wavOut.Pause();
                                    break;

                                case PlaybackState.Paused:
                                    wavOut.Play();
                                    break;
                            }
                            break;

                        case ConsoleKey.Escape:
                        case ConsoleKey.Q:
                            Console.CursorTop -= 10;
                            exitNow();
                            break;
                    }
                }
            }
            p = null;
        }
    }
}