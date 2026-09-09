from __future__ import annotations

import re
from pathlib import Path
from xml.sax.saxutils import escape

from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.lib.styles import ParagraphStyle, getSampleStyleSheet
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.platypus import (
    BaseDocTemplate,
    Frame,
    PageBreak,
    PageTemplate,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "collaboration" / "parallel-development-guide.md"
OUTPUT = ROOT / "output" / "pdf" / "cursor-hunter-parallel-development-guide.pdf"
FONT_PATH = Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("Guide", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("GuideBold", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("GuideCode", str(FONT_PATH)))


def inline_markup(value: str) -> str:
    value = escape(value)
    value = re.sub(r"`([^`]+)`", r'<font name="GuideCode">\1</font>', value)
    value = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", value)
    return value


def table_cells(line: str) -> list[str]:
    return [cell.strip() for cell in line.strip().strip("|").split("|")]


def is_table_separator(line: str) -> bool:
    cells = table_cells(line)
    return bool(cells) and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells)


def make_styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "GuideTitle",
            parent=base["Title"],
            fontName="GuideBold",
            fontSize=24,
            leading=30,
            textColor=colors.HexColor("#10243E"),
            spaceAfter=4 * mm,
        ),
        "subtitle": ParagraphStyle(
            "GuideSubtitle",
            parent=base["Normal"],
            fontName="Guide",
            fontSize=10.5,
            leading=16,
            textColor=colors.HexColor("#536477"),
            spaceAfter=2.5 * mm,
            wordWrap="CJK",
        ),
        "h1": ParagraphStyle(
            "GuideH1",
            parent=base["Heading1"],
            fontName="GuideBold",
            fontSize=16,
            leading=22,
            textColor=colors.HexColor("#10243E"),
            spaceBefore=6 * mm,
            spaceAfter=2.4 * mm,
            keepWithNext=True,
            wordWrap="CJK",
        ),
        "h2": ParagraphStyle(
            "GuideH2",
            parent=base["Heading2"],
            fontName="GuideBold",
            fontSize=12.2,
            leading=17,
            textColor=colors.HexColor("#14707C"),
            spaceBefore=3.5 * mm,
            spaceAfter=1.7 * mm,
            keepWithNext=True,
            wordWrap="CJK",
        ),
        "h3": ParagraphStyle(
            "GuideH3",
            parent=base["Heading3"],
            fontName="GuideBold",
            fontSize=10.4,
            leading=14,
            textColor=colors.HexColor("#334C68"),
            spaceBefore=2.5 * mm,
            spaceAfter=1.1 * mm,
            keepWithNext=True,
            wordWrap="CJK",
        ),
        "body": ParagraphStyle(
            "GuideBody",
            parent=base["BodyText"],
            fontName="Guide",
            fontSize=9.15,
            leading=14,
            textColor=colors.HexColor("#25364A"),
            spaceAfter=2 * mm,
            wordWrap="CJK",
        ),
        "bullet": ParagraphStyle(
            "GuideBullet",
            parent=base["BodyText"],
            fontName="Guide",
            fontSize=9.05,
            leading=13.8,
            leftIndent=5.3 * mm,
            firstLineIndent=-4.2 * mm,
            textColor=colors.HexColor("#25364A"),
            spaceAfter=1.1 * mm,
            wordWrap="CJK",
        ),
        "quote": ParagraphStyle(
            "GuideQuote",
            parent=base["BodyText"],
            fontName="Guide",
            fontSize=9.1,
            leading=14,
            leftIndent=4.5 * mm,
            rightIndent=3.5 * mm,
            textColor=colors.HexColor("#3E556C"),
            backColor=colors.HexColor("#ECF8F7"),
            borderColor=colors.HexColor("#3BA8A1"),
            borderWidth=1,
            borderPadding=5,
            spaceBefore=2 * mm,
            spaceAfter=3 * mm,
            wordWrap="CJK",
        ),
        "code": ParagraphStyle(
            "GuideCodeBlock",
            parent=base["Code"],
            fontName="GuideCode",
            fontSize=8.15,
            leading=12.2,
            leftIndent=3.2 * mm,
            rightIndent=3.2 * mm,
            textColor=colors.HexColor("#EAF5F6"),
            backColor=colors.HexColor("#10243E"),
            borderPadding=6,
            spaceBefore=1.5 * mm,
            spaceAfter=2.5 * mm,
            wordWrap="CJK",
        ),
        "table_header": ParagraphStyle(
            "GuideTableHeader",
            parent=base["BodyText"],
            fontName="GuideBold",
            fontSize=7.55,
            leading=10.4,
            textColor=colors.white,
            wordWrap="CJK",
        ),
        "table_cell": ParagraphStyle(
            "GuideTableCell",
            parent=base["BodyText"],
            fontName="Guide",
            fontSize=7.45,
            leading=10.4,
            textColor=colors.HexColor("#25364A"),
            wordWrap="CJK",
        ),
        "meta": ParagraphStyle(
            "GuideMeta",
            parent=base["Normal"],
            fontName="Guide",
            fontSize=8.3,
            leading=12.5,
            textColor=colors.HexColor("#64758A"),
            spaceAfter=1.2 * mm,
            wordWrap="CJK",
        ),
    }


