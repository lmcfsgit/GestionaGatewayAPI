#!/usr/bin/env python3
import argparse
import csv
import json
from html import escape
from pathlib import Path
from urllib.parse import urlparse


DEFAULT_INPUT = Path("logs") / "PendingTokens.json"
DEFAULT_OUTPUT = Path("logs") / "PendingTokens.html"


def parse_args():
    parser = argparse.ArgumentParser(
        description="Render logs/PendingTokens.json as an HTML table."
    )
    parser.add_argument(
        "-i",
        "--input",
        type=Path,
        default=DEFAULT_INPUT,
        help=f"JSON file to read. Default: {DEFAULT_INPUT}",
    )
    parser.add_argument(
        "-o",
        "--output",
        type=Path,
        default=DEFAULT_OUTPUT,
        help=f"HTML file to create. Default: {DEFAULT_OUTPUT}",
    )
    parser.add_argument(
        "-c",
        "--csv-output",
        type=Path,
        help="CSV file to create. Default: the HTML output path with a .csv extension.",
    )
    return parser.parse_args()


def read_tokens(path):
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)

    if not isinstance(data, dict):
        raise ValueError("Expected the JSON root to be an object.")

    return data


def collect_fields(tokens):
    fields = []
    for token in tokens.values():
        if isinstance(token, dict):
            for field in token:
                if field not in fields:
                    fields.append(field)
    return fields


def render_value(field, value):
    text = "" if value is None else str(value)
    safe_text = escape(text)

    if field.lower() in {"uri", "url"}:
        parsed = urlparse(text)
        if parsed.scheme in {"http", "https"}:
            return (
                f'<a href="{escape(text, quote=True)}" target="_blank" '
                f'rel="noopener noreferrer">{safe_text}</a>'
            )

    return safe_text


def render_html(tokens):
    fields = collect_fields(tokens)
    rows = []

    for object_id, token in sorted(tokens.items(), key=lambda item: str(item[0])):
        if not isinstance(token, dict):
            token = {"value": token}

        cells = [f'<td class="object-id">{escape(str(object_id))}</td>']
        cells.extend(
            f"<td>{render_value(field, token.get(field, ''))}</td>"
            for field in fields
        )
        rows.append(f"<tr>{''.join(cells)}</tr>")

    header_cells = ["<th>user_id</th>"] + [
        f"<th>{escape(field)}</th>" for field in fields
    ]

    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Pending Tokens</title>
  <style>
    :root {{
      font-family: "Segoe UI", Arial, sans-serif;
      color: #1f2937;
      background: #f6f8fb;
    }}
    body {{ margin: 0; padding: 32px; }}
    main {{ max-width: 1280px; margin: 0 auto; }}
    h1 {{ margin: 0 0 6px; font-size: 28px; }}
    .summary {{ margin: 0 0 22px; color: #596579; }}
    .table-wrap {{
      overflow-x: auto;
      background: #fff;
      border: 1px solid #d9e0ea;
      border-radius: 8px;
      box-shadow: 0 1px 2px rgba(16, 24, 40, 0.05);
    }}
    table {{ width: 100%; border-collapse: collapse; min-width: 900px; }}
    th, td {{
      padding: 12px 14px;
      border-bottom: 1px solid #e6ebf2;
      text-align: left;
      vertical-align: top;
      font-size: 14px;
      line-height: 1.35;
    }}
    th {{ background: #eef3f8; color: #344054; font-weight: 650; }}
    tr:last-child td {{ border-bottom: 0; }}
    td {{ overflow-wrap: anywhere; }}
    .object-id {{
      font-family: Consolas, Monaco, monospace;
      font-weight: 650;
      white-space: nowrap;
    }}
    a {{ color: #175cd3; }}
  </style>
</head>
<body>
  <main>
    <h1>Pending Tokens</h1>
    <p class="summary">{len(tokens)} object(s) loaded from {escape(DEFAULT_INPUT.name)}.</p>
    <div class="table-wrap">
      <table>
        <thead><tr>{''.join(header_cells)}</tr></thead>
        <tbody>{''.join(rows)}</tbody>
      </table>
    </div>
  </main>
</body>
</html>
"""


def write_csv(tokens, path):
    fields = collect_fields(tokens)

    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as file:
        writer = csv.writer(file)
        writer.writerow(["user_id", *fields])

        for object_id, token in sorted(tokens.items(), key=lambda item: str(item[0])):
            if not isinstance(token, dict):
                token = {"value": token}

            writer.writerow(
                [str(object_id)]
                + [
                    "" if token.get(field) is None else str(token.get(field, ""))
                    for field in fields
                ]
            )


def main():
    args = parse_args()
    tokens = read_tokens(args.input)
    html = render_html(tokens)
    csv_output = args.csv_output or args.output.with_suffix(".csv")

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(html, encoding="utf-8")
    write_csv(tokens, csv_output)
    print(f"Wrote {len(tokens)} object(s) to {args.output} and {csv_output}")


if __name__ == "__main__":
    main()
