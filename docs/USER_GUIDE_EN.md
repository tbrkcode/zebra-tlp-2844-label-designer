# TBRK Label Studio user guide

Version 0.1.3 · Windows 11 x64 · English guide

## Start and first label

Extract the complete application ZIP and run `TBRKLabelStudio.exe`. The bundled
.NET runtime means you do not need to install .NET separately. Keep the `docs`
and `examples` folders beside the EXE. Normal operation is offline.

The first launch uses English. For Turkish, open **App settings** and select **Türkçe**. The app applies your choice immediately and remembers it.

First follow the printer setup guide. In **Printer settings**, choose the installed
EPL TLP 2844 queue. For direct thermal labels without a ribbon, enable **Direkt
termal (ribonsuz)**. For thermal transfer stock with a ribbon, disable it. Start
with darkness 8 and speed code 2. Load 50 × 30 mm gap stock with a 3 mm gap before
running the native test button. It submits one real Code 128 label. Scan the output:
the expected value is `123456789012`.

## Products

Open **Products**. Click **New**, enter a product name and SKU, then optionally a
barcode, price, category, description and image. Click **Save**. SKU must be
unique. Prices are non-negative, with at most two decimal places. Select an
existing row to edit it; click **Save** before selecting another row. **Delete**
asks before removing a product. Product deletion does not delete reusable label
templates. The search field matches product name, SKU, barcode and category.

PNG, JPEG and BMP images are embedded in the local database, up to 5 MB and
4096 × 4096 pixels. The label image object uses its own attached image first,
otherwise the selected product image. Images print in black and white.

## CSV and Excel

Use **Import** or **Export** for `.csv` and `.xlsx`. Legacy `.xls` is not
supported; save it as `.xlsx` first. The first worksheet of an Excel file is read.
Example files are supplied in `examples/products.csv` and `examples/products.xlsx`.

| Column | Meaning | Required |
| --- | --- | --- |
| Sku | Unique stock code, stored as text | Yes |
| Name | Product name | Yes |
| Barcode | Barcode content; keep it as text to retain leading zeros | Yes, may be blank until a barcode is printed |
| Price | Decimal price, e.g. `12.50` or `12,50`, without grouping separators | Yes |
| Description | Description | No |
| Category | Category | No |
| ImageBase64 | Embedded image encoded as Base64 | No |

Turkish headers `Stok kodu`, `Ürün adı`, `Barkod`, `Fiyat`, `Açıklama`, `Kategori`
are also accepted. CSV supports UTF-8, BOM, comma/semicolon separators, quoted
fields, escaped quotes and multiline descriptions. Excel exports store identifiers
as text. A numeric Excel cell cannot recover leading zeros already discarded by
Excel; format barcodes as text before entry. Convert formulas to values before
importing. For exchanging untrusted formula-like text, prefer the XLSX exporter,
which writes text cells explicitly; CSV readers may interpret such text as formulas.

Import validates the complete file before changing products. Existing matching
SKUs are updated; new SKUs are added. A failed transaction leaves no partial import.
Duplicate SKUs within one input file are rejected. The row limit is 10,000. XLSX
cells cannot hold large Base64 images; use CSV or smaller images when notified.

## Design labels

Open **Label designer**. Set template name, width, height and gap. Dimensions
refer to the label face; the gap is separate. Three templates are included under
`examples/templates`. Use **Open** to load one and **Save** to save a `.etiket.json`.
Images attached to templates travel inside the JSON file.

Drag a tool from the left onto the label, or double-click it. Click an object to
select it; drag to move it. The right panel edits X/Y and size in millimetres,
content, font, point size, bold, barcode type, stroke and readable barcode text.
Use **Duplicate**, **Delete**, **Bring to front** and **Send to back** for object/layer changes.
Arrow keys move a selected canvas object by 0.5 mm; Delete removes it. Ctrl+Z and
Ctrl+Y undo/redo design changes. Optional snapping uses a 0.5 mm step.

Built-in fields: product name, SKU, price and computer date/time. In text/barcode
content you may use `{Name}`, `{Barcode}`, `{Sku}`, `{Price}`, `{Category}`,
`{Description}`, `{Date}` and `{Time}`. Price formatting is Turkish lira. All
products in one print operation use one timestamp captured before preview.

