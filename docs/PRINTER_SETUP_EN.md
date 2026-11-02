# Zebra TLP 2844 USB setup

Confirm the physical printer is **TLP 2844**, not **TLP 2844-Z**. The original
model uses EPL2; the -Z model is a different ZPL device. The application checks
the installed driver model before RAW submission, but cannot identify firmware
over a one-way Windows USB queue.

The printer is discontinued. A successful application build does not certify
legacy drivers on Windows 11. Install a manufacturer-supplied, appropriately
signed driver that includes the EPL TLP 2844 model. Zebra instructs users to
install the driver before connecting USB. Use a short USB cable directly to the
computer for initial tests. Confirm the installed queue and its actual USB port
in Windows printer settings; the application does not assume USB001.

Load 50 × 30 mm labels with a 3 mm gap for the native test. Select the queue in
Zebra TLP 2844 Label Designer. Enable direct thermal for heat-sensitive stock without a ribbon;
disable it for thermal transfer stock with a ribbon. Start at darkness 8, speed
code 2. Calibrate gapped media with the dedicated `xa` button. Do not use gap
calibration for continuous stock; set gap 0 for that stock. Black-mark stock is
outside this release's supported scope.

The native test sends one real EPL2 label, using `A` for the heading and `B` for
Code 128. Expected scan: `123456789012`. Then print the default design with a
saved product and verify Turkish text. Finally test the QR template and scan its
UTF-8 content. Check physical dimensions using a ruler.

Normal labels use the documented binary `GW` graphics command. QR and Turkish
text are rendered by the computer; no firmware QR extension or printer-resident
Turkish code page is assumed. No ZPL commands are generated.

For the alternative Windows driver path, set media, speed and darkness in Windows
printing preferences. The application requests and validates the physical page
size; it refuses incompatible page sizes or imageable margins rather than shrinking
barcodes. Start with RAW EPL2 for this printer.

Windows may accept a job while USB is unplugged. A disappearing queue entry does
not establish that a label printed. Check media, cover, ribbon, port, pending jobs
and the actual output. Do not automatically retry an uncertain submission.
Complete `HARDWARE_ACCEPTANCE.md` for your exact device, driver and Windows build.

Primary sources:

- [Zebra USB guidance](https://support.zebra.com/article/000016564)
- [Zebra configuration print guidance](https://support.zebra.com/article/000015691)
- [Zebra EPL programming guide](https://www.zebra.com/content/dam/support-dam/en/documentation/unrestricted/guide/software/epl2-pm-en.pdf)
- [Microsoft RAW datatype](https://learn.microsoft.com/windows-hardware/drivers/print/raw-data-type)

Reviewed 31 October 2026. Proprietary driver packages are not bundled.
