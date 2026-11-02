# Release verification report

Zebra TLP 2844 Label Designer 0.1.3 · 2 November 2026

**The code builds and the portable automated suite passes. Windows UI execution
and physical USB printing were not performed in this environment.** Treat the
release as a functional first build awaiting target-machine acceptance, not as
a hardware-certified production release.

## Environment

- Build host: Ubuntu 24.04 / Linux x64.
- .NET SDK: 10.0.403; runtime: 10.0.14 (LTS line).
- Desktop target: `net10.0-windows`, WPF, published for `win-x64`.
- Windows distribution: self-contained single application EXE with native library
  extraction; runtime download is not required for normal use.
- Printer attached to build host: none. Windows spooler on build host: unavailable.

## Results

| Verification | Result | Evidence |
| --- | --- | --- |
| Full Visual Studio solution build | PASS; 0 warnings, 0 errors | `test-results/build-0.1.3.log` |
| xUnit portable suite | PASS; 103 passed, 0 failed, 0 skipped | `test-results/release-0.1.3.trx` |
| Windows x64 publish | PASS | `test-results/publish-0.1.3.log`; PE32+ GUI executable |
| Turkish/English resource parity | PASS; 211 matching resource keys; no missing referenced keys | Static resource scan |
| XAML structure | PASS | XML parse and WPF XAML compilation |
| Example generation | PASS | Core fixture tool generated three templates, CSV/XLSX and EPL2 samples |
| Native Windows UI launch/layout | NOT RUN | Requires Windows; `scripts/Test-Windows.ps1` supplied |
| WPF final raster rendering and final-crop decode | NOT RUN on Windows | Windows self-test supplied; core decoder tested separately |
| Winspool P/Invoke and Windows driver/XPS submission | NOT RUN on Windows | Compiled real implementations; requires Windows queue |
| TLP 2844 USB physical label output | NOT RUN | Requires physical printer, correct driver and stock |
| Physical scanner readability, dimensions, media errors | NOT RUN | Use `docs/HARDWARE_ACCEPTANCE.md` |

## What the automated suite checks

- Correct retail check digits for EAN-13, EAN-8 and UPC-A; rejection of incorrect
  supplied checksums, unsupported characters, control characters and empty values.
- Independent encode/decode round trips for all six symbologies, including Turkish
  UTF-8 QR content; blank raster rejection; QR quiet zones and minimum module sizes.
- Exact 8-dot/mm conversion, label bounds, template schema, gap constraints,
  immutable cloning, and Turkish field substitution.
- Exact EPL2 header/payload/trailer bytes; native Code 128 syntax; calibration and
  configuration commands; pixel polarity, bit order, white padding, embedded LF,
  NUL and 0xff; quantity, speed, darkness and raster-size checks.
- Short-write offsets, zero/impossible write rejection and cancellation before
  the next chunk. These test the shared byte pump, not the Windows DLL itself.
- SQLite CRUD, integer-cent prices, image bytes, persistence, duplicate SKU errors,
  upsert identity, validation before import, transaction rollback after a database
  failure, print history and a WAL-consistent backup.
- CSV/XLSX Turkish text, multiline/quoted CSV, decimal separators, leading zeros,
  Base64 images, malformed CSV rejection, Excel formula rejection, explicit Excel
  text export, numeric price precision and both UI resource languages.

## Limits relevant to release decisions

The application can inspect installed queues and the status reported by their
drivers. It does not query USB hardware directly or receive EPL acknowledgements
from the printer. A disconnected legacy USB printer may still have an apparently
ready queue. The UI therefore reports “submitted”, never “physically printed”.

Font metrics, WPF templates, actual driver capabilities, runtime native extraction,
firmware interpretation and thermal output need target-Windows testing. The
included self-test produces local PNGs and a result file without accessing any
printer. The hardware script submits bytes only on an explicit `-Print` request
and records `physicalPrintVerified: false` until an operator observes output.

The original TLP 2844 is discontinued. No current official Windows 11 certification
is asserted for the user's unknown legacy driver. The release includes neither a
driver installer nor firmware. The app is unsigned. The build artifacts and source
have SHA-256 digests in the delivered checksum files.

## 0.1.1 localization regression checks

Nine added tests cover explicit English on a Turkish thread, live indexer and revision notifications, main-assembly English resource inclusion, complete key parity, independent catalogs, supported code normalization and saved-language persistence without changing products. All 81 portable tests passed. The Windows self-test now also checks that changing the view-model language updates actual tab headers and persists the selection. That WPF self-test still requires execution on Windows; it was not run on this Linux host.

The exact failure on the user's Windows installation was not directly reproduced. This patch removes the dependence on ambient Windows UI culture and satellite-resource deployment and implements immediate UI refresh.

## 0.1.2 workflow and recovery regression checks

21 added portable test cases cover product-token detection, static labels without
catalog data, product snapshots, WAL-consistent restore, preservation of safety
backups, rejection of foreign/corrupt databases and invalid drafts, rollback after
a database constraint failure, seven-backup retention and atomic session validation.
All 102 portable tests pass. The Windows-only smoke test adds actual WPF static-label
rendering, missing-product guards, product preview and layout at smaller logical
window sizes. It is supplied but NOT RUN on this Linux host. Window-size checks
are not a substitute for real 125%/150% Windows display-scale acceptance.

Driver cancellation remains through the Windows queue; this release does not claim
to cancel a synchronous XPS Write operation from the app's RAW cancellation button.

## 0.1.3 release verification

103 automated tests passed. The startup-language regression covers clean installs, upgrades from an older Turkish setting and explicit selection after upgrade. The Windows-x64 package was cross-compiled, and its executable and launcher paths were checked inside the single release folder. Physical printing and Windows UI execution remain pending.

## 0.1.4 verification

The renamed application builds without warnings or errors. All 103 portable tests pass. Physical TLP 2844 output and Windows GUI execution still require target-machine acceptance.
