using BPCM;
using BPCM.Easy;
using BPCM.Wave;
using NAudioLitle.Wave;
using PCM;
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
            Console.WriteLine("Encode:   bpcm input.wav output.bpcm2");
            Console.WriteLine("Decode:   bpcm input.bpcm2 output.wav");
            Console.WriteLine("Playback: bpcm input.bpcm2");
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
            for (int x = 0; x < AudioDeviceManager.AudioDevices.Count; x++) Console.WriteLine("                   ► " + x + ": " + AudioDeviceManager.AudioDevices[x].Name);
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

        private static void PrintInfo(Decoder.Info inf)
        {
            Console.WriteLine("{0,-20} {1}", "File size:", BPCM.Helpers.ByteFormatter.FormatBytes(inf.FileSize));
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

        private static void MoveConsoleCursor(Nullable<int> left, Nullable<int> top, bool relative = true)
        {
            if(relative)
            {
                if (Console.CursorLeft + (int)left < 0) {
                    Console.CursorLeft = 0;
                }
                else
                {
                    if (Console.CursorLeft + (int)left > Console.BufferWidth-1)
                        Console.CursorLeft = Console.BufferWidth-1;
                    else
                        Console.CursorLeft += (int)left;
                }
                if (Console.CursorTop + (int)top < 0)
                {
                    Console.CursorTop = 0;
                }
                else
                {
                    if (Console.CursorTop + top > Console.BufferHeight-1)
                        Console.CursorTop = Console.BufferHeight-1;
                    else
                        Console.CursorTop += (int)top;
                }
            }
            else
            {
                if(!left.Equals(null))
                {
                    if (left < 0)
                    {
                        Console.CursorLeft = 0;
                    }
                    else
                    {
                        if (left > Console.BufferWidth)
                        {
                            Console.CursorLeft = Console.BufferWidth-1;
                        }
                        else
                        {
                            Console.CursorLeft = (int)left;
                        }
                    }
                }

                if (!top.Equals(null))
                {
                    if (top < 0)
                    {
                        Console.CursorTop = 0;
                    }
                    else
                    {
                        if (top > Console.BufferHeight)
                        {
                            Console.CursorTop = Console.BufferHeight-1;
                        }
                        else
                        {
                            Console.CursorTop = (int)top;
                        }
                    }
                }
            }
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

            Algorithm algorithm = Algorithm.fast;

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
                                algorithm = Algorithm.none;
                                break;

                            case "bzip2":
                                algorithm = Algorithm.BZIP2;
                                break;

                            case "lzma":
                                algorithm = Algorithm.lzma;
                                break;

                            case "ac":
                                algorithm = Algorithm.arithmetic;
                                break;

                            case "fast":
                                algorithm = Algorithm.fast;
                                break;

                            case "brute":
                            case "bruteforce":
                                algorithm = Algorithm.bruteForce;
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
                Console.WriteLine("Compression algorithm: " + algorithm.ToString());
                Console.WriteLine("SilenceThreshold:      " + SilenceThreshold.ToString());
                Console.WriteLine(string.Concat(Enumerable.Repeat("\x2509", 80)));

                e_start = TimeSpan.FromTicks(DateTime.Now.Ticks);

                try
                {
                    Encoder.EncodeWaveFile(
                        infile
                      , outfile, new Encoder.Parameters()
                      {
                            BlockSize = blockSize
                          , Compression = algorithm
                          , SilenceThreshold = SilenceThreshold
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

                Decoder.DecodeBPCMFile(infile, outfile, new Decoder.ConfigurationBean()
                {
                    UpdateInterval = 1000 / 7.5,
                    Analyze = false,
                    EnableDither = enableDithering,
                    FileOpenedEvent = initialized,
                    ProgressUpdateEvent = updateStatus
                });

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

        private static void play(string BPCMFile, float volume, double rate, int OutputDeviceIndex, bool EnableDither = false)
        {
            Decoder.Info FileInfo;
            FileInfo.FrameSet = new List<Frame>();
            bool exit = false;
            int FrameCountStringLength = 0;

            //Box line and corner as well as connection chars
            const string strFcF = "\x250C", strFc7 = "\x2510", strFcL = "\x2514", strFcJ = "\x2518"
                       , strFbI = "\x2502", strFbL = "\x2500", strFbT = "\x252C", strFbUMT = "\x2534"
                       , strFbLT = "\x251C", strFbMMT = "\x253C", strFbRT = "\x2524", strAUD = "\x2195", strALR = "\x2194";

            //VU meter chars
            string[,] arrVU = { { " ", "\x2580" }, { " ", "\x2584" } };

            //Init player
            var _player = new Player(BPCMFile, new Player.ConfigurationBean()
            {
                Volume = volume,
                PlaybackRate = rate,
                WaveOutDevice = OutputDeviceIndex,
                EnableDithering = EnableDither
            });

            //Assign events
            _player.PlaybackUpdateEvent = PlaybackUpdate;
            _player.AnalysisUpdateEvent = AnalysisUpdate;
            _player.FileOpenedEvent = FileOpened;

            void FileOpened(Decoder.Info inf)
            {
                Console.CursorLeft = 0;
                Console.WriteLine("{0,-20} {1}", "File path:", new FileInfo(BPCMFile).Directory.FullName);
                Console.WriteLine("{0,-20} {1}", "File name:", new FileInfo(BPCMFile).Name);
                PrintInfo(inf);
                FileInfo = inf;
            }

            Console.WriteLine(string.Concat(Enumerable.Repeat("\x2509", 80)));

            FrameCountStringLength = FileInfo.FrameSet.Count.ToString().Length;
            if (FrameCountStringLength < 6) FrameCountStringLength = 6;

            //Prepare lines
            string strTsLine = string.Concat(Enumerable.Repeat(strFbL, 22))
                 , strFNumLine = string.Concat(Enumerable.Repeat(strFbL, FrameCountStringLength))
                 , strBitRateLine = string.Concat(Enumerable.Repeat(strFbL, 6))
                 , strComprLine = string.Concat(Enumerable.Repeat(strFbL, 10))
                 , strVolumeLine = string.Concat(Enumerable.Repeat(strFbL, 6))
                 , strRateLine = string.Concat(Enumerable.Repeat(strFbL, 5))
                 , strVULine = string.Concat(Enumerable.Repeat(strFbL, 58))
                 , strVUScale = "-\x221e\x2508"
                               + "-60" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-50" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-40" + string.Concat(Enumerable.Repeat("\x2508", 7))
                               + "-30" + string.Concat(Enumerable.Repeat("\x2508", 6))
                               + "-20" + string.Concat(Enumerable.Repeat("\x2508", 4))
                               + "-12" + string.Concat(Enumerable.Repeat("\x2508", 2))
                               + "-6\x2508-3\x25080"
                 , strVUBlank = string.Concat(Enumerable.Repeat(" ", 58));


            //Drawing status box
            Console.WriteLine("{0,-20} {1}", "Playing on:", AudioDeviceManager.AudioDevices[OutputDeviceIndex].Name);
            Console.WriteLine("");

            //Upper corner of box
            Console.WriteLine(strFcF + strTsLine + strFbT + strFNumLine + strFbT + strBitRateLine + strFbT + strComprLine + strFbT + strVolumeLine + strFbT + strRateLine + strFc7);
            //Descriptions
            Console.WriteLine(strFbI + "{0,-22}" + strFbI + "{1,-" + FrameCountStringLength + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + "{4,-6}" + strFbI + "{5,-5}" + strFbI, "timestamp", "frame#", "bit/s", "compr.", "volume", "rate");
            //Divider lines
            Console.WriteLine(strFbLT + strTsLine + strFbMMT + strFNumLine + strFbMMT + strBitRateLine + strFbMMT + strComprLine + strFbMMT + strVolumeLine + strFbMMT + strRateLine + strFbRT);
            //Status line (empty here)
            Console.WriteLine(strFbI + strALR + "{0,21}" + strFbI + "{1," + FrameCountStringLength + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + strAUD + "{4,4}%" + strFbI + "{5,5}" + strFbI, "N/A", "N/A", "N/A", "N/A", "N/A", "N/A");
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
            MoveConsoleCursor(0, -10, true);

            void AnalysisUpdate(float progress)
            {
                Console.CursorLeft = 0;
                Console.Write(string.Concat(Enumerable.Repeat(" ", Console.BufferWidth - 1)));
                Console.CursorLeft = 0;
                Console.Write("{0,-20} {1, -4:0.00} percent done.", "Analyzing file:", progress);
            }

            void PlaybackUpdate(Player.PlaybackUpdateInfo Info)
            {
                var currFrame = Info.CurrentFrame.FrameNumber;
                VolumeInfo[] vi = Info.CurrentFrame.VolumeInfo;

                var currTS = Info.CurrentFrame.TimeStamp;
                TimeSpan tsnPos = TimeSpan.FromSeconds(currTS);
                int days = tsnPos.Days; if (days > 9) days = 9; //Clamp days never to be more than 9.
                string strPos = String.Format("{0}d {1:00}h {2:00}m {3:00}s {4:000}ms", days, tsnPos.Hours, tsnPos.Minutes, tsnPos.Seconds, tsnPos.Milliseconds);
                //Debug.WriteLine(strPos);

                //Refresh the status line
                Console.CursorLeft = 0;
                Console.Write(strFbI + strALR + "{0,21}" + strFbI + "{1," + FrameCountStringLength + "}" + strFbI + "{2,6}" + strFbI + "{3,-10}" + strFbI + strAUD + "{4,4}%" + strFbI + "x{5,4:0.##}" + strFbI,
                                strPos, currFrame + 1, Math.Round(((Info.CurrentFrame.DataLength + Info.CurrentFrame.HederLength) / Info.CurrentFrame.Duration) * 8, 0), Info.CurrentFrame.CompressionTypeDescr, Math.Round(Info.Volume * 100, 1), Math.Round(Info.PlaybackRate, 2));

                //Calculate bar length from dB value.
                const int max = 57, lowpoint = 62; //lowpoint is the positive value of the minus dB the scale will start
                int L, R;
                if (vi != null)
                {
                    L = (int)Math.Round(((vi[0].dbPeak + lowpoint) / lowpoint) * max);
                    if (L < 0) L = 0; //clamp

                    R = (int)Math.Round(((vi[1].dbPeak + lowpoint) / lowpoint) * max);
                    if (R < 0) R = 0; //clamp
                }
                else
                {
                    L = 0;
                    R = 0;
                }
   

                string strVUMeterL = arrVU[0, 1] + string.Concat(Enumerable.Repeat(arrVU[0, 1], L)) + string.Concat(Enumerable.Repeat(arrVU[0, 0], max - L));
                string strVUMeterR = arrVU[1, 1] + string.Concat(Enumerable.Repeat(arrVU[1, 1], R)) + string.Concat(Enumerable.Repeat(arrVU[1, 0], max - R));

                Console.CursorLeft = 0;
                MoveConsoleCursor(0, 2, true);
                Console.WriteLine(strFbI + "L " + strVUMeterL + strFbI);

                Console.CursorLeft = 0;
                MoveConsoleCursor(0, 1, true);
                Console.WriteLine(strFbI + "R " + strVUMeterR + strFbI);

                Console.CursorLeft = 0;
                MoveConsoleCursor(0, -5, true);
            }


            _player.Playing = true;

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
                            if (_player.Volume + 0.001f >= 1.0f) _player.Volume = 1.0f; else _player.Volume += 0.001f;
                            break;

                        case ConsoleKey.DownArrow:
                        case ConsoleKey.VolumeDown:
                            if (_player.Volume - 0.001f <= 0.0f) _player.Volume = 0.0f; else _player.Volume -= 0.001f;
                            break;

                        case ConsoleKey.LeftArrow:
                            _player.Position -= 1;
                            break;

                        case ConsoleKey.RightArrow:
                            _player.Position += 1;
                            break;

                        case ConsoleKey.S:
                            if (_player.PlaybackRate - 0.01 <= 0.01f) _player.PlaybackRate = 0.01; else _player.PlaybackRate -= 0.01;
                            break;

                        case ConsoleKey.F:
                            if (_player.PlaybackRate + 0.01 >= 4.0f) _player.PlaybackRate = 4.0; else _player.PlaybackRate += 0.01;
                            break;
                    }
                }
                else if (key.Modifiers == (ConsoleModifiers.Control | ConsoleModifiers.Shift))
                {
                    switch (key.Key)
                    {
                        case ConsoleKey.LeftArrow:
                            _player.Position -= 60;
                            break;

                        case ConsoleKey.RightArrow:
                            _player.Position += 60;
                            break;
                    }
                }
                else
                {

                    switch (key.Key)
                    {
                        case ConsoleKey.UpArrow:
                        case ConsoleKey.VolumeUp:
                            if (_player.Volume + 0.01f >= 1.0f) _player.Volume = 1.0f; else _player.Volume += 0.01f;
                            break;
                        case ConsoleKey.DownArrow:
                        case ConsoleKey.VolumeDown:
                            if (_player.Volume - 0.01f <= 0.0f) _player.Volume = 0.0f; else _player.Volume -= 0.01f;
                            break;
                        case ConsoleKey.PageUp:
                            if (_player.Volume + 0.1f >= 1.0f) _player.Volume = 1.0f; else _player.Volume += 0.1f;
                            break;
                        case ConsoleKey.PageDown:
                            if (_player.Volume - 0.1f <= 0.0f) _player.Volume = 0.0f; else _player.Volume -= 0.1f;
                            break;
                        case ConsoleKey.LeftArrow:
                            _player.Position -= 5;
                            break;
                        case ConsoleKey.RightArrow:
                            _player.Position += 5;
                            break;
                        case ConsoleKey.D1:
                            _player.Position = 60;
                            break;
                        case ConsoleKey.D2:
                            _player.Position = 120;
                            break;
                        case ConsoleKey.D3:
                            _player.Position = 180;
                            break;
                        case ConsoleKey.D4:
                            _player.Position = 240;
                            break;
                        case ConsoleKey.D5:
                            _player.Position = 300;
                            break;
                        case ConsoleKey.D6:
                            _player.Position = 360;
                            break;
                        case ConsoleKey.D7:
                            _player.Position = 420;
                            break;
                        case ConsoleKey.D8:
                            _player.Position = 480;
                            break;
                        case ConsoleKey.D9:
                            _player.Position = 540;
                            break;
                        case ConsoleKey.D0:
                            _player.Position = 600;
                            break;
                        case ConsoleKey.S:
                            if (_player.PlaybackRate - 0.1 <= 0.01f) _player.PlaybackRate = 0.01; else _player.PlaybackRate -= 0.1;
                            break;
                        case ConsoleKey.D:
                            _player.PlaybackRate = 1;
                            break;
                        case ConsoleKey.F:
                            if (_player.PlaybackRate + 0.1 >= 4.0f) _player.PlaybackRate = 4.0; else _player.PlaybackRate += 0.1;
                            break;
                        case ConsoleKey.Home:
                            _player.Position = 0;
                            break;
                        case ConsoleKey.Spacebar:
                            _player.Playing = !_player.Playing;
                            break;
                        case ConsoleKey.Escape:
                        case ConsoleKey.Q:
                            MoveConsoleCursor(0, 10, true);
                            Console.CursorLeft = 0;
                            exit = true;
                            break;
                    }
                }
            }

            _player?.Dispose();
        }
    }
}