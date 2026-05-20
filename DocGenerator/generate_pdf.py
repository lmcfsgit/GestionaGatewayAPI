from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Preformatted,
    KeepTogether, CondPageBreak, PageBreak
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.pagesizes import letter
from reportlab.lib.enums import TA_CENTER
from reportlab.lib import colors
from reportlab.lib.utils import ImageReader
from reportlab.pdfgen import canvas as pdf_canvas
import re
import sys
from pathlib import Path

if len(sys.argv) < 2:
    print("Usage: python generate_pdf.py <markdown_file>")
    sys.exit(1)

markdown_path = Path(sys.argv[1])
markdown_text = markdown_path.read_text(encoding="utf-8")
header_image_path = markdown_path.parent / "medidata-espublico-2025-80w.png"

styles = getSampleStyleSheet()

title_style = ParagraphStyle(
    'TitleStyle',
    parent=styles['Heading1'],
    fontSize=22,
    leading=26,
    alignment=TA_CENTER,
    textColor=colors.HexColor("#1F3C88"),
    spaceAfter=16
)

h2_style = ParagraphStyle(
    'H2Style',
    parent=styles['Heading2'],
    fontSize=16,
    leading=20,
    textColor=colors.white,
    backColor=colors.HexColor("#1F77B4"),
    borderPadding=6,
    spaceBefore=8,
    spaceAfter=8
)

h3_style = ParagraphStyle(
    'H3Style',
    parent=styles['Heading3'],
    fontSize=13,
    leading=16,
    textColor=colors.HexColor("#D35400"),
    spaceBefore=6,
    spaceAfter=5
)

h4_style = ParagraphStyle(
    'H4Style',
    parent=styles['Heading4'],
    fontSize=10.5,
    leading=13,
    textColor=colors.HexColor("#2C3E50"),
    spaceBefore=4,
    spaceAfter=3
)

body_style = ParagraphStyle(
    'BodyStyle',
    parent=styles['BodyText'],
    fontSize=9.5,
    leading=12,
    spaceBefore=0,
    spaceAfter=2
)

bullet_style = ParagraphStyle(
    'BulletStyle',
    parent=body_style,
    leftIndent=24,
    firstLineIndent=0,
    bulletIndent=12,
    spaceBefore=1,
    spaceAfter=1
)

code_style = ParagraphStyle(
    'CodeStyle',
    parent=styles['Code'],
    fontName='Courier',
    fontSize=7.8,
    leading=9,
    textColor=colors.HexColor("#222222"),
    backColor=colors.HexColor("#F4F4F4"),
    borderColor=colors.HexColor("#CCCCCC"),
    borderWidth=0.5,
    borderPadding=6,
)

pdf_path = markdown_path.with_suffix(".pdf")

header_image = ImageReader(str(header_image_path))
header_image_width = 80
header_image_height = 32
header_top_offset = 10

doc = SimpleDocTemplate(
    str(pdf_path),
    pagesize=letter,
    rightMargin=36,
    leftMargin=36,
    topMargin=64,
    bottomMargin=32
)


def draw_header(canvas, doc):
    page_width, page_height = letter
    x = page_width - doc.rightMargin - header_image_width
    y = page_height - header_image_height - header_top_offset
    canvas.drawImage(
        header_image,
        x,
        y,
        width=header_image_width,
        height=header_image_height,
        preserveAspectRatio=True,
        mask='auto'
    )


def draw_footer(canvas, doc, page_number, total_pages):
    page_width, _ = letter
    footer_text = f"{page_number}/{total_pages}"
    canvas.setFont("Helvetica", 9)
    canvas.setFillColor(colors.HexColor("#666666"))
    canvas.drawCentredString(page_width / 2, 18, footer_text)


class NumberedCanvas(pdf_canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        total_pages = len(self._saved_page_states)
        for page_number, page_state in enumerate(self._saved_page_states, start=1):
            self.__dict__.update(page_state)
            draw_header(self, doc)
            draw_footer(self, doc, page_number, total_pages)
            super().showPage()
        super().save()


story = []

lines = markdown_text.splitlines()
in_code = False
code_buffer = []
first_h2_seen = False
current_bullet_lines = []


def format_inline_markdown(text):
    parts = re.split(r"(`[^`]+`)", text)
    formatted_parts = []

    for part in parts:
        escaped = (
            part.replace("&", "&amp;")
                .replace("<", "&lt;")
                .replace(">", "&gt;")
        )

        if part.startswith("`") and part.endswith("`") and len(part) >= 2:
            code_text = escaped[1:-1]
            formatted_parts.append(
                f'<font name="Courier" backcolor="#F4F4F4">{code_text}</font>'
            )
        else:
            formatted_parts.append(escaped)

    return "".join(formatted_parts)


def flush_bullet():
    global current_bullet_lines
    if not current_bullet_lines:
        return

    bullet_text = " ".join(line.strip() for line in current_bullet_lines)
    story.append(Paragraph(
        format_inline_markdown(bullet_text),
        bullet_style,
        bulletText="•"
    ))
    current_bullet_lines = []

for line in lines:
    stripped = line.strip()

    if stripped.startswith("```"):
        flush_bullet()
        if not in_code:
            in_code = True
            code_buffer = []
        else:
            code_text = "\n".join(code_buffer)

            estimated_height = max(70, len(code_buffer) * 9.5)
            story.append(CondPageBreak(estimated_height))

            story.append(
                KeepTogether([
                    Preformatted(code_text, code_style),
                    Spacer(1, 6)
                ])
            )

            in_code = False
        continue

    if in_code:
        code_buffer.append(line)
        continue

    if current_bullet_lines and line.startswith("  ") and stripped:
        current_bullet_lines.append(stripped)
        continue

    if stripped.startswith("# "):
        flush_bullet()
        story.append(Paragraph(format_inline_markdown(stripped[2:]), title_style))

    elif stripped.startswith("## "):
        flush_bullet()
        if first_h2_seen:
            story.append(PageBreak())
        first_h2_seen = True
        story.append(Paragraph(format_inline_markdown(stripped[3:]), h2_style))

    elif stripped.startswith("### "):
        flush_bullet()
        story.append(Paragraph(format_inline_markdown(stripped[4:]), h3_style))

    elif stripped.startswith("#### "):
        flush_bullet()
        story.append(Paragraph(format_inline_markdown(stripped[5:]), h4_style))

    elif stripped.startswith("- "):
        flush_bullet()
        current_bullet_lines.append(stripped[2:])

    elif stripped == "":
        flush_bullet()
        story.append(Spacer(1, 3))

    else:
        flush_bullet()
        story.append(Paragraph(format_inline_markdown(line), body_style))

flush_bullet()

doc.build(story, canvasmaker=NumberedCanvas)

print(f"PDF generated: {pdf_path}")
