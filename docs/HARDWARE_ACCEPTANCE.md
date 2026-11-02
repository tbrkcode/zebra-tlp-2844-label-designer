# Windows 11 + TLP 2844 physical acceptance record

**Initial status: NOT RUN.** No hardware result is supplied with this release.
The operator must complete this form from actual observations. Do not infer a
pass from Windows accepting or removing a job.

| Environment | Actual value |
| --- | --- |
| Operator and date | Not recorded |
| Windows 11 version/build and architecture | Not recorded |
| Physical model (must be TLP 2844, not -Z) | Not recorded |
| Serial / firmware from configuration label | Not recorded |
| Driver name and version | Not recorded |
| Queue name and USB port | Not recorded |
| Label dimensions, gap and stock type | Not recorded |
| Ribbon type, if used | Not recorded |
| Barcode scanner model | Not recorded |
| Application EXE SHA-256 | Not recorded |

## Non-printing Windows test

From the extracted application directory:

```powershell
.\scripts\Test-Windows.ps1
```

Inspect the generated PNGs and the result file. This tests WPF rendering; it sends
nothing to a printer. Also open all six app screens, edit each object type, test
light/dark themes, switch languages, save/reopen templates and restart the app.

## Native first-print milestone

Load 50 × 30 mm labels with a 3 mm gap. The bundled fixture uses direct thermal
stock. For a ribbon-based setup, use the native test button with the app's direct
thermal option cleared instead of sending the bundled direct-thermal fixture.

```powershell
.\scripts\Test-Printer.ps1 -PrinterName 'YOUR EXACT QUEUE NAME' -Print -WhatIf
.\scripts\Test-Printer.ps1 -PrinterName 'YOUR EXACT QUEUE NAME' -Print
```

The script asks before sending. It uses the application's real spooler path and
writes a JSON submission report with `physicalPrintVerified: false`. It does not
write an invented hardware pass. Scan the actual label and record the result below.

## Acceptance matrix

| Test | Expected result | Observed / pass-fail |
| --- | --- | --- |
| Native A/B test | One 50 × 30 label, heading ETIKET STUDIO, scan `123456789012` | NOT RUN |
| Physical size | Label layout measured against mm positions; no driver scaling | NOT RUN |
| Turkish text | `ç ğ ı İ ö ş ü Ç Ğ I İ Ö Ş Ü` printed without substitution | NOT RUN |
| EAN-13 raster | Scan `4006381333931` | NOT RUN |
| EAN-8 raster | Scan `96385074` | NOT RUN |
| UPC-A raster | Scan `036000291452` (scanner UPC/EAN reporting mode recorded) | NOT RUN |
| Code 128 raster | Scan `ABC-123456` | NOT RUN |
| Code 39 raster | Scan `ABC-123` | NOT RUN |
| QR raster | Scan `çğıİöşü / TBRK Label Studio` exactly | NOT RUN |
| Three identical copies | Exactly 3 correct labels | NOT RUN |
| Mixed batch | Each selected product and per-row quantity matches preview | NOT RUN |
| Calibration on gap stock | Feed aligns to the next label; no skipped labels afterward | NOT RUN |
| Darkness and speed | Record selected codes and usable visual/scanner output | NOT RUN |
| 180-degree orientation | Preview and physical output agree | NOT RUN |
| Windows driver path | Correct physical size; rejection if driver cannot represent media | NOT RUN |
| Unplug USB before submission | Observe actual driver status; never claim physical success | NOT RUN |
| Paper/ribbon out or cover open | Record actual spool status and recovery; no automatic retry | NOT RUN |
| Cancel a queued job | Correct application job cancelled; record any already printed labels | NOT RUN |
| Restart | Saved queue/settings/draft persist; old jobs are not resent | NOT RUN |
| Offline operation | Product CRUD, template editing and printing without internet | NOT RUN |

For raster tests, load 64 × 40 mm labels with a 3 mm gap and pass the corresponding
file with `-EplFile`. Inspect one result at a time. Do not mix stock sizes or send
all files automatically; calibration/configuration files intentionally feed media.

Attach photos/scanner output and the submission JSONs to your locally retained
report. Mark this build accepted only after the required rows have actual results.

## 0.1.2 target-Windows UI acceptance (no printer needed for steps 1–6)

1. Close older versions and launch 0.1.2. Switch Türkçe → English → Türkçe; reopen and verify the saved language.
2. Open preview with no selected product and a static-text label. Verify pixels appear and PNG export works without a printer.
3. Open a product-dependent template without selecting a product. Verify the explanation and disabled Print/EPL export. Select a saved product and verify its actual values.
4. Enter invalid numeric text or an out-of-range setting. Close: No returns to editing; Yes exits without saving invalid settings/draft. Saved products remain.
5. Back up disposable test data, modify it, restore the backup, reopen and verify products, draft, settings and history. Verify the before-restore safety copy contains the newer data. Never use the only copy of production data for acceptance.
6. At 125% and 150% Windows display scale, test every tab, the main scrollbars and preview controls. Repeat in both themes and languages.
7. Select/refresh the TLP 2844 inside preview, print one static label, then one product label. Check size, Turkish glyphs and scanner decoding.
8. Test RAW Stop submission; test driver cancellation in Windows queue. Verify the queue can be opened during submission and app RAW cancellation is disabled for driver mode.
9. Test a disconnected USB cable and missing media. Confirm inline error handling where the driver reports errors; a submitted job is never treated as physical proof.

These steps have NOT been executed by the build host. Record actual observations,
driver name/version, firmware, label dimensions and scanner results here.
