# BPCM2: Entropy encoded ADPCM audio codec version 2.0.0.0 "Feline"

## QuickLinks

* [Usage](#Usage)
    * [Encoding options](<#Encoding options>)
    * [Player options](<#Player options>)
* [Tewaks](#Tewaks)
* [Stream Stucture](<#Stream structure>)
* [Stream VERSION 2](<#Stream VERSION 2>)

## Usage

* Encode:   bpcm input.wav output.bpcm
* Decode:   bpcm input.bpcm output.wav
* Playback: bpcm input.bpcm

You can place your option parameters anywhere you like!

### Encoding options

1. -c     string    Use specific compression method
    * ► none
    * ► ac (Arithmetic order0)
    * ► BZIP2
    * ► LZMA
    * ► fast  (use best result of LZMA and ac [DEFAULT] [RECOMMENDED!]),
    * ► brute (use best result of all)
2. -bs    number    Sets the block size in milliseconds, default is 100 ms

### Player options

*   -vol   decimal   Sets the initial playback volume from 0 ... 1 (float). Default is 0.12.
* -r     decimal   Sets the initial playback speed as a factor value (float). Default is 1.
* -od    number    Audio output device (0 = system default)
                   ► 0: Lautsprecher (USB Sound Blaster
                   ► 1: Headphones (Realtek High Defini
                   ► 2: SPDIF-Out (USB Sound Blaster HD

## Tewaks

### = Encoding =

* -sltrh number    Sets the silence threshold from 0 ... 32747 (integer). Default is 4
= Decoding / Playback =
* -nodither        Disables random dithering on decoding, makes decoding faster

## Stream structure

### Stream VERSION 2

This version range supports only 16 bit sampling input in INTEL little endian (in VLC called s16l)!

* SYNC BYTE
```cs
0xB1 //Implies Version 2
```

* FRAME STRUCTURE
Two zeros = one byte, so 00 | 00 = word (16 bit) and 00 | 00 | 00 24 bit word so 3 bytes:
```
SYNC | INFO | nSamples *1  | DATA-LEN     | DATA*2 ----------------------->
B1   | 00   | 00 | 00      | 00 | ?? | ?? | [BINARY COMPRESSED AUDIO DATA]

*1 only when we need to set a block size
*2: only when data available, the datatype varies between 1 and 3 bytes
   On a silent frame it contains a double value after the info byte!
   (BZIP2 = 8, LZMA = 16 and arithmetic = 24 bit) with the sample count.
   In case compression is none, this frame is rejected!
```



* INFO BYTE
    * BIT 12 = data available? (see table)
    * BIT 34 = compression type (see table)
    * BIT 5  = channel config: 0=mono, 1=stereo
    * BIT 6  = use last blocksize: 0=use given (*1 is written) 1=use last
    * BIT 78 = sampling clock (see table)

* Data address table
    * 00 0 = No data / silence
    * 01 1 = 8 bit data length addressing
    * 10 2 = 16 bit data length addressing
    * 11 3 = 24 bit data length addressing

* Compression type table
    * 00 0 = none
    * 01 1 = BZIP2
    * 10 2 = LZMA
    * 11 3 = arithmetic order0

* Sampling clock table:
    * 00 0 =  44100 Hz
    * 01 1 =  48000 Hz
    * 10 2 =  32000 Hz
    * 11 3 =  24000 Hz