def column_widths(count: int) -> list[float]:
    available = 170 * mm
    if count == 2:
        return [available * 0.26, available * 0.74]
    if count == 3:
        return [available * 0.23, available * 0.38, available * 0.39]
    if count == 4:
        return [available * 0.21, available * 0.27, available * 0.26, available * 0.26]
    return [available / count] * count


def make_table(rows: list[list[str]], styles: dict[str, ParagraphStyle]) -> Table:
    count = max(len(row) for row in rows)
    normalized = [row + [""] * (count - len(row)) for row in rows]
    data = []
    for index, row in enumerate(normalized):
        style = styles["table_header"] if index == 0 else styles["table_cell"]
        data.append([Paragraph(inline_markup(cell), style) for cell in row])
    table = Table(data, colWidths=column_widths(count), repeatRows=1, hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#14707C")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("BACKGROUND", (0, 1), (-1, -1), colors.HexColor("#F4F8FA")),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.HexColor("#F4F8FA"), colors.white]),
                ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#C7D6DC")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


def parse_source(styles: dict[str, ParagraphStyle]) -> list[object]:
    lines = SOURCE.read_text(encoding="utf-8").splitlines()
    story: list[object] = []
    index = 0
    in_code = False
    code_lines: list[str] = []
    table_buffer: list[list[str]] = []

    def flush_table() -> None:
        nonlocal table_buffer
        if table_buffer:
            story.append(make_table(table_buffer, styles))
            story.append(Spacer(1, 2.2 * mm))
            table_buffer = []

    def flush_code() -> None:
        nonlocal code_lines
        if code_lines:
            block = "<br/>".join(inline_markup(line) if line else "&nbsp;" for line in code_lines)
            story.append(Paragraph(block, styles["code"]))
            code_lines = []

    while index < len(lines):
        raw = lines[index]
        stripped = raw.strip()
        if stripped.startswith("```"):
            flush_table()
            if in_code:
                flush_code()
            in_code = not in_code
            index += 1
            continue
        if in_code:
            code_lines.append(raw)
            index += 1
            continue
        if stripped == "<!-- pagebreak -->":
            flush_table()
            story.append(PageBreak())
            index += 1
            continue
        if not stripped:
            flush_table()
            index += 1
            continue
        if stripped.startswith("|"):
            if index + 1 < len(lines) and is_table_separator(lines[index + 1]):
                table_buffer.append(table_cells(stripped))
                index += 2
                continue
            if table_buffer:
                table_buffer.append(table_cells(stripped))
                index += 1
                continue
        flush_table()
        if stripped.startswith("# "):
            story.append(Paragraph(inline_markup(stripped[2:]), styles["title"]))
        elif stripped.startswith("## "):
            story.append(Paragraph(inline_markup(stripped[3:]), styles["h1"]))
        elif stripped.startswith("### "):
            story.append(Paragraph(inline_markup(stripped[4:]), styles["h2"]))
        elif stripped.startswith("> "):
            story.append(Paragraph(inline_markup(stripped[2:]), styles["quote"]))
        elif stripped.startswith("- "):
            story.append(Paragraph(inline_markup("- " + stripped[2:]), styles["bullet"]))
        else:
            style = styles["subtitle"] if not story else styles["body"]
            story.append(Paragraph(inline_markup(stripped), style))
        index += 1
    flush_table()
    flush_code()
    return story


def draw_page(canvas, doc) -> None:
    canvas.saveState()
    width, height = A4
    canvas.setFillColor(colors.HexColor("#10243E"))
    canvas.rect(0, height - 7 * mm, width, 7 * mm, stroke=0, fill=1)
    canvas.setStrokeColor(colors.HexColor("#D3E1E5"))
    canvas.setLineWidth(0.5)
    canvas.line(20 * mm, 15 * mm, width - 20 * mm, 15 * mm)
    canvas.setFont("Guide", 7.5)
    canvas.setFillColor(colors.HexColor("#718398"))
    canvas.drawString(20 * mm, 9.5 * mm, "CURSOR HUNTER / Prototype Parallel Development Guide")
    canvas.drawRightString(width - 20 * mm, 9.5 * mm, f"{doc.page}")
    canvas.restoreState()


def build() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    styles = make_styles()
    doc = BaseDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=20 * mm,
        rightMargin=20 * mm,
        topMargin=18 * mm,
        bottomMargin=20 * mm,
        title="CURSOR HUNTER 프로토타입 병렬 개발 가이드",
        author="OpenAI Codex",
    )
    frame = Frame(doc.leftMargin, doc.bottomMargin, doc.width, doc.height, id="normal")
    doc.addPageTemplates([PageTemplate(id="guide", frames=[frame], onPage=draw_page)])
    doc.build(parse_source(styles))
    print(OUTPUT)


if __name__ == "__main__":
    build()
