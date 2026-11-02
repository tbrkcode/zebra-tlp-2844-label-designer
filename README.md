# TBRK Label Studio

An offline, English-first Windows label designer for the **Zebra TLP 2844 EPL2**.
Written in C#/.NET 10 LTS, WPF and MVVM, with SQLite storage. GPL-3.0-only.

**Release 0.1.3 is a compiled functional first release, with hardware acceptance
pending.** The Windows x64 self-contained EXE is supplied. Automated core tests
and Windows cross-compilation were performed on Linux. The WPF window and a real
Windows 11/USB TLP 2844 were not available to run here. No physical print test is
claimed. See `docs/TEST_REPORT.md` for the exact evidence and limitations.

## Getting started

Extract the ZIP and open the **TBRKLabelStudio-0.1.3** folder. Run **TBRKLabelStudio.exe**. The first launch of this release uses English, including on upgrades from Etiket Studio. Later language changes in App settings are remembered. Your existing local product database is reused.

## 0.1.2 preview, printing and recovery

- Preview opens without a catalog product. Static text, literal barcodes, embedded logos and date fields can print independently.
- Product-dependent templates require an actual product selected in preview; sample editor values are never sent to the printer or exported as a print job.
- Choose and refresh Windows printers directly in preview. Offline/missing/unsupported printer errors stay visible in that window.
- Stop submission applies to RAW EPL2 only. Driver jobs are cancelled in the Windows queue, which remains accessible during submission.
- Invalid settings no longer trap the user on exit: correct them or explicitly discard unsaved settings/draft changes. Unsaved product forms prompt separately.
- Daily startup SQLite backups retain the latest seven automatic backups. Restore validates a snapshot, saves a safety backup, replaces data transactionally, and closes the app for reopening.
- Windows are fitted to the primary working area and the main workspace can scroll on small/high-scale displays.
- 102 portable automated tests pass. Windows UI execution and physical TLP 2844 printing still require target-machine acceptance.

## 0.1.1 language fix

Close the previous app, extract this complete update into a new folder and run
**English.cmd** to open directly in English. The title bar must say **0.1.3**.
The existing product database and templates are retained. You can also choose
**English** or **Türkçe** in App settings: changes apply immediately and save
automatically. English resources are embedded in the main assembly, and the saved
app language overrides the operating-system thread language.

## Run on Windows 11 x64

1. Extract the entire `TBRKLabelStudio-0.1.3-Windows11-x64.zip` archive into a writable folder.
2. Follow `docs/PRINTER_SETUP_TR.html` to install the correct EPL TLP 2844 driver.
3. Run `TBRKLabelStudio.exe`. A separate .NET runtime is not required. No internet
   connection, account or administrator rights are needed for normal operation.
4. In **Yazıcı ayarları**, select the TLP 2844 queue, choose the media type, and run
   the **50 × 30 mm EPL2 test baskısı** with matching stock loaded.
5. Read and scan the physical label. Only this observation confirms actual output.

The application is unsigned. No driver or proprietary Zebra software is bundled.
The original TLP 2844 is a discontinued printer; its installed driver and firmware
must be validated on the target computer. The **2844-Z is a different ZPL model**.

## Included functionality

- Real `winspool.drv` RAW submissions: enumeration, printer selection, job IDs,
  checked partial writes, abort-on-error, queue display and app-job cancellation.
- Alternate Windows driver/XPS printing with validated physical media size.
- Documented EPL2 `GW` binary image printing, `A`/`B` native diagnostic, `xa`
  calibration and `U` configuration print. No ZPL generator.
- Drag/drop label editor; millimetre positioning and sizing; text, product fields,
  price, date/time, barcode, QR, line, rectangle and embedded PNG/JPEG/BMP.
- Undo/redo, duplicate, layer order, grid snapping, real-size preview with monitor
  correction, and PNG/EPL2 export. Preview and printing share a monochrome raster.
- EAN-13, EAN-8, UPC-A, Code 128, Code 39 and QR validation. Missing retail check
  digits are completed; incorrect existing digits block printing.
- Product CRUD, images, search, CSV/XLSX import/export, per-product batch quantities,
  transactional imports, local backup, saved printer/settings/draft/history.
- Turkish and English resources, English default, immediate light/dark theme.

## Source layout

| Path | Responsibility |
| --- | --- |
| `TBRKLabelStudio.sln` | Visual Studio solution |
| `src/EtiketStudio.Core` | Domain, EPL2, barcode, SQLite and exchange services |
| `src/EtiketStudio.Windows` | WPF MVVM application, renderer and printer transports |
| `tests/EtiketStudio.Tests` | xUnit domain/integration/regression tests |
| `tools/EtiketStudio.Fixtures` | Reproducible example and EPL2 file generator; never prints |
| `examples` | Three templates, CSV/XLSX catalog and binary EPL2 fixtures |
| `scripts` | Windows build, rendering smoke test and explicit hardware-test harness |
| `docs` | Architecture, bilingual guides, protocol notes and test evidence |

## Build

Use Visual Studio with .NET 10 SDK support and the **.NET desktop development**
workload, or install the SDK version pinned in `global.json`. Open the solution
and choose `EtiketStudio.Windows` as the startup project.

```powershell
dotnet restore TBRKLabelStudio.sln
dotnet build TBRKLabelStudio.sln -c Release
dotnet test tests/EtiketStudio.Tests -c Release
.\scripts\Build-Windows.ps1
```

The build needs NuGet access unless packages and Windows runtime packs are already
cached. The resulting self-contained application runs offline. NuGet lockfiles
are included. `Build-Windows.ps1` publishes one runtime EXE plus optional manuals,
examples and required notices. Trimming is disabled for WPF/reflection safety.

## Release scope

This release supports single-column gap stock (gap at least 2 mm) or continuous
media (gap 0), width 25.4–104 mm and length 9.7–279 mm. It caps one submission at
1,000 labels and preview pixel storage at 128 MB; split larger jobs. Imports are
limited to 10,000 rows. Native speed codes are 1–4 and darkness 0–15.

Black-mark media, cutters, peel mode, multi-column layouts, GS1 application
identifier editing, per-object rotation and scheduled printing are outside this
release. Arbitrary font availability depends on installed Windows fonts. Automatic
retry is intentionally absent: a failed submission can have partially printed.

## Contributing and licensing

Keep UI text in the two resource files, business logic out of views, and all print
paths explicit about what was verified. Add regression tests for protocol/data
changes. Share the complete corresponding source when distributing this GPL
application. Runtime dependency licenses are in `THIRD_PARTY_NOTICES`. Submit a
hardware acceptance report before describing a device/driver combination as tested.
