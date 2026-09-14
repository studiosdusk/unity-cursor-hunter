from __future__ import annotations

import re
from html import escape
from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import BaseDocTemplate, Frame, LongTable, PageTemplate, Paragraph, Spacer, TableStyle


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs/Prototype_V0.0.0.1/README.md"
OUTPUT = ROOT / "output/pdf/cursor-hunter-prototype-v0.0.0.1.pdf"
FONT_PATH = Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf")

PAGE_WIDTH, PAGE_HEIGHT = A4
LEFT_MARGIN = 18 * mm
RIGHT_MARGIN = 18 * mm
TOP_MARGIN = 20 * mm
BOTTOM_MARGIN = 18 * mm
CONTENT_WIDTH = PAGE_WIDTH - LEFT_MARGIN - RIGHT_MARGIN

INK = colors.HexColor("#1B2B32")
MUTED = colors.HexColor("#5C6C73")
TEAL = colors.HexColor("#14707C")
TEAL_DARK = colors.HexColor("#0C4D59")
TEAL_LIGHT = colors.HexColor("#E8F3F4")
LINE = colors.HexColor("#D3E0E3")
CODE_BG = colors.HexColor("#F3F6F7")


def register_fonts() -> None:
    if not FONT_PATH.exists():
        raise FileNotFoundError(f"Korean font not found: {FONT_PATH}")
    pdfmetrics.registerFont(TTFont("KoreanRegular", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("KoreanBold", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("KoreanMono", str(FONT_PATH)))


def sanitize(value: str) -> str:
    """Keep punctuation readable and avoid non-ASCII dash variants in output."""

    return (
        value.replace("\u2010", "-")
        .replace("\u2011", "-")
        .replace("\u2012", "-")
        .replace("\u2013", "-")
        .replace("\u2014", "-")
        .replace("\u2212", "-")
    )


def inline_markup(value: str) -> str:
    value = escape(sanitize(value), quote=False)
    value = re.sub(
        r"\[([^\]]+)\]\([^)]*\)",
        r'<font color="#14707C"><u>\1</u></font>',
        value,
    )
    value = re.sub(r"`([^`]+)`", r'<font color="#0C4D59">\1</font>', value)
    value = re.sub(r"\*\*([^*]+)\*\*", r"<b>\1</b>", value)
    return value


def make_styles():
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "PrototypeTitle",
            parent=base["Title"],
            fontName="KoreanBold",
            fontSize=24,
            leading=30,
            textColor=TEAL_DARK,
            spaceAfter=8,
            alignment=TA_LEFT,
        ),
        "h1": ParagraphStyle(
            "PrototypeH1",
            parent=base["Heading1"],
            fontName="KoreanBold",
            fontSize=15,
            leading=20,
            textColor=TEAL_DARK,
            spaceBefore=13,
            spaceAfter=7,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "PrototypeH2",
            parent=base["Heading2"],
            fontName="KoreanBold",
            fontSize=11.5,
            leading=16,
            textColor=TEAL,
            spaceBefore=10,
            spaceAfter=5,
            keepWithNext=True,
        ),
        "h3": ParagraphStyle(
            "PrototypeH3",
            parent=base["Heading3"],
            fontName="KoreanBold",
            fontSize=10,
            leading=14,
            textColor=INK,
            spaceBefore=8,
            spaceAfter=4,
            keepWithNext=True,
        ),
        "body": ParagraphStyle(
            "PrototypeBody",
            parent=base["BodyText"],
            fontName="KoreanRegular",
            fontSize=9.2,
            leading=14,
            textColor=INK,
            spaceAfter=6,
            wordWrap="CJK",
        ),
        "quote": ParagraphStyle(
            "PrototypeQuote",
            parent=base["BodyText"],
            fontName="KoreanRegular",
            fontSize=9.4,
            leading=15,
            textColor=TEAL_DARK,
            leftIndent=10,
            rightIndent=6,
            borderColor=TEAL,
            borderWidth=2,
            borderPadding=(5, 7, 5, 7),
            backColor=TEAL_LIGHT,
            spaceBefore=2,
            spaceAfter=9,
            wordWrap="CJK",
        ),
        "bullet": ParagraphStyle(
            "PrototypeBullet",
            parent=base["BodyText"],
            fontName="KoreanRegular",
            fontSize=9.1,
            leading=13.5,
            textColor=INK,
            leftIndent=12,
            firstLineIndent=-9,
            spaceAfter=2,
            wordWrap="CJK",
        ),
        "ordered": ParagraphStyle(
            "PrototypeOrdered",
            parent=base["BodyText"],
            fontName="KoreanRegular",
            fontSize=9.1,
            leading=13.5,
            textColor=INK,
            leftIndent=15,
            firstLineIndent=-13,
            spaceAfter=2,
            wordWrap="CJK",
        ),
        "code": ParagraphStyle(
            "PrototypeCode",
            parent=base["Code"],
            fontName="KoreanMono",
            fontSize=8.15,
            leading=11.2,
            textColor=INK,
            backColor=CODE_BG,
            borderColor=LINE,
            borderWidth=0.6,
            borderPadding=6,
            spaceBefore=3,
            spaceAfter=8,
            wordWrap="CJK",
        ),
        "table_header": ParagraphStyle(
            "PrototypeTableHeader",
            parent=base["BodyText"],
            fontName="KoreanBold",
            fontSize=7.5,
            leading=10,
            textColor=colors.white,
            wordWrap="CJK",
        ),
        "table_cell": ParagraphStyle(
            "PrototypeTableCell",
            parent=base["BodyText"],
            fontName="KoreanRegular",
            fontSize=7.35,
            leading=9.4,
            textColor=INK,
            wordWrap="CJK",
        ),
    }


