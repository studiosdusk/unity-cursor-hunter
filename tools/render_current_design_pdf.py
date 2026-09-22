"""Render the current v0.0.4 Markdown design as a reviewable PDF.

This intentionally reads the concise collaboration handoff rather than the
legacy screen-catalog generator. The long design Markdown remains the detailed
reference while the PDF is kept reviewable for day-to-day collaboration.
"""

from pathlib import Path
import sys

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.units import mm
from reportlab.platypus import BaseDocTemplate, Frame, PageTemplate


ROOT = Path(__file__).resolve().parents[1]
DOCS = ROOT / "docs"
SOURCE = DOCS / "collaboration" / "current-state-handoff-v0.0.4.md"
OUTPUT = ROOT / "output" / "pdf" / "cursor-hunter-game-design-v0.0.4.pdf"


def main() -> None:
    sys.path.insert(0, str(DOCS))
    import render_plan_pdf as base

    base.register_fonts()

    def draw_page(canvas, document):
        canvas.saveState()
        width, height = A4
        canvas.setFillColor(colors.HexColor("#14213D"))
        canvas.rect(0, height - 9 * mm, width, 9 * mm, fill=1, stroke=0)
        canvas.setFillColor(colors.HexColor("#2AA7B8"))
        canvas.rect(0, height - 9 * mm, 28 * mm, 9 * mm, fill=1, stroke=0)
        canvas.setFont("KoreanBold", 7.5)
        canvas.setFillColor(colors.white)
        canvas.drawString(18 * mm, height - 5.8 * mm, "CURSOR HUNTER")
        canvas.setFont("Korean", 7.5)
        canvas.setFillColor(colors.HexColor("#687487"))
        canvas.drawString(
            18 * mm,
            9 * mm,
            "협업 인수인계 핵심본 v0.0.4 · 150분 목표",
        )
        canvas.drawRightString(
            width - 18 * mm,
            9 * mm,
            f"{canvas.getPageNumber():02d}",
        )
        canvas.setStrokeColor(colors.HexColor("#D6E2E5"))
        canvas.setLineWidth(0.45)
        canvas.line(18 * mm, 13 * mm, width - 18 * mm, 13 * mm)
        canvas.restoreState()

    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    document = BaseDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=18 * mm,
        rightMargin=18 * mm,
        topMargin=17 * mm,
        bottomMargin=18 * mm,
        title="Cursor Hunter v0.0.4 협업 인수인계 핵심본",
        author="Codex",
    )
    frame = Frame(
        document.leftMargin,
        document.bottomMargin,
        document.width,
        document.height,
        id="body",
    )
    document.addPageTemplates(
        [PageTemplate(id="current-design", frames=[frame], onPage=draw_page)]
    )
    document.build(
        base.markdown_flowables(
            SOURCE.read_text(encoding="utf-8"),
            base.styles(),
        )
    )
    print(OUTPUT)


if __name__ == "__main__":
    main()
