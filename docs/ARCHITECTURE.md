# Zebra TLP 2844 Label Designer architecture

Status: implementation baseline, 31 October 2026. License: GPL-3.0-only.

## Runtime boundaries

- **EtiketStudio.Core**: platform-neutral label/product/settings models, validation,
  barcode matrices, EPL2 byte generation, SQLite repositories, CSV/XLSX exchange.
- **EtiketStudio.Windows**: WPF/MVVM desktop composition, interactive millimetre
  canvas, 8-dot/mm raster rendering, Windows spooler and driver integration.
- **EtiketStudio.Tests**: executable automated tests of the portable domain,
  persistence, file interchange, barcode decoding and printer command bytes.
- **scripts**: reproducible Windows release publishing and opt-in hardware checks.

## First printing milestone

Enumerate installed Windows queues, select the EPL TLP 2844 queue, validate label
geometry and barcode content, preview an immutable print snapshot, then submit
actual binary EPL2 with OpenPrinter / StartDocPrinter / WritePrinter. Report the
Windows job ID. Spool acceptance is never reported as confirmed paper output.
No automatic retry after ambiguous submission: that could duplicate labels.

## Rendering and printer protocol

Use the original **TLP 2844 EPL2**, not the **2844-Z**. The printer uses 8 dots/mm
(nominal 203 dpi), a maximum 832-dot printable width, and conservative 279 mm
maximum label length for the base model. The normal pipeline renders the whole
label at printer resolution, including Turkish text, images and QR codes, then
sends the monochrome bitmap with EPL2 GW. This avoids dependence on firmware QR
extensions or resident Turkish font code pages. GW uses binary bytes, MSB first,
with 0 = black and 1 = white, including white padding bits.

The native diagnostic label separately exercises EPL2 A and B commands. Output
contains no ZPL. Calibration is the documented EPL2 xa command. Darkness and speed
are validated before submission. The alternate WPF driver path uses physical
page dimensions and rejects incompatible print capabilities instead of silently
scaling the barcode. Device-specific speed/darkness settings for that path belong
to the driver. App settings apply directly to RAW EPL2.

## Data and user interface

SQLite is located under the current user's LocalApplicationData/EtiketStudio.
Parameterized SQL, transactional product imports, integer minor-unit prices,
schema versioning, and a native backup API protect offline data. Template JSON
embeds image bytes so templates do not depend on external image paths. Settings,
draft label and print submission history survive restarts. Unsent jobs are never
automatically replayed. Imported product images are stored as bytes.

View models expose commands and observable state. Views own only input gestures,
layout and rendering. Core services never depend on WPF. Turkish is the neutral
resource language; English resources use the same keys. Both language catalogs are embedded in the main assembly. An observable catalog
refreshes bound screen text immediately. The app saves its language separately
from unrelated numeric settings. Themes change immediately.

## Validation gates

1. Cross-platform unit/integration tests and Windows cross-compilation.
2. Windows WPF rendering smoke tests, explicitly reported as not run on Linux.
3. Physical Windows 11 + USB TLP 2844 acceptance checklist, including scanning
   the printed barcode, Turkish text, QR, dimensions, disconnected USB, media-out,
   cancellation and restart behaviour. This gate requires the actual hardware.

No build or byte-level test is a substitute for gates 2 and 3.

## 0.1.2 preview and recovery

PrintPreparation classifies product-dependent fields independently of WPF. Preview
renders literal labels with no catalog product; dependent templates block until a
real product is selected. The preview window owns printer refresh and inline error
presentation. Every image is ready before ShowDialog returns a print decision.
The view model then uses those frozen images for the real transport.

RAW submissions alone expose the cancellation token. The synchronous XPS driver
path directs cancellation to the Windows queue. No cancellation is fabricated.

SQLite settings/draft saves and restores use transactions. Restore first snapshots
the chosen file, checks its schema/integrity and known data, then creates a safety
backup before replacing tables in one transaction. A successful restore marks the
view model to skip normal persistence during shutdown. Daily startup backups keep
seven automatic files; manual and safety backups are outside that retention rule.