def split_table_row(line: str) -> list[str]:
    line = line.strip()
    if line.startswith("|"):
        line = line[1:]
    if line.endswith("|"):
        line = line[:-1]
    return [cell.strip() for cell in line.split("|")]


def is_table_separator(line: str) -> bool:
    cells = split_table_row(line)
    return bool(cells) and all(re.fullmatch(r":?-{3,}:?", cell.replace(" ", "")) for cell in cells)


def table_widths(headers: list[str]) -> list[float]:
    count = len(headers)
    if count == 2:
        ratios = [0.30, 0.70]
    elif count == 3:
        joined = " ".join(headers)
        if "영역" in joined and "상태" in joined:
            ratios = [0.18, 0.15, 0.67]
        elif "어셈블리" in joined:
            ratios = [0.27, 0.25, 0.48]
        else:
            ratios = [0.22, 0.25, 0.53]
    elif count == 4:
        ratios = [0.18, 0.18, 0.27, 0.37]
    else:
        ratios = [1 / count] * count
    return [CONTENT_WIDTH * ratio for ratio in ratios]


def make_table(rows: list[list[str]], styles) -> LongTable:
    headers = rows[0]
    data = []
    for row_index, row in enumerate(rows):
        if len(row) < len(headers):
            row = row + [""] * (len(headers) - len(row))
        if len(row) > len(headers):
            row = row[: len(headers) - 1] + [" | ".join(row[len(headers) - 1 :])]
        style = styles["table_header"] if row_index == 0 else styles["table_cell"]
        data.append([Paragraph(inline_markup(cell), style) for cell in row])

    table = LongTable(
        data,
        colWidths=table_widths(headers),
        repeatRows=1,
        hAlign="LEFT",
        splitByRow=1,
    )
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), TEAL_DARK),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("GRID", (0, 0), (-1, -1), 0.35, LINE),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.white, colors.HexColor("#F8FBFB")]),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 5),
                ("RIGHTPADDING", (0, 0), (-1, -1), 5),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


def code_paragraph(code_lines: list[str], styles) -> Paragraph:
    code = "<br/>".join(inline_markup(line) for line in code_lines)
    return Paragraph(code or "&nbsp;", styles["code"])


