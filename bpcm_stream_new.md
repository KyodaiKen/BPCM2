BPCM Stream structure - BPCM VERSION: X
=======================================

This version range supports only 16 bit sampling input in INTEL little endian (in VLC called s16l)!

Version X is a CHANGE version that is intended to test some new ideas and change the concept.

It's still a streaming format with variable bitrate and no VBV buffer.

# FRAME STRUCTURE
## SYNC
BYTE: 0xDB - Implies Version X

- SY = Sync
- IB = Info Byte
- EX = Extension Byte
- SR = Sampling Rate
- NS = number of samples (1 byte each occurrence)
- DS = data size / length (1 byte each occurrence)

Order: `SY IB EX SR NS NS NS DS DS DS`

Those bytes are not always written, see mode control.

## Info byte layout
Bits  | Description
----- | ----------------------------
1,2,3 | Mode
4,5   | Compression type (see table)
7     | Channel mode (see table)

## Mode table
Bits | Value | Description
----:| -----:| ------------------------------------
000  |     0 | define sampling rate and block size
001  |     1 | use defined sampling rate and block size
010  |     2 | RESEVED / INVALID
011  |     3 | silent frame, use block size
100  |     4 | silent frame, use last known block size
101  |     5 | RESEVED / INVALID
110  |     6 | header supplement mode without data length
111  |     7 | header supplement mode

## Mode control table
Value |  0  |   1 |   2 |  *1 |  *2 |  *3 |  *4 | length 16 bit | length 24 bit | struct 16 bit          | struct 24 bit
-----:| --- | --- | --- | --- | --- | --- | --- | -------------:| -------------:| ---------------------- | --------------------------
0     |  0  |   0 |   0 |   1 |   1 |   1 |   0 |             7 |             9 | `SY IB SR NS NS DS DS` | `SY IB SR NS NS NS DS DS DS`
1     |  0  |   0 |   1 |   0 |   0 |   1 |   0 |             4 |             5 | `SY IB DS DS`          | `SY IB DS DS DS`
3     |  0  |   1 |   1 |   0 |   1 |   0 |   0 |             4 |             5 | `SY IB NS NS`          | `SY IB NS NS NS`
4     |  1  |   0 |   0 |   0 |   0 |   0 |   0 |             2 |             2 | `SY IB`                | `SY IB`
6     |  1  |   1 |   0 |   0 |   0 |   0 |   1 |             3 |             3 | `SY IB SU`             | `SY IB SU`
7     |  1  |   1 |   1 |   0 |   0 |   1 |   1 |             5 |             6 | `SY IB SU DS DS`       | `SY IB SU DS DS DS`

## Compression type table
Bits | Value | Description
----:| -----:| -----------------
00   |     0 | LZMA level 2
01   |     1 | LZMA level 4 
10   |     2 | arithmetic order0
11   |     3 | Brotli

## Channel mode table
Bits | Value | Description
----:| -----:| -----------------
0    |     0 | mono
1    |     1 | stereo

## Sampling rate formula
- The sampling rate is stored in 100 Hz steps, the minimum is 24000 Hz and the maximum is 49500 Hz
- byteValue = (samplingRate - 24000) / 100;
- samplintRate = byteValue * 100 + 24000;

## header supplement mode
This mode is for allowing additional data such as control data, text data, tagging and so on. It adds an extra byte to the header and depending on the value of that said byte, there can also be additional data. When this mode is activated, this is a frame that is ignored for playback data.

Here is a table of the supported supplements.

### Supplement byte values
- `1x`: Meta data
  - `10`: Meta data - JSON tags (Brotli compressed)
  - `11`: Meta data - Image, JPEG
  - `12`: Meta data - Image, PNG
  - `13`: Meta data - Lyrics/Text (Brotli compressed)
- `2x`: Subtitles / Karaoke (Brotli compressed)
  - `20`: SubRip / SubViewer - SRT
  - `21`: SubStation Alpha - SSA
  - `22`: Synchronized Multimedia Integration Language - SMIL
  - `23`: Timed Text Markup Language - TTML
  - `24`: MPEG-4 Timed Text
  - `25`: Continuous Media Markup Language - CMML
  - `26`: LyRiCs - LRC
  - `27`: Universal Subtitle Format - USF
  - `28`: VobSub
  - `29`: WebVTT
  - `2A`: MicroDVD
  - `2F`: BPCM subtitle format
- `4x`: Stream ID - allows up to 16 different audio streams! (no data)
  - `A0`: Warp point (reference to another frame)

Any value that is not implemented is invalid and the frame will be ignored.

# Notes
Maximum block size for non silent frames is restricted to one second.