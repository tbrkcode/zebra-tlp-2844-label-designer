# EPL2 protocol and verification notes

## Target and sources

Target: original Zebra **TLP 2844**, EPL2 page language, 8 dots/mm, maximum
832-dot printable width. This is not a TLP 2844-Z implementation. Baseline media
limits are conservative for the unexpanded model. There is no ZPL generator and
no firmware update operation.

The implementation was checked against Zebra's **EPL Programming Guide,
14245L-003 Rev. A**, downloaded from the official Zebra documentation site,
and these primary references (reviewed 31 October 2026):

- [EPL programming guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/epl2-pm-en.pdf)
- [Zebra GW bit polarity](https://cpws.zebra.com/cpws/docs/eltron/gw_command.htm)
- [Zebra GW command applicability](https://support.zebra.com/article/000016453)
- [Zebra speed command](https://support.zebra.com/article/000014807)
- [Zebra configuration print](https://support.zebra.com/article/000015691)
- [Microsoft StartDocPrinter](https://learn.microsoft.com/windows/win32/printdocs/startdocprinter)
- [Microsoft RAW data type](https://learn.microsoft.com/windows-hardware/drivers/print/raw-data-type)

## Commands emitted

| Command | Use | Guide page |
| --- | --- | --- |
| `R0,0` then `q<width>` | Reset prior reference; set label buffer width. q follows R because a later R overrides q. | 137–138, 143 |
| `Q<height>,<gap>` | Label face height and separate gap; 0 gap means continuous stock. | 139–142 |
| `S1`–`S4` | Model-specific speed code. | 144 |
| `D0`–`D15` | Density. | 88 |
| `ZT` | Fixed buffer direction; optional rotation is applied in the raster beforehand. | 170 |
| `OD` or `O` | Direct thermal or thermal transfer; disable other option modes. | 132–133 |
| `N` | Clear buffer after configuration, before image data. | 122 |
| `GW0,0,<bytesPerRow>,<rows>,<binary>` | Unformatted binary monochrome image payload. | 110 |
| `P<count>` | Print copies. | 135 |
| `A...` / `B...` | Native ASCII and Code 128 diagnostic only. Code 128 auto uses type `1`. | 44–57 |
| `xa` | Gap AutoSense; moves media, does not enter dump mode. | 167 |
| `U` | Print configuration. | 148 |

For the 2824/2844 speed table: code 1 = 1.5 ips, 2 = 2 ips, 3 = 2.5 ips,
4 = 3.5 ips. Do not reuse the GK/GX speed table. The interface exposes speed
**codes** and explains the mapping.

The native 50 × 30 mm fixture has width 400 dots, height 240 dots and gap 24 dots.
Its Code 128 is `123456789012`; it is deliberately ASCII. Normal user labels use
the raster path so Turkish Unicode and QR do not depend on firmware features.

## Binary contract

Each row is `ceil(widthDots / 8)` bytes. Rows run top to bottom, bits run MSB
first, **0 means black, 1 means white**, and unused right-hand bits are white.
The payload has exactly `rowBytes × heightDots` bytes. LF, NUL and 0xff inside it
are image data, not delimiters. A final newline precedes the `P` command.

Never read/export an EPL raster file through a text encoder, `Out-File`, or a
text editor that may rewrite line endings. Send bytes unchanged. Native A/B
diagnostics are ASCII text; the GW fixtures are binary despite their `.epl` suffix.

## Software verification

Tests compare exact command framing and packed bit patterns, including white
padding and embedded LF/NUL bytes. All six symbologies are encoded and decoded
with ZXing to verify content. The WPF print preflight also decodes each barcode
crop from the final composite raster, rejecting overlays or rendering damage that
prevent decoding. This is software evidence, not an ISO barcode grade or a test of
the printer's thermal head, media, transport or firmware.

`examples/epl2` contains a native test plus six raster tests on **64 × 40 mm,
3 mm gap** stock. PBM files are lossless visual counterparts; PBM polarity is
opposite to GW. Do not send PBM files to the printer. Each raster fixture prints
one barcode without a caption, so identify the file and expected value in the
hardware form before sending it. All provided fixtures use direct thermal mode;
for thermal transfer use the app's media setting and export a fresh EPL file.

## Transport semantics

The RAW path uses OpenPrinter → StartDocPrinter(`RAW`) → StartPagePrinter →
checked/chunked WritePrinter → EndPagePrinter → EndDocPrinter. On failure it
attempts AbortPrinter and does not retry. A job ID proves Windows acceptance;
it does not acknowledge physical output. Enumeration and status come from the
Windows spooler, not raw USB discovery or a bidirectional EPL status channel.

The driver path separately uses WPF/XPS; it never sends XPS bytes to WritePrinter.
It checks the accepted print ticket and imageable area and uses 100% scale.

## Required physical verification

Physical printer testing was **not run** in the Linux build environment. Use
`scripts/Test-Printer.ps1` on the target Windows computer, inspect and scan the
labels, and record the driver/firmware/stock in `HARDWARE_ACCEPTANCE.md`. Do not
change that status based solely on a unit test or spool job ID.
