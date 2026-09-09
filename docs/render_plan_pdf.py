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
    Flowable,
    Frame,
    KeepTogether,
    PageTemplate,
    PageBreak,
    Paragraph,
    Spacer,
    Table,
    TableStyle,
)


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "docs" / "cursor-hunter-game-design-v0.0.3.md"
OUTPUT = ROOT / "output" / "pdf" / "cursor-hunter-game-design-v0.0.3.pdf"
FONT_PATH = Path("/System/Library/Fonts/Supplemental/Arial Unicode.ttf")


def register_fonts() -> None:
    pdfmetrics.registerFont(TTFont("Korean", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("KoreanBold", str(FONT_PATH)))
    pdfmetrics.registerFont(TTFont("KoreanCode", str(FONT_PATH)))


def inline_markup(text: str) -> str:
    text = escape(text)
    text = re.sub(r"`([^`]+)`", r'<font name="KoreanCode">\1</font>', text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<b>\1</b>", text)
    return text


def table_cells(line: str) -> list[str]:
    body = line.strip().strip("|")
    return [cell.strip() for cell in body.split("|")]


def is_table_separator(line: str) -> bool:
    cells = table_cells(line)
    return bool(cells) and all(re.fullmatch(r":?-{3,}:?", cell) for cell in cells)


def styles() -> dict[str, ParagraphStyle]:
    base = getSampleStyleSheet()
    return {
        "title": ParagraphStyle(
            "PlanTitle",
            parent=base["Title"],
            fontName="KoreanBold",
            fontSize=24,
            leading=31,
            textColor=colors.HexColor("#14213D"),
            spaceAfter=7 * mm,
            alignment=TA_LEFT,
        ),
        "subtitle": ParagraphStyle(
            "PlanSubtitle",
            parent=base["Normal"],
            fontName="Korean",
            fontSize=10.5,
            leading=17,
            textColor=colors.HexColor("#526071"),
            spaceAfter=2 * mm,
        ),
        "h1": ParagraphStyle(
            "PlanH1",
            parent=base["Heading1"],
            fontName="KoreanBold",
            fontSize=15,
            leading=21,
            textColor=colors.HexColor("#14213D"),
            spaceBefore=8 * mm,
            spaceAfter=3 * mm,
            keepWithNext=True,
        ),
        "h2": ParagraphStyle(
            "PlanH2",
            parent=base["Heading2"],
            fontName="KoreanBold",
            fontSize=12.2,
            leading=17,
            textColor=colors.HexColor("#1F7182"),
            spaceBefore=5 * mm,
            spaceAfter=2 * mm,
            keepWithNext=True,
        ),
        "h3": ParagraphStyle(
            "PlanH3",
            parent=base["Heading3"],
            fontName="KoreanBold",
            fontSize=10.8,
            leading=15,
            textColor=colors.HexColor("#31435E"),
            spaceBefore=3 * mm,
            spaceAfter=1.5 * mm,
            keepWithNext=True,
        ),
        "h4": ParagraphStyle(
            "PlanH4",
            parent=base["Heading4"],
            fontName="KoreanBold",
            fontSize=9.8,
            leading=14,
            textColor=colors.HexColor("#526071"),
            spaceBefore=2.5 * mm,
            spaceAfter=1.2 * mm,
            keepWithNext=True,
        ),
        "body": ParagraphStyle(
            "PlanBody",
            parent=base["BodyText"],
            fontName="Korean",
            fontSize=9.2,
            leading=14.5,
            textColor=colors.HexColor("#253044"),
            spaceAfter=2.2 * mm,
            wordWrap="CJK",
        ),
        "bullet": ParagraphStyle(
            "PlanBullet",
            parent=base["BodyText"],
            fontName="Korean",
            fontSize=9.1,
            leading=14.2,
            leftIndent=5 * mm,
            firstLineIndent=-4 * mm,
            textColor=colors.HexColor("#253044"),
            spaceAfter=1.1 * mm,
            wordWrap="CJK",
        ),
        "quote": ParagraphStyle(
            "PlanQuote",
            parent=base["BodyText"],
            fontName="Korean",
            fontSize=8.9,
            leading=14,
            leftIndent=5 * mm,
            rightIndent=4 * mm,
            textColor=colors.HexColor("#4B5563"),
            backColor=colors.HexColor("#F1F7F8"),
            borderColor=colors.HexColor("#2AA7B8"),
            borderWidth=1,
            borderPadding=5,
            spaceBefore=2 * mm,
            spaceAfter=3 * mm,
            wordWrap="CJK",
        ),
        "code": ParagraphStyle(
            "PlanCode",
            parent=base["Code"],
            fontName="KoreanCode",
            fontSize=8.7,
            leading=13.5,
            leftIndent=4 * mm,
            rightIndent=4 * mm,
            textColor=colors.HexColor("#ECF4F6"),
            backColor=colors.HexColor("#14213D"),
            borderPadding=6,
            spaceBefore=2 * mm,
            spaceAfter=3 * mm,
            wordWrap="CJK",
        ),
        "table_header": ParagraphStyle(
            "PlanTableHeader",
            parent=base["BodyText"],
            fontName="KoreanBold",
            fontSize=7.8,
            leading=11.2,
            textColor=colors.white,
            wordWrap="CJK",
        ),
        "table_cell": ParagraphStyle(
            "PlanTableCell",
            parent=base["BodyText"],
            fontName="Korean",
            fontSize=7.7,
            leading=11.2,
            textColor=colors.HexColor("#253044"),
            wordWrap="CJK",
        ),
        "meta": ParagraphStyle(
            "PlanMeta",
            parent=base["BodyText"],
            fontName="Korean",
            fontSize=8.5,
            leading=13,
            textColor=colors.HexColor("#526071"),
            spaceAfter=1.2 * mm,
        ),
    }


def column_widths(count: int) -> list[float]:
    available = 170 * mm
    if count == 2:
        return [available * 0.26, available * 0.74]
    if count == 3:
        return [available * 0.24, available * 0.38, available * 0.38]
    if count == 4:
        return [available * 0.22, available * 0.26, available * 0.26, available * 0.26]
    if count == 5:
        return [available * 0.18, available * 0.19, available * 0.19, available * 0.21, available * 0.23]
    if count == 6:
        return [available * 0.15, available * 0.17, available * 0.16, available * 0.16, available * 0.18, available * 0.18]
    return [available / count] * count


def make_table(rows: list[list[str]], style_map: dict[str, ParagraphStyle]) -> Table:
    count = max(len(row) for row in rows)
    normalized = [row + [""] * (count - len(row)) for row in rows]
    data = []
    for row_index, row in enumerate(normalized):
        cell_style = style_map["table_header"] if row_index == 0 else style_map["table_cell"]
        data.append([Paragraph(inline_markup(cell), cell_style) for cell in row])
    table = Table(data, colWidths=column_widths(count), repeatRows=1, hAlign="LEFT")
    table.setStyle(
        TableStyle(
            [
                ("BACKGROUND", (0, 0), (-1, 0), colors.HexColor("#1F7182")),
                ("TEXTCOLOR", (0, 0), (-1, 0), colors.white),
                ("BACKGROUND", (0, 1), (-1, -1), colors.HexColor("#F7FAFB")),
                ("ROWBACKGROUNDS", (0, 1), (-1, -1), [colors.HexColor("#F7FAFB"), colors.white]),
                ("GRID", (0, 0), (-1, -1), 0.35, colors.HexColor("#D6E2E5")),
                ("VALIGN", (0, 0), (-1, -1), "TOP"),
                ("LEFTPADDING", (0, 0), (-1, -1), 4),
                ("RIGHTPADDING", (0, 0), (-1, -1), 4),
                ("TOPPADDING", (0, 0), (-1, -1), 4),
                ("BOTTOMPADDING", (0, 0), (-1, -1), 4),
            ]
        )
    )
    return table


class WireframeFlowable(Flowable):
    """Draws a compact, vector wireframe for the four primary game states."""

    GEM_COLORS = ["#B84646", "#D4A33E", "#9263C5", "#3E80CA", "#E9EEF4"]

    def __init__(self, kind: str):
        super().__init__()
        self.kind = kind
        self.width = 170 * mm
        self.height = 96 * mm
        self.display_scale = 0.82 if kind in ("splash", "main", "settlement", "failure") else 1.0

    def wrap(self, avail_width, avail_height):
        return min(self.width, avail_width), self.height * self.display_scale

    @staticmethod
    def text(canvas, value: str, x: float, y: float, size: float = 7.5, color="#253044", align="left"):
        canvas.setFont("KoreanBold" if size >= 8.5 else "Korean", size)
        canvas.setFillColor(colors.HexColor(color))
        if align == "center":
            canvas.drawCentredString(x, y, value)
        elif align == "right":
            canvas.drawRightString(x, y, value)
        else:
            canvas.drawString(x, y, value)

    @staticmethod
    def box(canvas, x, y, width, height, label, fill="#F8F4E8", stroke="#526071", text_color="#253044", radius=3, size=7.5):
        canvas.setFillColor(colors.HexColor(fill))
        canvas.setStrokeColor(colors.HexColor(stroke))
        canvas.setLineWidth(0.6)
        canvas.roundRect(x, y, width, height, radius, fill=1, stroke=1)
        WireframeFlowable.text(canvas, label, x + width / 2, y + (height - size) / 2 + 1.5, size, text_color, "center")

    def draw_background(self, canvas, width, height):
        canvas.setFillColor(colors.HexColor("#B9D96C"))
        canvas.rect(0, 0, width, height, fill=1, stroke=0)
        # The supplied reference is a quiet green field with repeated environmental shapes.
        grass = [(16, 18), (47, 35), (82, 20), (125, 42), (158, 18), (30, 62), (102, 66), (145, 73)]
        canvas.setStrokeColor(colors.HexColor("#8FBE64"))
        canvas.setLineWidth(0.55)
        for gx, gy in grass:
            x = gx / 170 * width
            y = gy / 100 * height
            canvas.line(x, y, x + 1.3 * mm, y + 1.5 * mm)
            canvas.line(x + 1.8 * mm, y, x + 2.2 * mm, y + 1.7 * mm)
        trees = [(8, 77, 8), (35, 93, 7), (71, 84, 9), (153, 84, 8), (166, 48, 7), (25, 12, 9), (64, 8, 7), (134, 17, 10)]
        for tx, ty, radius in trees:
            x = tx / 170 * width
            y = ty / 100 * height
            canvas.setFillColor(colors.HexColor("#8C664E"))
            canvas.rect(x - 1.1 * mm, y - radius * mm / 2, 2.2 * mm, radius * mm / 2 + 2 * mm, fill=1, stroke=0)
            canvas.setFillColor(colors.HexColor("#72A968"))
            canvas.circle(x, y, radius * mm, fill=1, stroke=0)
            canvas.setStrokeColor(colors.HexColor("#548D67"))
            canvas.setLineWidth(0.55)
            canvas.circle(x, y, radius * mm, fill=0, stroke=1)
        bushes = [(13, 52), (55, 57), (118, 54), (148, 35), (96, 86)]
        for bx, by in bushes:
            x = bx / 170 * width
            y = by / 100 * height
            canvas.setFillColor(colors.HexColor("#E4C66A"))
            canvas.roundRect(x - 4 * mm, y - 3 * mm, 8 * mm, 6 * mm, 2 * mm, fill=1, stroke=0)
            canvas.setStrokeColor(colors.HexColor("#C69E4D"))
            canvas.setLineWidth(0.45)
            canvas.roundRect(x - 4 * mm, y - 3 * mm, 8 * mm, 6 * mm, 2 * mm, fill=0, stroke=1)

    def draw_frame(self, canvas):
        canvas.setFillColor(colors.HexColor("#F7FAFB"))
        canvas.setStrokeColor(colors.HexColor("#14213D"))
        canvas.setLineWidth(1.0)
        canvas.roundRect(0, 0, self.width, self.height, 5, fill=1, stroke=1)
        canvas.saveState()
        path = canvas.beginPath()
        path.roundRect(0, 0, self.width, self.height, 5)
        canvas.clipPath(path, stroke=0, fill=0)
        self.draw_background(canvas, self.width, self.height)
        canvas.restoreState()

    def draw_gems(self, canvas):
        start_x = self.width - 61 * mm
        y = self.height - 12 * mm
        self.text(canvas, "가넷  토파즈  자수정  사파이어  다이아", start_x, y + 6 * mm, 5.5, "#253044")
        for index, gem_color in enumerate(self.GEM_COLORS):
            cx = start_x + index * 12 * mm + 3 * mm
            canvas.setFillColor(colors.HexColor(gem_color))
            canvas.setStrokeColor(colors.HexColor("#F8F4E8"))
            canvas.setLineWidth(0.9)
            canvas.circle(cx, y, 2.7 * mm, fill=1, stroke=1)
            self.text(canvas, "0", cx, y - 6 * mm, 6.2, "#253044", "center")

    def draw_timer(self, canvas):
        self.box(canvas, 5 * mm, self.height - 18 * mm, 28 * mm, 9 * mm, ("TIME 00:12" if self.kind == "normal-field" else "TIME 01:00"), fill="#F8F4E8", stroke="#526071", size=6.8)

    def draw_attack_system(self, canvas):
        cx = self.width * 0.5
        cy = 13 * mm
        canvas.setStrokeColor(colors.HexColor("#1F7182"))
        canvas.setLineWidth(1.0)
        canvas.circle(cx, cy, 7 * mm, fill=0, stroke=1)
        canvas.circle(cx, cy, 3 * mm, fill=0, stroke=1)
        canvas.line(cx - 10 * mm, cy, cx + 10 * mm, cy)
        canvas.line(cx, cy - 10 * mm, cx, cy + 10 * mm)
        self.text(canvas, "커서 위치 = 공격 범위", cx, cy + 10 * mm, 7.4, "#1F7182", "center")

    def draw_menu(self, canvas):
        cx = self.width * 0.5
        self.text(canvas, "CURSOR HUNTER", cx, self.height * 0.70, 12, "#14213D", "center")
        labels = ["일반 Field", "보스 Field", "특성/업그레이드", "설정"]
        for index, label in enumerate(labels):
            y = self.height * 0.56 - index * 10 * mm
            self.text(canvas, label, cx, y, 8.5, "#253044", "center")
            canvas.setStrokeColor(colors.HexColor("#526071"))
            canvas.setLineWidth(0.65)
            canvas.line(cx - 19 * mm, y - 2.2 * mm, cx + 19 * mm, y - 2.2 * mm)

    def draw_normal_entities(self, canvas):
        positions = [(35, 55), (70, 69), (101, 48), (132, 70), (145, 31)]
        for index, (px, py) in enumerate(positions):
            x = px / 170 * self.width
            y = py / 100 * self.height
            canvas.setFillColor(colors.HexColor("#E7A84E" if index % 2 == 0 else "#7D9F6D"))
            canvas.circle(x, y, 4 * mm, fill=1, stroke=0)
            canvas.setStrokeColor(colors.HexColor("#F8F4E8"))
            canvas.setLineWidth(0.75)
            canvas.circle(x, y, 4 * mm, fill=0, stroke=1)
        self.text(canvas, "자동 출현 / 랜덤 위치", self.width * 0.5, self.height * 0.45, 7.2, "#526071", "center")

    def draw_boss(self, canvas):
        cx = self.width * 0.5
        cy = self.height * 0.53
        canvas.setFillColor(colors.HexColor("#8065A5"))
        canvas.setStrokeColor(colors.HexColor("#5D467E"))
        canvas.setLineWidth(0.9)
        canvas.circle(cx, cy, 15 * mm, fill=1, stroke=1)
        canvas.setFillColor(colors.HexColor("#E5C65E"))
        canvas.circle(cx - 5 * mm, cy + 3 * mm, 2.1 * mm, fill=1, stroke=0)
        canvas.circle(cx + 5 * mm, cy + 3 * mm, 2.1 * mm, fill=1, stroke=0)
        self.text(canvas, "BOSS", cx, cy - 2.5 * mm, 8.5, "#F8F4E8", "center")
        self.text(canvas, "일반 몬스터 없음", cx, cy - 22 * mm, 7.2, "#526071", "center")

    def draw_hp_bar(self, canvas):
        x, y, width, height = 8 * mm, self.height - 29 * mm, self.width - 16 * mm, 6 * mm
        canvas.setFillColor(colors.HexColor("#F8F4E8"))
        canvas.setStrokeColor(colors.HexColor("#526071"))
        canvas.roundRect(x, y, width, height, 2 * mm, fill=1, stroke=1)
        canvas.setFillColor(colors.HexColor("#C85A5A"))
        canvas.roundRect(x + 1 * mm, y + 1 * mm, width * 0.72, height - 2 * mm, 1.2 * mm, fill=1, stroke=0)
        self.text(canvas, "BOSS HP", x + width / 2, y + 1.3 * mm, 6.8, "#253044", "center")

    def draw_settlement(self, canvas):
        overlay = colors.Color(0.05, 0.08, 0.12, alpha=0.48)
        canvas.setFillColor(overlay)
        canvas.rect(0, 0, self.width, self.height, fill=1, stroke=0)
        modal_x, modal_y = 31 * mm, 10 * mm
        modal_w, modal_h = self.width - 62 * mm, self.height - 20 * mm
        self.box(canvas, modal_x, modal_y, modal_w, modal_h, "", fill="#F8F4E8", stroke="#D6A33F", radius=4)
        self.text(canvas, "FIELD 정산", modal_x + modal_w / 2, modal_y + modal_h - 10 * mm, 11, "#14213D", "center")
        self.box(canvas, modal_x + 5 * mm, modal_y + modal_h - 12 * mm, 14 * mm, 7 * mm, "MAIN", fill="#E7EFF0", stroke="#9DB7D8", size=6.5)
        self.text(canvas, "잼 스톤 획득량", modal_x + 8 * mm, modal_y + modal_h - 22 * mm, 7.2, "#526071")
        for index, gem_color in enumerate(self.GEM_COLORS):
            x = modal_x + 9 * mm + (index % 5) * 14 * mm
            y = modal_y + modal_h - 29 * mm
            canvas.setFillColor(colors.HexColor(gem_color))
            canvas.circle(x, y, 2.2 * mm, fill=1, stroke=0)
            self.text(canvas, "0", x + 4 * mm, y - 1.8 * mm, 6.2, "#253044")
        self.text(canvas, "DPS  0000", modal_x + 10 * mm, modal_y + 28 * mm, 8, "#253044")
        self.text(canvas, "몬스터 처치  000", modal_x + 10 * mm, modal_y + 20 * mm, 8, "#253044")
        labels = ["일반", "보스", "특성/업그레이드"]
        button_w = (modal_w - 22 * mm) / 3
        for index, label in enumerate(labels):
            self.box(canvas, modal_x + 7 * mm + index * (button_w + 4 * mm), modal_y + 6 * mm, button_w, 8 * mm, label, fill="#E7EFF0", stroke="#9DB7D8", size=6.7)

    def panel(self, c, x, y, w, h, title):
        self.box(c, x*mm,y*mm,w*mm,h*mm,"",fill="#F7FAFB",stroke="#A7BAC4")
        self.text(c,title,(x+3)*mm,(y+h-7)*mm,8.5)

    def rows(self,c,values,x,y,gap=8,size=7):
        for i,v in enumerate(values):
            self.text(c,v,x*mm,(y-i*gap)*mm,size)

    def draw_traits(self,c,kind):
        # Fixed HUD surrounds a pannable branching tree. No purchased branch locks another.
        bg="#121D36"; ink="#E7EDF8"; muted="#A8B7D1"; gold="#EDBD51"
        self.box(c,0,0,self.width,self.height,"",fill=bg,stroke="#263A59")
        for i,(label,key) in enumerate([("Stat","attack"),("Skill","skills"),("Monster","monsters")]):
            selected=kind.endswith(key)
            self.box(c,(4+i*27)*mm,81*mm,25*mm,11*mm,label,
                     fill=gold if selected else "#2B3E59",stroke=gold if selected else "#667C99",
                     text_color=bg if selected else ink,size=9)
        self.text(c,"< Main    전체 보기    선택 위치",4*mm,75*mm,6.5,muted)
        # The four bars use fixed final denominators, not arbitrary combat scores.
        self.box(c,112*mm,73*mm,54*mm,20*mm,"",fill="#22324D",stroke="#667C99")
        self.text(c,"현재 특성  [상세]",115*mm,89*mm,6.8,ink)
        for j,(label,value,fraction) in enumerate([
            ("공격","100 / L10",10/30),("범위","60px / Lv2",2/6),
            ("다중","2타",2/3),("자동","미구매",0)]):
            y=85-j*3.2
            self.text(c,label,115*mm,y*mm,5.2,muted)
            c.setFillColor(colors.HexColor("#455674"));c.rect(125*mm,y*mm,17*mm,1.8*mm,fill=1,stroke=0)
            c.setFillColor(colors.HexColor(gold));c.rect(125*mm,y*mm,17*fraction*mm,1.8*mm,fill=1,stroke=0)
            self.text(c,value,144*mm,y*mm,5.2,ink)
        self.text(c,"잼 스톤",5*mm,65*mm,7.5,ink)
        for j,(name,value,color) in enumerate(zip(
            ["가넷","토파즈","자수정","사파이어","다이아"],
            ["50,000","85","30","15","0"],self.GEM_COLORS)):
            y=55-j*8
            self.box(c,4*mm,y*mm,31*mm,7*mm,"",fill="#22324D",stroke="#526586")
            c.setFillColor(colors.HexColor(color));c.circle(7*mm,(y+3.5)*mm,1.5*mm,fill=1,stroke=0)
            self.text(c,name,10*mm,(y+2.5)*mm,5.8,ink)
            self.text(c,value,33*mm,(y+2.5)*mm,5.8,ink,"right")
        self.text(c,"빈 공간 드래그 / 휠 확대",42*mm,70*mm,6,muted)
        def edge(x1,y1,x2,y2):
            c.setStrokeColor(colors.HexColor(gold));c.setLineWidth(1.6)
            c.line(x1*mm,y1*mm,x2*mm,y2*mm)
        def node(x,y,label,state="open"):
            fill={"owned":"#3D615E","open":"#30466B","locked":"#33354D","selected":"#496691"}[state]
            width = 18 if label == "시작 / 무료" else 24
            self.box(c,(x-width/2)*mm,(y-3.5)*mm,width*mm,7*mm,label,fill=fill,
                     stroke=gold if state!="locked" else "#7A6689",text_color=ink,size=5.8)
        # The root is free. The shared junction is not an upgrade node.
        ys=[62,51,40,29] if kind.endswith("attack") else [64,55,46,37,28]
        edge(55,46,60,46);edge(60,min(ys),60,max(ys))
        for y in ys:edge(60,y,72,y)
        if kind.endswith("attack"):
            for y in ys:edge(96,y,122,y)
            node(46,46,"시작 / 무료","owned")
            for y,left,right,ls,rs in [
                (62,"공격 Lv10","다음 Lv11","owned","open"),
                (51,"범위 Lv2","범위 Lv3","owned","open"),
                (40,"더블 / 완료","트리플 / 선택","owned","selected"),
                (29,"자동 / v3","속도 / v4","locked","locked")]:
                node(84,y,left,ls);node(134,y,right,rs)
            detail="트리플: 2타 -> 3타 | v2 + 더블 충족 | 가넷5,000 + 토파즈25"
            button="구매"
        elif kind.endswith("skills"):
            node(46,46,"시작 / 무료","owned")
            for y,name,gate,state in zip(ys,["파이어 볼","라이트닝","프리징","허리케인","브레스"],
                                         ["피해 Lv1","피해 Lv1","강화 / 선행","강화 / 선행","강화 / 선행"],
                                         ["owned","selected","locked","locked","locked"]):
                edge(96,y,122,y)
                node(84,y,name if state!="locked" else name+" 잠금",state)
                node(134,y,gate,"open" if state=="owned" else "locked")
            detail="라이트닝: 0.8A / 8초 | v2 충족 | 가넷40,000 + 자수정12"
            button="해금 구매"
        else:
            node(46,46,"시작 / 무료","owned")
            # Two species branches are expanded; remaining branches are visible on the map below.
            node(84,64,"슬라임 / 완료","owned")
            node(84,55,"고블린 / 완료","owned")
            node(84,46,"오크 / 가능","open")
            node(84,37,"골렘 / v3","locked")
            node(84,28,"와이번 / v4","locked")
            edge(96,64,104,64);edge(104,59,104,67);edge(104,67,122,67);edge(104,59,122,59)
            node(134,67,"出현0/3".replace("出","출"),"open");node(134,59,"보상0/3","open")
            edge(96,55,108,55);edge(108,43,108,55);edge(108,51,122,51);edge(108,43,122,43)
            node(134,51,"출현0/3 선택","selected");node(134,43,"보상0/3","open")
            self.text(c,"아래: 드래곤(v5) 가지 / 이동해 보기",106*mm,32*mm,5.8,muted)
            detail="고블린 출현: 3초 -> 2.61초 | 가넷2,000 | 보상 강화와 독립"
            button="출현 강화"
        self.box(c,4*mm,3*mm,162*mm,16*mm,"",fill="#263955",stroke="#6F86A5")
        self.text(c,detail,7*mm,13*mm,6.2,ink)
        self.text(c,"체크=완료 / 밝은 테두리=구매 가능 / 잠금=조건 미달",7*mm,6*mm,5.7,muted)
        self.box(c,141*mm,8*mm,21*mm,8*mm,button,fill=gold,stroke=gold,text_color=bg,size=7)
        self.text(c,"다른 가지도 자유롭게 구매 가능",160*mm,21*mm,5.9,muted,"right")

    def draw_result(self,c,failed=False):
        c.setFillColor(colors.Color(0.05,0.08,0.12,alpha=.55));c.rect(0,0,self.width,self.height,fill=1,stroke=0)
        self.panel(c,13,6,144,84,"")
        self.text(c,"⌂ MAIN".replace("⌂","<"),18*mm,81*mm,7)
        self.text(c,"보스 도전 실패" if failed else "일반 Field 완료",85*mm,79*mm,11,align="center")
        self.text(c,"전투 48초 / 시간 차감 12초" if failed else "전투 60초 / 시간 종료",85*mm,71*mm,7,align="center")
        self.rows(c,["최종 보상: 전 종류 0" if failed else "보석           기본     보너스     최종",
                     "예약 보너스: 처치 실패로 미지급" if failed else "가넷             96           0           96",
                     "남은 보스 HP 2,100 / 12,000" if failed else "토파즈 / 자수정 / 사파이어 / 다이아: 0",
                     "실측 DPS 206.3 / 파훼 성공2 · 실패3" if failed else "실측 DPS 10.7 / 슬라임 32마리 처치"],22,59,9,7)
        labels=["일반","보스 재도전" if failed else "보스 선택","특성 / 업그레이드"]
        for i,label in enumerate(labels):
            self.text(c,label,(36+i*49)*mm,15*mm,8,align="center")
        if failed:
            self.text(c,"공격력 성장 후 다시 도전하세요",85*mm,24*mm,7,"#A64747","center")

    def draw(self):
        c=self.canv;c.saveState()
        c.translate(self.width * (1-self.display_scale)/2, 0)
        c.scale(self.display_scale,self.display_scale)
        self.draw_frame(c)
        if self.kind.startswith("traits-"):
            self.draw_traits(c,self.kind)
        elif self.kind=="splash":
            self.box(c,0,0,self.width,self.height,"",fill="#17263B")
            self.text(c,"CURSOR HUNTER",self.width/2,54*mm,19,"#FFFFFF","center")
            self.text(c,"커서 하나로 시작되는 사냥",self.width/2,42*mm,9,"#BED8E3","center")
            self.box(c,45*mm,20*mm,80*mm,7*mm,"초기화 중  80%",fill="#DAE9ED",size=7)
            self.text(c,"실패 시: 재시도 / 백업 복구 / 종료",self.width/2,10*mm,7,"#BED8E3","center")
        elif self.kind=="boss-select":
            self.box(c,0,0,self.width,self.height,"",fill="#EAF0F3")
            self.text(c,"< 돌아가기     보스 선택",6*mm,86*mm,10)
            self.panel(c,5,17,35,61,"보스 목록")
            self.rows(c,["v1  처치 완료","v2  선택","v3  잠김","v4  잠김","v5  잠김"],8,60,9,7.5)
            self.panel(c,43,17,61,61,"v2 고블린 족장")
            self.rows(c,["HP 12,000 / 60초","표식 2개 순서대로 클릭","실패 시 -4초","권장: 공격력 Lv10"],47,58,9,7)
            self.panel(c,107,17,58,61,"처치 보상 / 해금")
            self.rows(c,["가넷2,000 / 토파즈35","자수정20","트리플·오크·라이트닝","현재 이론 DPS 400","추천 레벨은 입장 제한 아님"],111,60,8,6.5)
            self.text(c,"도전하기",85*mm,7*mm,10,align="center")
        elif self.kind=="settings":
            self.box(c,0,0,self.width,self.height,"",fill="#EAF0F3")
            self.text(c,"< 돌아가기       설정",6*mm,86*mm,10)
            self.panel(c,8,21,72,58,"오디오 / 화면")
            self.rows(c,["전체 음량     ───── 80%","음악 / 효과음     60% / 80%","창 모드     1920 x 1080","UI 배율     100%"],12,60,10,7)
            self.panel(c,85,21,77,58,"가독성 / 입력")
            self.rows(c,["이펙트 강도     낮음 / 보통 / 높음","화면 흔들림     OFF","피해 숫자     묶음 표시","자동 클릭 [A]     해금 후 활성"],89,60,10,7)
            self.text(c,"변경 취소                       적용",85*mm,10*mm,9,align="center")
        else:
            self.draw_gems(c)
            if self.kind=="main":self.draw_menu(c)
            elif self.kind=="normal-field":
                self.draw_timer(c)
                palette=["#71905D","#B6984A","#967459","#668C9C","#9974AF"]
                for group,(gx,gy) in enumerate([(30,53),(59,62),(83,39),(115,55),(140,37)]):
                    c.setFillColor(colors.HexColor(palette[group]))
                    for j in range(3):
                        for i in range(5):
                            c.circle((gx+i*3+(j%2))*mm,(gy+j*3)*mm,1.1*mm,fill=1,stroke=0)
                c.setStrokeColor(colors.HexColor("#1F7182"));c.setLineWidth(1)
                c.circle(89*mm,44*mm,17*mm,fill=0,stroke=1)
                self.text(c,"커서 범위 / 다수 동시 타격",89*mm,22*mm,6.5,align="center")
                c.setStrokeColor(colors.HexColor("#B84646"));c.line(96*mm,48*mm,114*mm,75*mm)
                c.line(114*mm,75*mm,110*mm,72*mm);c.line(114*mm,75*mm,114*mm,70*mm)
                self.text(c,"보석 자석 이동",128*mm,63*mm,6,align="center")
                self.text(c,"후반 물량 예시 / 슬라임부터 모든 종 중첩",75*mm,74*mm,6.2,align="center")
                self.text(c,"이번 판 +1.2억 가넷",162*mm,71*mm,6,align="right")
                self.text(c,"자동 ON [A]  30묶음 x 3타 / ESC 일시정지",85*mm,7*mm,6.5,align="center")
            elif self.kind=="boss-field":
                self.draw_timer(c);self.draw_hp_bar(c);self.draw_boss(c)
                for i in range(2):
                    self.box(c,(124+i*15)*mm,39*mm,12*mm,12*mm,str(i+1),fill="#F4D98C",size=9)
                self.text(c,"순서대로 클릭 / 남은 1.6초",127*mm,32*mm,6,align="center")
                self.text(c,"파훼 성공 2회 / 처치 시 보너스 +10%",85*mm,8*mm,7,align="center")
            elif self.kind in ("settlement","failure"):
                self.draw_result(c,self.kind=="failure")
        c.restoreState()


def markdown_flowables(markdown: str, style_map: dict[str, ParagraphStyle]):
    lines = markdown.splitlines()
    flowables = []
    index = 0
    paragraph_buffer: list[str] = []
    pending_wireframe_heading = None

    def flush_paragraph() -> None:
        if paragraph_buffer:
            text = " ".join(part.strip() for part in paragraph_buffer)
            flowables.append(Paragraph(inline_markup(text), style_map["body"]))
            paragraph_buffer.clear()

    while index < len(lines):
        line = lines[index]
        stripped = line.strip()

        if not stripped:
            flush_paragraph()
            if pending_wireframe_heading is None:
                flowables.append(Spacer(1, 1.2 * mm))
            index += 1
            continue

        if stripped == "<!-- pagebreak -->":
            flush_paragraph()
            flowables.append(PageBreak())
            index += 1
            continue

        if stripped.startswith("```"):
            flush_paragraph()
            language = stripped[3:].strip().lower()
            code_lines: list[str] = []
            index += 1
            while index < len(lines) and not lines[index].strip().startswith("```"):
                code_lines.append(lines[index])
                index += 1
            if language.startswith("wireframe"):
                parts = language.split(maxsplit=1)
                kind = parts[1] if len(parts) == 2 else "unknown"
                wireframe = WireframeFlowable(kind)
                if pending_wireframe_heading is not None:
                    flowables.append(KeepTogether([pending_wireframe_heading, wireframe, Spacer(1, 3 * mm)]))
                    pending_wireframe_heading = None
                else:
                    flowables.append(wireframe)
                    flowables.append(Spacer(1, 3 * mm))
            else:
                code_html = "<br/>".join(escape(code_line) for code_line in code_lines)
                flowables.append(Paragraph(code_html, style_map["code"]))
            index += 1
            continue

        if stripped.startswith("|") and stripped.endswith("|"):
            flush_paragraph()
            rows: list[list[str]] = []
            while index < len(lines):
                candidate = lines[index].strip()
                if not (candidate.startswith("|") and candidate.endswith("|")):
                    break
                if not is_table_separator(candidate):
                    rows.append(table_cells(candidate))
                index += 1
            if rows:
                flowables.append(KeepTogether([make_table(rows, style_map), Spacer(1, 2.2 * mm)]))
            continue

        heading = re.match(r"^(#{1,4})\s+(.*)$", stripped)
        if heading:
            flush_paragraph()
            level = len(heading.group(1))
            text = inline_markup(heading.group(2))
            if level == 1:
                heading_flowable = Paragraph(text, style_map["h1"])
            elif level == 2:
                heading_flowable = Paragraph(text, style_map["h2"])
            elif level == 3:
                heading_flowable = Paragraph(text, style_map["h3"])
            else:
                heading_flowable = Paragraph(text, style_map["h4"])
            lookahead = index + 1
            while lookahead < len(lines) and not lines[lookahead].strip():
                lookahead += 1
            if lookahead < len(lines) and lines[lookahead].strip().lower().startswith("```wireframe"):
                pending_wireframe_heading = heading_flowable
            else:
                flowables.append(heading_flowable)
            index += 1
            continue

        if stripped.startswith(">"):
            flush_paragraph()
            quote_lines = [stripped[1:].strip()]
            index += 1
            while index < len(lines) and lines[index].strip().startswith(">"):
                quote_lines.append(lines[index].strip()[1:].strip())
                index += 1
            quote_text = " ".join(quote_lines)
            flowables.append(Paragraph(inline_markup(quote_text), style_map["quote"]))
            continue

        bullet = re.match(r"^[-*]\s+(.*)$", stripped)
        if bullet:
            flush_paragraph()
            flowables.append(Paragraph("• " + inline_markup(bullet.group(1)), style_map["bullet"]))
            index += 1
            continue

        numbered = re.match(r"^(\d+)\.\s+(.*)$", stripped)
        if numbered:
            flush_paragraph()
            flowables.append(Paragraph(f"{numbered.group(1)}. " + inline_markup(numbered.group(2)), style_map["bullet"]))
            index += 1
            continue

        if stripped.startswith("상태:") or stripped.startswith("작성일:") or stripped.startswith("대상:"):
            flush_paragraph()
            flowables.append(Paragraph(inline_markup(stripped), style_map["meta"]))
            index += 1
            continue

        paragraph_buffer.append(stripped)
        index += 1

    flush_paragraph()
    return flowables


def draw_page(canvas, document) -> None:
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
    canvas.drawString(18 * mm, 9 * mm, "게임 기획서 v0.0.3 / 개정 3 · 2시간 엔딩 초안")
    canvas.drawRightString(width - 18 * mm, 9 * mm, f"{canvas.getPageNumber():02d}")
    canvas.setStrokeColor(colors.HexColor("#D6E2E5"))
    canvas.setLineWidth(0.45)
    canvas.line(18 * mm, 13 * mm, width - 18 * mm, 13 * mm)
    canvas.restoreState()


def main() -> None:
    register_fonts()
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    style_map = styles()
    source_text = SOURCE.read_text(encoding="utf-8")
    document = BaseDocTemplate(
        str(OUTPUT),
        pagesize=A4,
        leftMargin=18 * mm,
        rightMargin=18 * mm,
        topMargin=17 * mm,
        bottomMargin=18 * mm,
        title="커서 액션 아이들러 게임 기획서 v0.0.3",
        author="Codex",
    )
    frame = Frame(document.leftMargin, document.bottomMargin, document.width, document.height, id="body")
    document.addPageTemplates([PageTemplate(id="plan", frames=[frame], onPage=draw_page)])
    document.build(markdown_flowables(source_text, style_map))
    print(OUTPUT)


if __name__ == "__main__":
    main()
