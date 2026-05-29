from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Preformatted,
    KeepTogether, CondPageBreak, PageBreak, Flowable
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

if len(sys.argv) < 2 or len(sys.argv) > 3:
    print("Usage: python generate_pdf.py <markdown_file> [output_pdf_file]")
    sys.exit(1)

base_path = Path.cwd()
script_path = Path(__file__).resolve().parent
markdown_path = (base_path / sys.argv[1]).resolve()
markdown_text = markdown_path.read_text(encoding="utf-8")
header_image_path = script_path / "medidata-espublico-2025-80w.png"

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

bold_body_style = ParagraphStyle(
    'BoldBodyStyle',
    parent=body_style,
    fontName='Helvetica-Bold',
    fontSize=11,
    leading=14,
    textColor=colors.HexColor("#1F1F1F"),
    spaceBefore=3,
    spaceAfter=4
)

centered_body_style = ParagraphStyle(
    'CenteredBodyStyle',
    parent=body_style,
    alignment=TA_CENTER
)

centered_bold_body_style = ParagraphStyle(
    'CenteredBoldBodyStyle',
    parent=bold_body_style,
    alignment=TA_CENTER,
    fontSize=12,
    leading=15,
    textColor=colors.HexColor("#1F3C88"),
    spaceBefore=4,
    spaceAfter=6
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

pdf_path = (
    (base_path / sys.argv[2]).resolve()
    if len(sys.argv) == 3
    else markdown_path.with_suffix(".pdf")
)

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
        bookmark_refs = getattr(self, "_bookmark_refs", [])
        for page_number, page_state in enumerate(self._saved_page_states, start=1):
            self.__dict__.update(page_state)
            for bookmark_page, name, left, top in bookmark_refs:
                if bookmark_page == page_number:
                    self.bookmarkHorizontalAbsolute(name, top, left=left)
            draw_header(self, doc)
            draw_footer(self, doc, page_number, total_pages)
            super().showPage()
        super().save()


class AnchorFlowable(Flowable):
    def __init__(self, name):
        super().__init__()
        self.name = name
        self.width = 0
        self.height = 0

    def draw(self):
        left, top = self.canv.absolutePosition(0, 0)
        bookmark_refs = getattr(self.canv, "_bookmark_refs", None)
        if bookmark_refs is None:
            bookmark_refs = []
            self.canv._bookmark_refs = bookmark_refs
        bookmark_refs.append((self.canv._pageNumber, self.name, left, top))


story = []

lines = markdown_text.splitlines()
in_code = False
code_buffer = []
first_h2_seen = False
current_bullet_lines = []
linked_anchors = {
    href[1:]
    for href in re.findall(r"\[[^\]]+\]\((#[^)]+)\)", markdown_text)
}
emitted_anchors = set()


def format_inline_markdown(text):
    link_pattern = re.compile(r"\[([^\]]+)\]\(([^)]+)\)")
    match = link_pattern.search(text)
    if match:
        formatted_parts = []
        position = 0

        for match in link_pattern.finditer(text):
            formatted_parts.append(format_inline_markdown(text[position:match.start()]))
            link_text = format_inline_markdown(match.group(1))
            href = escape_xml(match.group(2).strip())
            formatted_parts.append(
                f'<link href="{href}" color="#1F77B4" underline="0">{link_text}</link>'
            )
            position = match.end()

        formatted_parts.append(format_inline_markdown(text[position:]))
        return "".join(formatted_parts)

    parts = re.split(r"(`[^`]+`)", text)
    formatted_parts = []

    for part in parts:
        escaped = escape_xml(part)

        if part.startswith("`") and part.endswith("`") and len(part) >= 2:
            code_text = escaped[1:-1]
            formatted_parts.append(
                f'<font name="Courier" backcolor="#F4F4F4">{code_text}</font>'
            )
        else:
            bold_formatted = re.sub(
                r"\*\*(.+?)\*\*",
                r'<font name="Helvetica-Bold">\1</font>',
                escaped
            )
            bold_formatted = re.sub(
                r"__(.+?)__",
                r'<font name="Helvetica-Bold">\1</font>',
                bold_formatted
            )
            italic_formatted = re.sub(
                r"(?<!\*)\*(?!\*)(.+?)(?<!\*)\*(?!\*)",
                r'<font name="Helvetica-Oblique">\1</font>',
                bold_formatted
            )
            italic_formatted = re.sub(
                r"(?<!_)_(?!_)(.+?)(?<!_)_(?!_)",
                r'<font name="Helvetica-Oblique">\1</font>',
                italic_formatted
            )
            formatted_parts.append(italic_formatted)

    return "".join(formatted_parts)


def escape_xml(text):
    return (
        text.replace("&", "&amp;")
            .replace("<", "&lt;")
            .replace(">", "&gt;")
            .replace('"', "&quot;")
    )


def get_markdown_anchor(text):
    text = re.sub(r"`([^`]+)`", r"\1", text)
    text = re.sub(r"\[(.*?)\]\((.*?)\)", r"\1", text)
    text = text.lower()
    text = re.sub(r"[^\w\s-]", "", text)
    text = re.sub(r"\s+", "-", text.strip())
    return text


def with_anchor(text):
    anchor = get_markdown_anchor(text)
    if anchor not in linked_anchors or anchor in emitted_anchors:
        return format_inline_markdown(text)

    emitted_anchors.add(anchor)
    story.append(AnchorFlowable(anchor))
    return format_inline_markdown(text)


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


def get_centered_text(text):
    match = re.fullmatch(r"<center>(.*?)</center>", text, re.IGNORECASE)
    return match.group(1).strip() if match else None


def get_fully_bold_text(text):
    match = re.fullmatch(r"(?:\*\*|__)(.+?)(?:\*\*|__)", text)
    return match.group(1).strip() if match else None

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
        story.append(Paragraph(with_anchor(stripped[2:]), title_style))

    elif stripped.startswith("## "):
        flush_bullet()
        if first_h2_seen:
            story.append(PageBreak())
        first_h2_seen = True
        story.append(Paragraph(with_anchor(stripped[3:]), h2_style))

    elif stripped.startswith("### "):
        flush_bullet()
        story.append(Paragraph(with_anchor(stripped[4:]), h3_style))

    elif stripped.startswith("#### "):
        flush_bullet()
        story.append(Paragraph(with_anchor(stripped[5:]), h4_style))

    elif stripped.startswith("- "):
        flush_bullet()
        current_bullet_lines.append(stripped[2:])

    elif stripped == "":
        flush_bullet()
        story.append(Spacer(1, 3))

    else:
        flush_bullet()
        centered_text = get_centered_text(stripped)
        if centered_text is not None:
            centered_bold_text = get_fully_bold_text(centered_text)
            if centered_bold_text is not None:
                story.append(Paragraph(
                    format_inline_markdown(centered_bold_text),
                    centered_bold_body_style
                ))
                continue

            story.append(Paragraph(
                format_inline_markdown(centered_text),
                centered_body_style
            ))
        else:
            fully_bold_text = get_fully_bold_text(stripped)
            if fully_bold_text is not None:
                story.append(Paragraph(
                    format_inline_markdown(fully_bold_text),
                    bold_body_style
                ))
            else:
                story.append(Paragraph(format_inline_markdown(line), body_style))

flush_bullet()

doc.build(story, canvasmaker=NumberedCanvas)

print(f"PDF generated: {pdf_path}")
