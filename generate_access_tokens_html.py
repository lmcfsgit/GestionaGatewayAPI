#!/usr/bin/env python3
import argparse
import json
from datetime import datetime, timezone
from html import escape
from pathlib import Path


DEFAULT_INPUT = Path("logs") / "AccessTokens.json"
DEFAULT_OUTPUT = Path("logs") / "AccessTokens.html"


def parse_args():
    parser = argparse.ArgumentParser(
        description="Render logs/AccessTokens.json as a readable HTML report."
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
    return parser.parse_args()


def read_tokens(path):
    with path.open("r", encoding="utf-8") as file:
        data = json.load(file)

    if not isinstance(data, dict):
        raise ValueError("Expected the JSON root to be an object.")

    return data


def format_authorization_date(value):
    if value is None or value == "":
        return ""

    text = str(value)
    try:
        timestamp = int(text)
    except ValueError:
        return escape(text)

    formatted = datetime.fromtimestamp(timestamp, tz=timezone.utc).strftime(
        "%Y-%m-%d %H:%M:%S UTC"
    )
    return f"{escape(text)}<br><span class=\"muted\">{formatted}</span>"


def collect_fields(tokens):
    fields = []
    for token in tokens.values():
        if isinstance(token, dict):
            for field in token.keys():
                if field not in fields:
                    fields.append(field)
    return fields


def render_html(tokens):
    fields = collect_fields(tokens)
    rows = []

    for object_id, token in sorted(tokens.items(), key=lambda item: str(item[0])):
        if not isinstance(token, dict):
            token = {"value": token}

        cells = [f"<td class=\"object-id\">{escape(str(object_id))}</td>"]
        for field in fields:
            value = token.get(field, "")
            if field == "authorization_date":
                rendered_value = format_authorization_date(value)
            else:
                rendered_value = escape("" if value is None else str(value))
            cells.append(f"<td>{rendered_value}</td>")

        rows.append(f"<tr>{''.join(cells)}</tr>")

    header_cells = ["<th>Object ID</th>"] + [
        f"<th>{escape(field)}</th>" for field in fields
    ]

    return f"""<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Access Tokens</title>
  <style>
    :root {{
      color-scheme: light;
      font-family: Segoe UI, Arial, sans-serif;
      background: #f6f8fb;
      color: #1f2937;
    }}
    body {{
      margin: 0;
      padding: 32px;
    }}
    main {{
      max-width: 1280px;
      margin: 0 auto;
    }}
    h1 {{
      margin: 0 0 6px;
      font-size: 28px;
      font-weight: 650;
    }}
    .summary {{
      margin: 0 0 22px;
      color: #596579;
    }}
    .table-wrap {{
      overflow-x: auto;
      background: #ffffff;
      border: 1px solid #d9e0ea;
      border-radius: 8px;
      box-shadow: 0 1px 2px rgba(16, 24, 40, 0.05);
    }}
    table {{
      width: 100%;
      border-collapse: collapse;
      min-width: 900px;
    }}
    th,
    td {{
      padding: 12px 14px;
      border-bottom: 1px solid #e6ebf2;
      text-align: left;
      vertical-align: top;
      font-size: 14px;
      line-height: 1.35;
    }}
    th {{
      position: sticky;
      top: 0;
      z-index: 1;
      background: #eef3f8;
      color: #344054;
      font-weight: 650;
    }}
    tr:last-child td {{
      border-bottom: 0;
    }}
    td {{
      word-break: break-word;
    }}
    .object-id {{
      font-family: Consolas, Monaco, monospace;
      font-weight: 650;
      white-space: nowrap;
    }}
    .muted {{
      color: #667085;
      font-size: 12px;
    }}
  </style>
</head>
<body>
  <main>
    <h1>Access Tokens</h1>
    <p class="summary">{len(tokens)} object(s) loaded from AccessTokens.json.</p>
    <div class="table-wrap">
      <table>
        <thead>
          <tr>{''.join(header_cells)}</tr>
        </thead>
        <tbody>
          {''.join(rows)}
        </tbody>
      </table>
    </div>
  </main>
</body>
</html>
"""


def main():
    args = parse_args()
    tokens = read_tokens(args.input)
    html = render_html(tokens)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(html, encoding="utf-8")

    print(f"Wrote {len(tokens)} object(s) to {args.output}")


if __name__ == "__main__":
    main()