Code 128 accepts printable ASCII. Code 39 accepts uppercase letters, digits and
its documented punctuation. QR accepts Turkish UTF-8 text. Retail barcodes accept
the full number or a number missing only its check digit. Incorrect full check
digits are rejected. Missing check digits are completed in the encoded output;
the product catalog is not silently rewritten. Use **Barcode tools** to inspect
the normalized number and apply it to a selected barcode object.

Red objects indicate invalid or overflowing content. Correct them before printing.
The engine rejects objects outside the label, text that does not fit, and barcodes
too small to preserve whole printer dots and quiet zones. It never squeezes a
barcode to force it into a narrow field. Leave clear space around every barcode;
avoid overlapping other objects onto its white border.

## Preview and print

Set the quantity and click **Print preview / Baskı önizleme**. Static text, literal
barcodes, embedded logos and dates do not require a catalog product. If the label
uses product fields, product images or tokens such as `{Barcode}`, select a real
product at the top of preview. A missing selection shows an explanation and blocks
printing and EPL2 export; editor demonstration values are never printed.

Select or refresh the printer in preview. Valid labels can be previewed and exported
without a printer. Print is enabled when rendering succeeds and a printer is selected.
Printer errors remain visible in preview so the selection can be corrected. Batch jobs use **Batch printing**: select products, set each row's
quantity, then preview. **Select all** selects the complete catalog, including an
imported Excel list. To print just imported rows, select those rows explicitly or
use a separate catalog/backup workflow; importing does not remove existing rows.

Preview validates every selected product before any printing. Browse the job with
**Önceki / Sonraki**. **Print** sends it to the selected queue; **Cancel** sends
nothing. Save the same image to PNG or the entire batch to binary EPL2 for later
diagnostics. The preview shows the actual monochrome pixels used for printing,
including the optional 180° rotation.

**Actual size** uses Windows logical display dimensions. Measure the 50 mm ruler
in preview with a physical ruler; enter `50 / measured millimetres` as **Ekran
boyut düzeltmesi** under app settings. This changes only display scaling. Printed
dimensions always use the printer's exact 8 dots/mm grid.

The default transport is EPL2 RAW. **Print through Windows driver** instead uses the
installed driver's XPS path. Set speed, darkness and media type in Windows printing
preferences for that path. A driver that cannot accept the requested paper size
and printable area is rejected instead of scaling the label.

## Queue, errors and recovery

A submitted job has a Windows job ID. Submission and disappearance from a queue
do not prove paper output. Check the physical label and scanner result. Legacy
USB drivers may not immediately report a disconnected cable, paper-out or an open
cover. The application reports the status Windows makes available.

Use **Refresh queue** to inspect the selected queue, and **Cancel selected job** to
cancel an TBRK Label Studio job. Windows may already have delivered some data; those
labels cannot be recalled. The bottom Stop submission button stops further RAW batch
submissions between synchronous Windows calls. Use the Windows queue for a driver
job; the app Stop submission button is disabled for that transport. Open Windows
queue remains available during submission. The app never automatically retries or replays history after a restart.

If a batch fails, inspect the spooler and already printed labels before retrying.
The submission history records accepted, pending and failed/uncertain operations.
A pending entry after a crash is not replayed automatically.

## Local data and backup

Data lives in `%LOCALAPPDATA%\EtiketStudio\etiketstudio.db`. Settings, products,
draft design and recent job records are local. **Back up database** takes a
consistent SQLite backup including committed WAL data. On the first launch each
day, an automatic backup is saved in `Backups`; the latest seven daily startup
backups are retained. These do not include later edits that day, so make a manual
backup after important changes.

Use **App settings → Restore backup** and select a `.db` file. Save any pending
product or design edits first. After confirmation the app validates a snapshot,
creates a `Backups/before-restore-...db` safety copy of current data and restores
all tables in one transaction. Invalid files and failed transactions leave current
data intact. The app closes after success: reopen it to load restored settings.
Manual and pre-restore backups are not automatically deleted.

If invalid settings prevent saving at exit, choose No to return and correct them,
or Yes to discard unsaved settings/draft edits. Unsaved product forms prompt
separately. Previously saved products are retained. On small or highly scaled
displays use the workspace scrollbars. Technical errors are logged to
`application.log`; review paths and product details before sharing logs.

See `TEST_REPORT.md` for the release's verification status and
`HARDWARE_ACCEPTANCE.md` for the target-computer checklist.

Open TBRKLabelStudio.exe inside the extracted TBRKLabelStudio-0.1.3 folder. This release starts in English on first launch. App settings remembers subsequent language choices.