def build_story(styles) -> list:
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    story = []
    paragraph_lines: list[str] = []

    def flush_paragraph() -> None:
        if paragraph_lines:
            text = " ".join(part.strip() for part in paragraph_lines if part.strip())
            if text:
                story.append(Paragraph(inline_markup(text), styles["body"]))
            paragraph_lines.clear()

    index = 0
    first_heading = True
    while index < len(lines):
        raw = sanitize(lines[index].rstrip())
        stripped = raw.strip()

        if not stripped:
            flush_paragraph()
            index += 1
            continue

        if stripped.startswith("```"):
            flush_paragraph()
            index += 1
            block = []
            while index < len(lines) and not lines[index].strip().startswith("```"):
                block.append(sanitize(lines[index].rstrip()))
                index += 1
            if index < len(lines):
                index += 1
            story.append(code_paragraph(block, styles))
            continue

        heading = re.match(r"^(#{1,3})\s+(.+)$", stripped)
        if heading:
            flush_paragraph()
            level = len(heading.group(1))
            heading_text = heading.group(2).strip()
            if first_heading and level == 1:
                story.append(Paragraph(inline_markup(heading_text), styles["title"]))
                first_heading = False
            else:
                story.append(Paragraph(inline_markup(heading_text), styles[f"h{level}"]))
            index += 1
            continue

        if stripped.startswith(">"):
            flush_paragraph()
            quote_lines = []
            while index < len(lines) and lines[index].strip().startswith(">"):
                quote_lines.append(sanitize(lines[index].strip()[1:].strip()))
                index += 1
            quote = " ".join(line for line in quote_lines if line)
            story.append(Paragraph(inline_markup(quote), styles["quote"]))
            continue

        if stripped.startswith("|") and index + 1 < len(lines) and is_table_separator(lines[index + 1]):
            flush_paragraph()
            rows = [split_table_row(stripped)]
            index += 2
            while index < len(lines) and lines[index].strip().startswith("|"):
                rows.append(split_table_row(lines[index]))
                index += 1
            story.append(make_table(rows, styles))
            story.append(Spacer(1, 5))
            continue

        bullet = re.match(r"^[-*]\s+(.+)$", stripped)
        if bullet:
            flush_paragraph()
            story.append(Paragraph(f"- {inline_markup(bullet.group(1))}", styles["bullet"]))
            index += 1
            continue

        ordered = re.match(r"^(\d+)\.\s+(.+)$", stripped)
        if ordered:
            flush_paragraph()
            story.append(Paragraph(f"{ordered.group(1)}. {inline_markup(ordered.group(2))}", styles["ordered"]))
            index += 1
            continue

        if re.fullmatch(r"-{3,}", stripped):
            flush_paragraph()
            story.append(Spacer(1, 3))
            index += 1
            continue

        paragraph_lines.append(stripped)
        index += 1

    flush_paragraph()
    return story


def draw_page(canvas, document) -> None:
    canvas.saveState()
    canvas.setFillColor(TEAL_DARK)
    canvas.rect(0, PAGE_HEIGHT - 9 * mm, PAGE_WIDTH, 9 * mm, fill=1, stroke=0)
    canvas.setFillColor(colors.white)
    canvas.setFont("KoreanBold", 7.2)
    canvas.drawString(LEFT_MARGIN, PAGE_HEIGHT - 5.8 * mm, "CURSOR HUNTER  /  PROTOTYPE V0.0.0.1")

    canvas.setStrokeColor(LINE)
    canvas.setLineWidth(0.5)
    canvas.line(LEFT_MARGIN, 11 * mm, PAGE_WIDTH - RIGHT_MARGIN, 11 * mm)
    canvas.setFillColor(MUTED)
    canvas.setFont("KoreanRegular", 7.2)
    canvas.drawString(LEFT_MARGIN, 6.5 * mm, "Prototype summary  |  Read with the repository source")
    canvas.drawRightString(PAGE_WIDTH - RIGHT_MARGIN, 6.5 * mm, f"{document.page}")
    canvas.restoreState()


def main() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    styles = make_styles()
    document = BaseDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=LEFT_MARGIN,
        rightMargin=RIGHT_MARGIN,
        topMargin=TOP_MARGIN,
        bottomMargin=BOTTOM_MARGIN,
        title="Cursor Hunter Prototype V0.0.0.1",
        author="Cursor Hunter Team",
    )
    frame = Frame(
        LEFT_MARGIN,
        BOTTOM_MARGIN,
        CONTENT_WIDTH,
        PAGE_HEIGHT - TOP_MARGIN - BOTTOM_MARGIN,
        id="content",
        leftPadding=0,
        rightPadding=0,
        topPadding=0,
        bottomPadding=0,
    )
    document.addPageTemplates([PageTemplate(id="prototype", frames=[frame], onPage=draw_page)])
    document.build(build_story(styles))
    print(f"Wrote {OUTPUT}")


if __name__ == "__main__":
    main()
