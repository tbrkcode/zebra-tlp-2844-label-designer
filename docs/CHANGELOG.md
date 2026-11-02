# Changelog

## 0.1.3

- Rename the application to TBRK Label Studio.
- Start in English on first launch or upgrade and remember subsequent language selections.
- Place the application and its support files in a single folder inside the ZIP.

## 0.1.2 — 31 October 2026

- Open preview without a selected product; allow static labels and require real data for product-dependent labels.
- Select products and printers in preview; refresh printer list and show errors inline.
- Keep sample editor data out of print/export payloads. Preserve one timestamp and render all batch labels before submission.
- Make RAW-only cancellation explicit and keep the Windows queue accessible during driver submission.
- Offer discard-and-exit after validation failures and warn about unsaved product forms.
- Save settings and draft atomically in one SQLite transaction.
- Add daily startup backups with seven-file retention, validated transactional restore and mandatory pre-restore safety copies.
- Prevent multiple new-version app instances and avoid overwriting restored state during shutdown.
- Fit windows to the work area; allow main-workspace scrolling on smaller displays.
- 102 portable automated tests pass. Windows UI and physical USB printing are still NOT RUN here.

## 0.1.1 — 31 October 2026

- Apply the selected app language explicitly, independently of the Windows UI thread culture.
- Embed English resources in the main application assembly instead of relying on satellite deployment.
- Update labels, menus, grid headings and object type names immediately when the language changes.
- Display English and Türkçe as language choices and automatically persist the selection.
- Add English.cmd and Turkce.cmd launchers, plus an optional --language switch.
- Display 0.1.1 in the window title so the updated executable can be identified.
- Retain the existing SQLite products, templates and settings; no database reset or schema migration.
- 81 automated core tests pass. Windows UI execution and real printer testing remain pending.
