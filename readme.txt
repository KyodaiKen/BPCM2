BPCM2: Entropy encoded ADPCM audio codec version 2.0.0.0 "Feline"
================================================================================

------------------------ Usage ------------------------
Encode:   bpcm input.wav output.bpcm
Decode:   bpcm input.bpcm output.wav
Playback: bpcm input.bpcm
You can place your option parameters anywhere you like!

== Encoding options ==
-c     string    Use specific compression method
                   ► none
                   ► ac (Arithmetic order0)
                   ► BZIP2
                   ► LZMA
                   ► fast  (use best result of LZMA and ac [DEFAULT] [RECOMMENDED!]),
                   ► brute (use best result of all)
-bs    number    Sets the block size in milliseconds, default is 100 ms

== Player options ==
-vol   decimal   Sets the initial playback volume from 0 ... 1 (float). Default is 0.12.
-r     decimal   Sets the initial playback speed as a factor value (float). Default is 1.
-od    number    Audio output device (0 = system default)
                   ► 0: Lautsprecher (USB Sound Blaster
                   ► 1: Headphones (Realtek High Defini
                   ► 2: SPDIF-Out (USB Sound Blaster HD

== Tewaks ==
= Encoding =
-sltrh number    Sets the silence threshold from 0 ... 32747 (integer). Default is 4
= Decoding / Playback =
-nodither        Disables random dithering on decoding, makes decoding faster