# Requested capability map

| Request | Implementation / delivery | Verification boundary |
| --- | --- | --- |
| C#, current LTS .NET, WPF, MVVM | Four-project solution, .NET 10 LTS; views, view model and independent services | Compiled |
| SQLite, offline, Windows 11 | Per-user SQLite, self-contained Windows x64 app; no runtime web calls | SQLite tests; Windows execution pending |
| GPL-3.0 | GPL-3.0-only LICENSE and dependency notices | Included |
| TLP 2844 USB, EPL2 | Installed USB queue via real Winspool RAW path; strict EPL model check | Protocol tests; hardware pending |
| Windows-driver print | WPF/XPS print ticket, physical size/capability validation | Compiled; Windows test pending |
| Discover/select printer, remember last | Installed queue enumeration; SQLite setting | Real Windows implementation; hardware pending |
| Density, speed, size, orientation | D0–15, S1–4, mm/8-dot grid, raster 180° rotation | Portable validation tested; physical pending |
| Calibration, native test, queue/errors | xa, native A/B, U, EnumJobs, SetJob, Turkish errors and job history | Byte tests; Windows integration pending |
| Visual editor | Drag/drop toolbox, move, numeric resize, snap, layers, undo/redo | XAML compiled; interactive acceptance pending |
| All listed object kinds | Text, barcode, QR, line, rectangle, image, price, product name/SKU, timestamp | Implemented; WPF rendering acceptance pending |
| Real-size preview | Exact monochrome print raster with monitor correction ruler | Compiled; physical measurement pending |
| All six barcode types | ZXing encode/decode, checksums, whole-dot module sizes and quiet zones | All six portable decode tests pass |
| Product catalog and images | CRUD, categories, description, binary images, search | SQLite tests pass |
| Excel and CSV | XLSX/CSV import/export; transactional SKU updates and row errors | Round-trip tests pass; XLS is outside scope |
| Batch/repeated/selected printing | Per-product quantities; all-catalog selection; preflight then preview | Logic implemented; actual queue tests pending |
| Turkish UI and characters | Turkish/English main-assembly resources, live language switching; WPF font rendering | Resource parity tests pass; visual text pending |
| Modern light/dark layout | Left navigation, center canvas, right properties | Compiled; visual acceptance pending |
| Source and Visual Studio solution | Complete application-owned source and .sln archive | Included |
| Executable / single-file EXE | Actual self-contained x64 PE executable in application ZIP | Published; launch pending |
| Turkish/English manuals and setup | Markdown plus offline HTML guides | Included |
| EPL2 files and templates | Native and binary raster fixtures; 3 templates; catalog examples | Generated and byte-tested; physical pending |

Release limits are documented in README. No physical or Windows integration test
is represented as complete merely because its implementation compiles.
