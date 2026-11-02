#!/usr/bin/env python3
# SPDX-License-Identifier: GPL-3.0-only
"""Render the offline manual pages. Development dependency: pip install markdown==3.10.2."""
from pathlib import Path
import html
import markdown

root = Path(__file__).resolve().parents[1]
css = '''body{margin:0;background:#f3f6fa;color:#182536;font:16px/1.65 system-ui,Segoe UI,sans-serif}header{background:#152333;color:#fff;padding:28px max(24px,calc((100vw - 980px)/2))}header b{font-size:24px;letter-spacing:2px}header small{display:block;color:#8ee2cd}nav{display:flex;gap:18px;flex-wrap:wrap;margin:16px 0 0}nav a{color:#d8f1e8;font-size:13px;text-decoration:none}main{max-width:920px;margin:26px auto 60px;background:white;border:1px solid #dce3ec;border-radius:14px;padding:30px 40px;box-shadow:0 8px 30px #15233306}h1{font-size:32px;line-height:1.2;margin-top:0}h2{margin-top:34px;font-size:22px;color:#087f70}h3{font-size:18px}a{color:#087f70}table{border-collapse:collapse;width:100%;font-size:14px;display:block;overflow:auto}td,th{border:1px solid #dce3ec;padding:10px 12px;text-align:left;vertical-align:top}th{background:#edf5f3}tr:nth-child(even){background:#f8fafb}code{background:#edf1f5;padding:2px 5px;border-radius:4px;font-size:.9em;overflow-wrap:anywhere}pre{background:#152333;color:#ecf6f2;padding:18px;border-radius:8px;overflow:auto}pre code{background:transparent;color:inherit;padding:0;white-space:pre}blockquote{border-left:4px solid #087f70;margin-left:0;padding-left:18px}li{margin:5px 0}footer{color:#5c6e82;font-size:12px;border-top:1px solid #dce3ec;margin-top:40px;padding-top:16px}@media(max-width:700px){main{margin:12px;padding:22px}h1{font-size:26px}}@media print{body{background:white;font-size:11pt}header,nav{display:none}main{max-width:none;margin:0;padding:0;border:0;box-shadow:none}h2{break-after:avoid}tr{break-inside:avoid}a{color:inherit}pre{white-space:pre-wrap}table{display:table}}'''
nav = [('USER_GUIDE_TR','Türkçe'),('USER_GUIDE_EN','English'),('PRINTER_SETUP_TR','Yazıcı kurulumu'),('PRINTER_SETUP_EN','Printer setup'),('TEST_REPORT','Test report'),('HARDWARE_ACCEPTANCE','Hardware acceptance'),('ARCHITECTURE','Architecture')]
for path in sorted((root/'docs').glob('*.md')):
    text=path.read_text(encoding='utf-8')
    content=markdown.markdown(text, extensions=['tables','fenced_code','toc'])
    title=text.splitlines()[0].lstrip('# ')
    language='tr' if path.stem.endswith('_TR') else 'en'
    links=''.join(f'<a href="{stem}.html">{label}</a>' for stem,label in nav)
    page=f'<!doctype html><html lang="{language}"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{html.escape(title)}</title><style>{css}</style></head><body><header><b>ZEBRA TLP 2844 LABEL DESIGNER</b><small>Desktop label design · TLP 2844 · EPL2</small><nav>{links}</nav></header><main>{content}<footer>Zebra TLP 2844 Label Designer 0.1.4 · GPL-3.0-only · Offline documentation · 31 October 2026</footer></main></body></html>'
    path.with_suffix('.html').write_text(page,encoding='utf-8')
print('Offline HTML manuals generated.')
