"""ADR-Manager – kleine GUI zur Verwaltung von Architecture Decision Records.

Liest/schreibt Markdown-Dateien im Format des Samples aus dem konfigurierten
ADR-Ordner (Standard: ../adr):

    # ADR-<nummer> – <titel>

    **Status:** <status>
    **Datum:** <datum>
    **Entscheider:** <entscheider>
    **Implementierungsplan:** [<datei>](<relativer pfad>)   (Pflicht ab Status Accepted)
    **C4-Diagramm:** [<datei>.drawio](<relativer pfad>)     (optional, draw.io)
    **Commit:** <hash>, <hash>                              (Pflicht ab "Implementation Tested and Acceptance")

    <freier Markdown-Inhalt: Kontext, Entscheidung, Konsequenzen, ...>
"""

from __future__ import annotations

import argparse
import base64
import html
import json
import os
import re
import subprocess
import sys
import tkinter as tk
import urllib.parse
import xml.etree.ElementTree as ET
import zlib
from dataclasses import dataclass, field
from datetime import date
from pathlib import Path
from tkinter import filedialog, font as tkfont, messagebox
from typing import List, Optional

import customtkinter as ctk

APP_DIR = Path(__file__).resolve().parent
CONFIG_PATH = APP_DIR / "config.json"
DEFAULT_ADR_DIR = APP_DIR.parent / "adr"
DEFAULT_PLAN_DIR = APP_DIR.parent / "features"

PLAN_KEY = "Implementierungsplan"
C4_KEY = "C4-Diagramm"
COMMIT_KEY = "Commit"
COMMIT_RE = re.compile(r"^[0-9a-f]{7,40}$")
REPO_ROOT = APP_DIR.parent.parent
PLAN_LINK_RE = re.compile(r"\[[^\]]*\]\(([^)]+)\)")
TITLE_RE = re.compile(r"^#\s*ADR-(\d+)\s*[–-]\s*(.*)$")
META_RE = re.compile(r"^\*\*([^*:]+):\*\*\s*(.*)$")

STATUS_PROPOSED = "Proposed"
STATUS_ACCEPTED = "Accepted"
STATUS_IMPLEMENTED = "Implemented"
STATUS_TESTED = "Implementation Tested and Acceptance"
STATUS_SECURITY = "Security Review"
STATUS_GDPR = "GDPR/Compliance Review"
STATUS_FULL = "Full Acceptance (Final)"
STATUS_REJECTED = "Rejected"
STATUS_DEPRECATED = "Deprecated"

STATUS_OPTIONS = [
    STATUS_PROPOSED,
    STATUS_ACCEPTED,
    STATUS_IMPLEMENTED,
    STATUS_TESTED,
    STATUS_SECURITY,
    STATUS_GDPR,
    STATUS_FULL,
    STATUS_REJECTED,
    STATUS_DEPRECATED,
]
STATUS_BADGE = {
    STATUS_PROPOSED: "🟡",
    STATUS_ACCEPTED: "🟢",
    STATUS_IMPLEMENTED: "🔵",
    STATUS_TESTED: "✅",
    STATUS_SECURITY: "🛡",
    STATUS_GDPR: "⚖",
    STATUS_FULL: "🏁",
    STATUS_REJECTED: "🔴",
    STATUS_DEPRECATED: "⚪",
}

# Farben je Status: kraeftig (Rahmen/Chip) und Tint (Hintergrund, hell/dunkel)
STATUS_COLOR = {
    STATUS_PROPOSED: "#d4a017",
    STATUS_ACCEPTED: "#2e9e4f",
    STATUS_IMPLEMENTED: "#2f7fd1",
    STATUS_TESTED: "#14a08a",
    STATUS_SECURITY: "#8e44ad",
    STATUS_GDPR: "#d35400",
    STATUS_FULL: "#1e8449",
    STATUS_REJECTED: "#c0392b",
    STATUS_DEPRECATED: "#8a8a8a",
}
STATUS_TINT = {
    STATUS_PROPOSED: ("#fff6d6", "#3a3317"),
    STATUS_ACCEPTED: ("#dcf3e2", "#17341f"),
    STATUS_IMPLEMENTED: ("#dbeafa", "#172c42"),
    STATUS_TESTED: ("#d2f3ec", "#15352f"),
    STATUS_SECURITY: ("#eadcf3", "#2f1c3a"),
    STATUS_GDPR: ("#fbe3d0", "#3d2412"),
    STATUS_FULL: ("#c8ecd3", "#10381f"),
    STATUS_REJECTED: ("#f8dcd8", "#3d1d1a"),
    STATUS_DEPRECATED: ("#e6e6e6", "#2e2e2e"),
}
STATUS_SHORT = {STATUS_TESTED: "Tested", STATUS_SECURITY: "Security", STATUS_GDPR: "GDPR", STATUS_FULL: "Full Accept."}

# Erlaubte Statuswechsel (Lebenszyklus). Ein unveraenderter Status ist immer erlaubt.
# Rueckwaerts nur als Nacharbeit: Implemented -> Accepted; Tested und die Reviews -> Implemented.
STATUS_TRANSITIONS = {
    STATUS_PROPOSED: [STATUS_ACCEPTED, STATUS_REJECTED],
    STATUS_ACCEPTED: [STATUS_IMPLEMENTED, STATUS_DEPRECATED],
    STATUS_IMPLEMENTED: [STATUS_TESTED, STATUS_ACCEPTED, STATUS_DEPRECATED],
    STATUS_TESTED: [STATUS_SECURITY, STATUS_IMPLEMENTED, STATUS_DEPRECATED],
    STATUS_SECURITY: [STATUS_GDPR, STATUS_IMPLEMENTED, STATUS_DEPRECATED],
    STATUS_GDPR: [STATUS_FULL, STATUS_IMPLEMENTED, STATUS_DEPRECATED],
    STATUS_FULL: [STATUS_DEPRECATED],
    STATUS_REJECTED: [STATUS_PROPOSED],
    STATUS_DEPRECATED: [],
}

# Ab diesen Status muss ein Implementierungsplan vorliegen und im ADR verlinkt sein.
PLAN_REQUIRED_STATUS = {STATUS_ACCEPTED, STATUS_IMPLEMENTED, STATUS_TESTED, STATUS_SECURITY, STATUS_GDPR, STATUS_FULL}

# Ab diesen Status muss der Implementierungs-Commit (mindestens einer) im ADR stehen.
COMMIT_REQUIRED_STATUS = {STATUS_TESTED, STATUS_SECURITY, STATUS_GDPR, STATUS_FULL}

# Vorlagen/Beispiele (Nummer >= 900000) sind von der Planpflicht ausgenommen.
SAMPLE_NUMBER_MIN = 900000

DEFAULT_BODY_TEMPLATE = """## Kontext
Beschreibe die Ausgangslage und das Problem, das diese Entscheidung adressiert.

## Entscheidung
Beschreibe die getroffene Entscheidung.

## Konsequenzen
- Positive und negative Auswirkungen der Entscheidung

## Alternativen
- Welche Alternativen wurden erwogen?

## Links
- Referenzen, Issues, weiterführende Dokumente
"""


@dataclass
class ADR:
    number: str
    title: str
    status: str
    datum: str
    entscheider: str
    body: str
    path: Optional[Path] = field(default=None)
    plan: str = ""  # Pfad des Implementierungsplans, relativ zum ADR-Ordner
    commits: str = ""  # Implementierungs-Commit(s), kommagetrennt
    c4: str = ""  # optional: Pfad des C4-Diagramms (draw.io), relativ zum ADR-Ordner

    @property
    def is_sample(self) -> bool:
        return self.number.isdigit() and int(self.number) >= SAMPLE_NUMBER_MIN

    @property
    def filename(self) -> str:
        return f"ADR-{self.number}-{slugify(self.title)}.md"


def slugify(title: str) -> str:
    s = title.strip().lower()
    replacements = {"ä": "ae", "ö": "oe", "ü": "ue", "ß": "ss"}
    for src, dst in replacements.items():
        s = s.replace(src, dst)
    s = re.sub(r"[^a-z0-9\s-]", "", s)
    s = re.sub(r"[\s_]+", "-", s).strip("-")
    return s or "adr"


def parse_plan_value(raw: str) -> str:
    raw = raw.strip()
    m = PLAN_LINK_RE.search(raw)
    return (m.group(1) if m else raw).strip()


def parse_adr(path: Path) -> ADR:
    lines = path.read_text(encoding="utf-8").split("\n")
    number, title = "", path.stem
    idx = 0
    if lines and lines[0].startswith("#"):
        m = TITLE_RE.match(lines[0])
        if m:
            number, title = m.group(1), m.group(2).strip()
        idx = 1

    meta = {"Status": "", "Datum": "", "Entscheider": "", PLAN_KEY: "", C4_KEY: "", COMMIT_KEY: ""}
    while idx < len(lines):
        stripped = lines[idx].strip()
        if not stripped:
            idx += 1
            continue
        m = META_RE.match(stripped)
        if not m:
            break
        meta[m.group(1).strip()] = m.group(2).strip()
        idx += 1

    while idx < len(lines) and lines[idx].strip() == "":
        idx += 1
    body = "\n".join(lines[idx:]).rstrip("\n")

    return ADR(
        number=number,
        title=title,
        status=meta.get("Status", ""),
        datum=meta.get("Datum", ""),
        entscheider=meta.get("Entscheider", ""),
        body=body,
        path=path,
        plan=parse_plan_value(meta.get(PLAN_KEY, "")),
        c4=parse_plan_value(meta.get(C4_KEY, "")),
        commits=meta.get(COMMIT_KEY, "").strip(),
    )


def render_adr(adr: ADR) -> str:
    header = f"# ADR-{adr.number} – {adr.title}"
    meta_lines = [
        f"**Status:** {adr.status}",
        f"**Datum:** {adr.datum}",
        f"**Entscheider:** {adr.entscheider}",
    ]
    if adr.plan:
        meta_lines.append(f"**{PLAN_KEY}:** [{Path(adr.plan).name}]({adr.plan})")
    if adr.c4:
        meta_lines.append(f"**{C4_KEY}:** [{Path(adr.c4).name}]({adr.c4})")
    if adr.commits.strip():
        meta_lines.append(f"**{COMMIT_KEY}:** {adr.commits.strip()}")
    body = adr.body.strip("\n")
    return "\n".join([header, "", *meta_lines, "", body, ""])


def load_config() -> dict:
    if CONFIG_PATH.exists():
        try:
            return json.loads(CONFIG_PATH.read_text(encoding="utf-8"))
        except (json.JSONDecodeError, OSError):
            return {}
    return {}


def save_config(cfg: dict) -> None:
    CONFIG_PATH.write_text(json.dumps(cfg, indent=2, ensure_ascii=False), encoding="utf-8")


def get_adr_dir() -> Path:
    raw = load_config().get("adr_dir")
    return Path(raw).expanduser() if raw else DEFAULT_ADR_DIR


def set_adr_dir(path: Path) -> None:
    cfg = load_config()
    cfg["adr_dir"] = str(path)
    save_config(cfg)


def get_plan_dir() -> Path:
    raw = load_config().get("plan_dir")
    return Path(raw).expanduser() if raw else DEFAULT_PLAN_DIR


def set_plan_dir(path: Path) -> None:
    cfg = load_config()
    cfg["plan_dir"] = str(path)
    save_config(cfg)


def load_all_adrs(adr_dir: Path) -> List[ADR]:
    adr_dir.mkdir(parents=True, exist_ok=True)
    adrs = [parse_adr(p) for p in sorted(adr_dir.glob("ADR-*.md"))]
    adrs.sort(key=lambda a: (int(a.number) if a.number.isdigit() else 0))
    return adrs


def next_number(adrs: List[ADR]) -> str:
    real_nums = [int(a.number) for a in adrs if a.number.isdigit() and int(a.number) < 900000]
    n = max(real_nums, default=0) + 1
    width = max([4] + [len(a.number) for a in adrs if a.number.isdigit() and int(a.number) < 900000])
    return str(n).zfill(width)


def transition_error(old: str, new: str) -> Optional[str]:
    """None, wenn der Statuswechsel erlaubt ist, sonst eine Begruendung."""
    if not old or old == new or old not in STATUS_TRANSITIONS:
        return None
    if new in STATUS_TRANSITIONS[old]:
        return None
    allowed = ", ".join(STATUS_TRANSITIONS[old]) or "keiner"
    return f"Statuswechsel '{old}' -> '{new}' ist nicht erlaubt (erlaubt: {allowed})."


def plan_ref(plan_path: Path, adr_dir: Path) -> str:
    return Path(os.path.relpath(plan_path, adr_dir)).as_posix()


def plan_file(adr: ADR, adr_dir: Path) -> Optional[Path]:
    return (adr_dir / adr.plan).resolve() if adr.plan else None


def plan_problems(adr: ADR, adr_dir: Path) -> List[str]:
    """Verstoesse gegen die Planpflicht (leer, wenn alles in Ordnung ist)."""
    if adr.is_sample:
        return []
    target = plan_file(adr, adr_dir)
    problems: List[str] = []
    if adr.status in PLAN_REQUIRED_STATUS and target is None:
        problems.append(f"ADR-{adr.number} ({adr.status}): kein Implementierungsplan verlinkt.")
    if target is not None and not target.is_file():
        problems.append(f"ADR-{adr.number}: verlinkter Plan fehlt: {adr.plan}")
    if adr.c4 and not (adr_dir / adr.c4).is_file():
        problems.append(f"ADR-{adr.number}: verlinktes C4-Diagramm fehlt: {adr.c4}")
    return problems


def split_commits(raw: str) -> List[str]:
    return [c.strip().lower() for c in re.split(r"[,\s;]+", raw or "") if c.strip()]


def commit_errors(raw: str) -> List[str]:
    """Prueft Format und (soweit git verfuegbar) Existenz der Commits im Repository."""
    errors: List[str] = []
    for c in split_commits(raw):
        if not COMMIT_RE.match(c):
            errors.append(f"'{c}' ist kein Commit-Hash (7 bis 40 Hex-Zeichen).")
            continue
        try:
            r = subprocess.run(["git", "-C", str(REPO_ROOT), "cat-file", "-e", f"{c}^{{commit}}"], capture_output=True)
        except OSError:
            continue  # git nicht verfuegbar: nur Format pruefen
        if r.returncode != 0:
            errors.append(f"Commit {c} existiert im Repository nicht.")
    return errors


def commit_required_error(adr: ADR) -> Optional[str]:
    if adr.is_sample or adr.status not in COMMIT_REQUIRED_STATUS:
        return None
    if not split_commits(adr.commits):
        return f"Status '{adr.status}' verlangt den Implementierungs-Commit (Feld 'Commit')."
    errs = commit_errors(adr.commits)
    return errs[0] if errs else None


def suggest_commits(number: str) -> List[str]:
    """Kandidaten aus der Git-Historie: Commits, deren Nachricht 'ADR-<nummer>' nennt (aeltester zuerst)."""
    try:
        r = subprocess.run(
            ["git", "-C", str(REPO_ROOT), "log", "--reverse", "--format=%h", "-i", f"--grep=ADR-{number}\\b"],
            capture_output=True, text=True,
        )
    except OSError:
        return []
    return r.stdout.split() if r.returncode == 0 else []


PLAN_TEMPLATE = """# Feature: {title} (Implementierungsplan)

> **Status: Entwurf, {datum}.** Grundlage: [ADR-{number}]({adr_ref}) (**{status}**).
> Dieser Plan zerlegt die Umsetzung in Meilensteine und Aufgaben. Er enthaelt **keinen Code**.

**Rollenkuerzel:** **SE** software-engineer · **T** software-tester · **SEC** it-security · **COMP** it-compliance · **A** software-architekt.

## 0. Umsetzungsstand (laufend fortzuschreiben)

| Aufgabe | Stand | Anmerkung |
| --- | --- | --- |
| 1.1 | offen | |

## 1. Ziel und Abgrenzung

Siehe ADR-{number}: Entscheidung und Nicht-Ziele.

## 2. Meilensteine und Aufgaben

| Nr. | Rolle | Aufgabe | Ergebnis |
| --- | --- | --- | --- |
| 1.1 | SE | | |

## 3. Tests und Abnahme

| Nr. | Rolle | Pruefung | Erwartung |
| --- | --- | --- | --- |
| T.1 | T | | |

Nach erfolgreicher Umsetzung: ADR auf `Implemented`. Nach Tests und Abnahme: ADR auf `Implementation Tested and Acceptance`.

## 4. Prüfnachweise

Je Status bewertet eine Rolle (siehe Skill `adr-workflow`, Schritt 6). Ergebnis und Befunde hier eintragen.

| Status | Rolle | Datum | Ergebnis | Befunde |
| --- | --- | --- | --- | --- |
| Implemented | software-engineer, code-review | | | |
| Implementation Tested and Acceptance | software-tester | | | |
| Security Review | it-security | | | |
| GDPR/Compliance Review | it-compliance | | | |
| Full Acceptance (Final) | software-architekt, Projektinhaber | | | |

## 5. Offene Punkte

- 
"""


def create_plan_stub(adr: ADR, adr_dir: Path, plan_dir: Path) -> Path:
    """Legt ein Plan-Geruest an und setzt adr.plan (die ADR-Datei schreibt der Aufrufer)."""
    slug = slugify(adr.title)[:60].rstrip("-") or "adr"
    target = plan_dir / f"{slug}.md"
    if target.exists():
        target = plan_dir / f"adr-{adr.number}-{slug}.md"
    if target.exists():
        raise FileExistsError(target)
    plan_dir.mkdir(parents=True, exist_ok=True)
    adr_ref = Path(os.path.relpath(adr_dir / adr.filename, plan_dir)).as_posix()
    target.write_text(
        PLAN_TEMPLATE.format(
            title=adr.title, datum=date.today().isoformat(), number=adr.number, adr_ref=adr_ref, status=adr.status
        ),
        encoding="utf-8",
    )
    adr.plan = plan_ref(target, adr_dir)
    return target


def ensure_plan(adr: ADR, adr_dir: Path, plan_dir: Path) -> Optional[Path]:
    """Erzeugt den Plan, falls die Planpflicht gilt und noch keiner verlinkt ist. Gibt den neuen Plan zurueck."""
    if adr.is_sample or adr.status not in PLAN_REQUIRED_STATUS or adr.plan:
        return None
    return create_plan_stub(adr, adr_dir, plan_dir)


def find_adr(adrs: List[ADR], number: str) -> Optional[ADR]:
    for a in adrs:
        if a.number.isdigit() and number.isdigit() and int(a.number) == int(number):
            return a
    return None


def create_new_adr_headless(title: str, status: str, datum: str, entscheider: str, adr_dir: Path) -> Path:
    adrs = load_all_adrs(adr_dir)
    adr = ADR(
        number=next_number(adrs),
        title=title,
        status=status,
        datum=datum,
        entscheider=entscheider,
        body=DEFAULT_BODY_TEMPLATE,
    )
    target = adr_dir / adr.filename
    if target.exists():
        raise FileExistsError(target)
    adr_dir.mkdir(parents=True, exist_ok=True)
    target.write_text(render_adr(adr), encoding="utf-8")
    return target


class RichTextEditor(tk.Frame):
    """Klickbar formatierbarer Editor (Fett/Kursiv/Überschrift/Liste), der
    intern als Markdown gespeichert/gelesen wird."""

    INLINE_RE = re.compile(r"\*\*\*(.+?)\*\*\*|\*\*(.+?)\*\*|\*(.+?)\*")
    TABLE_ROW_RE = re.compile(r"^\s*\|(.+)\|\s*$")
    TABLE_SEP_CELL_RE = re.compile(r"^:?-{1,}:?$")

    def __init__(self, master: tk.Widget) -> None:
        self.is_dark = ctk.get_appearance_mode() == "Dark"
        bg = "#242424" if self.is_dark else "#ffffff"
        fg = "#e6e6e6" if self.is_dark else "#1a1a1a"
        self.cell_bg = "#2b2b2b" if self.is_dark else "#ffffff"
        self.header_bg = "#3a3a3a" if self.is_dark else "#e6e6e6"
        self.border_color = "#555555" if self.is_dark else "#c4c4c4"
        self.fg = fg
        super().__init__(master, bg=bg)

        base_font = tkfont.Font(family="Helvetica", size=13)
        self.bold_font = base_font.copy()
        self.bold_font.configure(weight="bold")
        self.italic_font = base_font.copy()
        self.italic_font.configure(slant="italic")
        self.bolditalic_font = base_font.copy()
        self.bolditalic_font.configure(weight="bold", slant="italic")
        self.heading_font = base_font.copy()
        self.heading_font.configure(size=17, weight="bold")

        self.text = tk.Text(
            self,
            wrap="word",
            font=base_font,
            bg=bg,
            fg=fg,
            insertbackground=fg,
            selectbackground="#3a6ea5",
            relief="flat",
            borderwidth=0,
            undo=True,
            padx=10,
            pady=8,
        )
        scrollbar = tk.Scrollbar(self, command=self.text.yview)
        self.text.configure(yscrollcommand=scrollbar.set)
        self.text.grid(row=0, column=0, sticky="nsew")
        scrollbar.grid(row=0, column=1, sticky="ns")
        self.grid_rowconfigure(0, weight=1)
        self.grid_columnconfigure(0, weight=1)

        self.text.tag_configure("bold", font=self.bold_font)
        self.text.tag_configure("italic", font=self.italic_font)
        self.text.tag_configure("bolditalic", font=self.bolditalic_font)
        self.text.tag_configure("heading", font=self.heading_font)

        self.text.bind("<Control-b>", lambda _e: self.toggle_bold() or "break")
        self.text.bind("<Control-i>", lambda _e: self.toggle_italic() or "break")
        self.text.bind("<KeyPress>", self._on_keypress)

        self.pending_tags: set = set()
        self.on_pending_change = None  # optionaler Callback für Toolbar-Feedback

    # ---------- toolbar actions ----------

    def _selection_range(self) -> Optional[tuple]:
        try:
            return self.text.index("sel.first"), self.text.index("sel.last")
        except tk.TclError:
            return None

    def _range_fully_tagged(self, tag: str, start: str, end: str) -> bool:
        idx = start
        while self.text.compare(idx, "<", end):
            if tag not in self.text.tag_names(idx):
                return False
            idx = self.text.index(f"{idx}+1c")
        return True

    def _toggle_char_tag(self, tag: str) -> None:
        sel = self._selection_range()
        if not sel:
            return
        start, end = sel
        if self._range_fully_tagged(tag, start, end):
            self.text.tag_remove(tag, start, end)
        else:
            self.text.tag_add(tag, start, end)

    def _toggle_format(self, tag: str) -> None:
        if self._selection_range():
            self._toggle_char_tag(tag)
        else:
            if tag in self.pending_tags:
                self.pending_tags.discard(tag)
            else:
                self.pending_tags.add(tag)
            if self.on_pending_change:
                self.on_pending_change(set(self.pending_tags))

    def toggle_bold(self) -> None:
        self._toggle_format("bold")

    def toggle_italic(self) -> None:
        self._toggle_format("italic")

    def _on_keypress(self, event: tk.Event) -> Optional[str]:
        if not self.pending_tags or not event.char or not event.char.isprintable():
            return None
        sel = self._selection_range()
        if sel:
            self.text.delete(sel[0], sel[1])
        self.text.insert("insert", event.char, tuple(self.pending_tags))
        return "break"

    def _selected_line_range(self) -> tuple:
        sel = self._selection_range()
        if sel:
            start_line = int(self.text.index(sel[0]).split(".")[0])
            end_line = int(self.text.index(sel[1]).split(".")[0])
        else:
            cur = int(self.text.index("insert").split(".")[0])
            start_line = end_line = cur
        return start_line, end_line

    def toggle_heading(self) -> None:
        start_line, end_line = self._selected_line_range()
        for line in range(start_line, end_line + 1):
            line_start, line_end = f"{line}.0", f"{line}.end"
            if "heading" in self.text.tag_names(line_start):
                self.text.tag_remove("heading", line_start, line_end)
            else:
                self.text.tag_add("heading", line_start, line_end)

    def toggle_bullet(self) -> None:
        start_line, end_line = self._selected_line_range()
        for line in range(start_line, end_line + 1):
            line_start = f"{line}.0"
            content = self.text.get(line_start, f"{line}.end")
            if content.startswith("• "):
                self.text.delete(line_start, f"{line_start}+2c")
            else:
                self.text.insert(line_start, "• ")

    # ---------- markdown <-> rich text ----------

    def _insert_markdown_line(self, line: str) -> None:
        heading = False
        if line.startswith("## "):
            heading = True
            line = line[3:]
        elif line.startswith("- "):
            self.text.insert("end", "• ")
            line = line[2:]

        line_start_index = self.text.index("end-1c")
        pos = 0
        for m in self.INLINE_RE.finditer(line):
            if m.start() > pos:
                self.text.insert("end", line[pos : m.start()])
            if m.group(1) is not None:
                self.text.insert("end", m.group(1), ("bolditalic",))
            elif m.group(2) is not None:
                self.text.insert("end", m.group(2), ("bold",))
            else:
                self.text.insert("end", m.group(3), ("italic",))
            pos = m.end()
        if pos < len(line):
            self.text.insert("end", line[pos:])

        if heading:
            self.text.tag_add("heading", line_start_index, self.text.index("end-1c"))

    def _try_parse_table(self, lines: List[str], i: int) -> tuple:
        if i + 1 >= len(lines):
            return None, 0
        header_m = self.TABLE_ROW_RE.match(lines[i])
        sep_m = self.TABLE_ROW_RE.match(lines[i + 1])
        if not header_m or not sep_m:
            return None, 0
        sep_cells = [c.strip() for c in sep_m.group(1).split("|")]
        if not sep_cells or not all(self.TABLE_SEP_CELL_RE.match(c) for c in sep_cells):
            return None, 0
        header_cells = [c.strip() for c in header_m.group(1).split("|")]
        rows = [header_cells]
        j = i + 2
        while j < len(lines):
            m = self.TABLE_ROW_RE.match(lines[j])
            if not m:
                break
            rows.append([c.strip() for c in m.group(1).split("|")])
            j += 1
        return rows, j - i

    def load_markdown(self, body: str) -> None:
        self.text.delete("1.0", "end")
        if self.pending_tags:
            self.pending_tags = set()
            if self.on_pending_change:
                self.on_pending_change(set())
        lines = body.split("\n")
        i = 0
        first = True
        while i < len(lines):
            table_rows, consumed = self._try_parse_table(lines, i)
            if not first:
                self.text.insert("end", "\n")
            first = False
            if table_rows is not None:
                self._insert_table(table_rows, at="end")
                i += consumed
            else:
                self._insert_markdown_line(lines[i])
                i += 1
        self.text.edit_reset()

    # ---------- editierbare Tabellen ----------

    def _make_cell_entry(self, frame: tk.Frame, value: str, header: bool) -> tk.Entry:
        var = tk.StringVar(value=value)
        entry = tk.Entry(
            frame,
            textvariable=var,
            width=max(8, len(value) + 2),
            font=self.bold_font if header else self.text.cget("font"),
            bg=self.header_bg if header else self.cell_bg,
            fg=self.fg,
            insertbackground=self.fg,
            relief="solid",
            highlightthickness=1,
            highlightbackground=self.border_color,
            highlightcolor=self.border_color,
            bd=0,
        )
        var.trace_add("write", lambda *_: entry.configure(width=max(8, len(var.get()) + 2)))
        return entry

    def _insert_table(self, rows: List[List[str]], at: str = "end") -> None:
        ncols = max(len(r) for r in rows)
        frame = tk.Frame(self.text, bg=self.border_color, bd=0)
        state = {"ncols": ncols}
        entries: List[List[tk.Entry]] = []

        toolbar = tk.Frame(frame, bg=self.border_color)

        def reflow() -> None:
            toolbar.grid(row=len(entries), column=0, columnspan=state["ncols"], sticky="w", pady=(4, 0))

        def add_row() -> None:
            row_entries = [self._make_cell_entry(frame, "", header=False) for _ in range(state["ncols"])]
            for c, e in enumerate(row_entries):
                e.grid(row=len(entries), column=c, sticky="nsew", padx=1, pady=1)
            entries.append(row_entries)
            reflow()

        def remove_row() -> None:
            if len(entries) <= 2:
                return
            for e in entries.pop():
                e.destroy()
            reflow()

        def add_col() -> None:
            for r, row in enumerate(entries):
                e = self._make_cell_entry(frame, "", header=(r == 0))
                e.grid(row=r, column=state["ncols"], sticky="nsew", padx=1, pady=1)
                row.append(e)
            state["ncols"] += 1
            reflow()

        def remove_col() -> None:
            if state["ncols"] <= 1:
                return
            for row in entries:
                row.pop().destroy()
            state["ncols"] -= 1
            reflow()

        for r, row in enumerate(rows):
            cells = (row + [""] * ncols)[:ncols]
            row_entries = [self._make_cell_entry(frame, cells[c], header=(r == 0)) for c in range(ncols)]
            for c, e in enumerate(row_entries):
                e.grid(row=r, column=c, sticky="nsew", padx=1, pady=1)
            entries.append(row_entries)

        mini = dict(width=26, height=20, fg_color=self.header_bg, hover_color=self.border_color, text_color=self.fg)
        ctk.CTkButton(toolbar, text="+Zeile", command=add_row, **mini).pack(side="left", padx=2, pady=2)
        ctk.CTkButton(toolbar, text="-Zeile", command=remove_row, **mini).pack(side="left", padx=2, pady=2)
        ctk.CTkButton(toolbar, text="+Spalte", command=add_col, **mini).pack(side="left", padx=2, pady=2)
        ctk.CTkButton(toolbar, text="-Spalte", command=remove_col, **mini).pack(side="left", padx=2, pady=2)
        reflow()

        frame.adr_entries = entries
        frame.adr_state = state
        self.text.window_create(at, window=frame)

    def insert_table_at_cursor(self, rows: int = 2, cols: int = 3) -> None:
        line_no, col_no = map(int, self.text.index("insert").split("."))
        line_content = self.text.get(f"{line_no}.0", f"{line_no}.end")
        if line_content.strip() != "" or col_no != 0:
            self.text.insert("insert", "\n")
        header = [f"Spalte {i + 1}" for i in range(cols)]
        body_rows = [header] + [[""] * cols for _ in range(max(rows - 1, 1))]
        self._insert_table(body_rows, at="insert")
        self.text.insert("insert", "\n")

    def _inline_serialize(self, start: str, end: str) -> str:
        out: List[str] = []
        run_chars: List[str] = []
        run_bold: Optional[bool] = None
        run_italic: Optional[bool] = None

        def flush() -> None:
            if not run_chars:
                return
            chunk = "".join(run_chars)
            if run_bold and run_italic:
                out.append(f"***{chunk}***")
            elif run_bold:
                out.append(f"**{chunk}**")
            elif run_italic:
                out.append(f"*{chunk}*")
            else:
                out.append(chunk)

        idx = start
        while self.text.compare(idx, "<", end):
            tags = self.text.tag_names(idx)
            bold = "bold" in tags or "bolditalic" in tags
            italic = "italic" in tags or "bolditalic" in tags
            ch = self.text.get(idx)
            if (bold, italic) != (run_bold, run_italic) and run_chars:
                flush()
                run_chars = []
            run_bold, run_italic = bold, italic
            run_chars.append(ch)
            idx = self.text.index(f"{idx}+1c")
        flush()
        return "".join(out)

    def _table_to_markdown(self, frame: tk.Frame) -> List[str]:
        rows = [[e.get() for e in row] for row in frame.adr_entries]
        ncols = frame.adr_state["ncols"]

        def fmt_row(cells: List[str]) -> str:
            return "| " + " | ".join(cells) + " |"

        lines = [fmt_row(rows[0]), "| " + " | ".join(["---"] * ncols) + " |"]
        lines.extend(fmt_row(row) for row in rows[1:])
        return lines

    def make_read_only(self) -> None:
        for _k, name, _idx in self.text.dump("1.0", "end", window=True):
            frame = self.text.nametowidget(name)
            for r, row in enumerate(getattr(frame, "adr_entries", [])):
                for entry in row:
                    # readonly nutzt sonst den hellgrauen Standard-Hintergrund (im Dunkelmodus unlesbar)
                    bg = self.header_bg if r == 0 else self.cell_bg
                    entry.configure(state="readonly", readonlybackground=bg, fg=self.fg)
            for child in frame.winfo_children():
                if isinstance(child, tk.Frame):
                    child.grid_remove()  # Tabellen-Toolbar (+Zeile/-Zeile ...)
        self.text.configure(state="disabled")

    def to_markdown(self) -> str:
        total_lines = int(self.text.index("end-1c").split(".")[0])
        out_lines = []
        for n in range(1, total_lines + 1):
            line_start, line_end = f"{n}.0", f"{n}.end"
            windows = [v for k, v, _idx in self.text.dump(line_start, line_end, window=True) if k == "window"]
            if windows:
                frame = self.text.nametowidget(windows[0])
                out_lines.extend(self._table_to_markdown(frame))
                continue
            has_content = self.text.compare(line_start, "<", line_end)
            heading = has_content and "heading" in self.text.tag_names(line_start)
            inline_md = self._inline_serialize(line_start, line_end)
            if heading:
                out_lines.append("## " + inline_md)
            elif inline_md.startswith("• "):
                out_lines.append("- " + inline_md[2:])
            else:
                out_lines.append(inline_md)
        return "\n".join(out_lines)


def _drawio_text(value: str) -> str:
    v = re.sub(r"<\s*(br|/div|/p)\s*/?>", "\n", value or "", flags=re.I)
    v = re.sub(r"<[^>]+>", "", v)
    return html.unescape(v).strip()


def _drawio_style(style: str) -> dict:
    out = {}
    for part in (style or "").split(";"):
        if "=" in part:
            k, v = part.split("=", 1)
            out[k] = v
    return out


def parse_drawio(path: Path) -> List[tuple]:
    """Liest eine .drawio-Datei und liefert [(seitenname, mxGraphModel-Element)]. Unterstuetzt komprimierte Seiten."""
    root = ET.fromstring(path.read_text(encoding="utf-8"))
    pages = []
    for d in root.iter("diagram"):
        model = d.find("mxGraphModel")
        if model is None and (d.text or "").strip():
            raw = zlib.decompress(base64.b64decode(d.text.strip()), -15).decode("utf-8")
            model = ET.fromstring(urllib.parse.unquote(raw))
        if model is not None:
            pages.append((d.get("name", "Seite"), model))
    if not pages and root.tag == "mxGraphModel":
        pages.append(("Seite 1", root))
    return pages


def draw_drawio_page(canvas: tk.Canvas, model: ET.Element, zoom: float = 1.0) -> None:
    """Zeichnet eine draw.io-Seite (Boxen, Pfeile, Beschriftungen) auf ein Canvas mit weissem Grund."""
    canvas.delete("all")
    cells = {c.get("id"): c for c in model.iter("mxCell")}
    rects: dict = {}

    def geom(c: ET.Element):
        g = c.find("mxGeometry")
        return g

    def origin(c: ET.Element) -> tuple:
        x = y = 0.0
        parent = cells.get(c.get("parent"))
        while parent is not None and parent.get("vertex") == "1":
            g = geom(parent)
            if g is not None:
                x += float(g.get("x", 0))
                y += float(g.get("y", 0))
            parent = cells.get(parent.get("parent"))
        return x, y

    for cid, c in cells.items():
        g = geom(c)
        if c.get("vertex") == "1" and g is not None and g.get("width"):
            ox, oy = origin(c)
            x, y = float(g.get("x", 0)) + ox, float(g.get("y", 0)) + oy
            rects[cid] = (x, y, float(g.get("width")), float(g.get("height", 0)))

    def clip(rect: tuple, tx: float, ty: float) -> tuple:
        x, y, w, h = rect
        cx, cy = x + w / 2, y + h / 2
        dx, dy = tx - cx, ty - cy
        if dx == 0 and dy == 0:
            return cx, cy
        t = min((w / 2) / abs(dx) if dx else float("inf"), (h / 2) / abs(dy) if dy else float("inf"))
        return cx + dx * t, cy + dy * t

    def z(v: float) -> float:
        return v * zoom

    for cid, c in cells.items():  # Kanten zuerst, damit Boxen darueber liegen
        if c.get("edge") != "1":
            continue
        st = _drawio_style(c.get("style", ""))
        g = geom(c)
        pts = []
        if g is not None:
            arr = g.find("Array")
            if arr is not None:
                pts = [(float(p.get("x", 0)), float(p.get("y", 0))) for p in arr.findall("mxPoint")]
        src, dst = rects.get(c.get("source")), rects.get(c.get("target"))
        sp = tp = None
        if g is not None:
            for p in g.findall("mxPoint"):
                pt = (float(p.get("x", 0)), float(p.get("y", 0)))
                if p.get("as") == "sourcePoint":
                    sp = pt
                elif p.get("as") == "targetPoint":
                    tp = pt
        first = pts[0] if pts else (tp or (dst and (dst[0] + dst[2] / 2, dst[1] + dst[3] / 2)))
        last = pts[-1] if pts else (sp or (src and (src[0] + src[2] / 2, src[1] + src[3] / 2)))
        start = clip(src, *first) if src and first else sp
        end = clip(dst, *last) if dst and last else tp
        if not start or not end:
            continue
        line = [start, *pts, end]
        color = st.get("strokeColor", "#707070")
        canvas.create_line(
            *[coord for p in line for coord in (z(p[0]), z(p[1]))],
            fill=color if color != "none" else "#707070", width=max(1, z(float(st.get("strokeWidth", 1.5)))),
            arrow=tk.LAST if st.get("endArrow", "classic") != "none" else None,
            dash=(6, 4) if st.get("dashed") == "1" else None,
        )
        label = _drawio_text(c.get("value", ""))
        if label:
            mx, my = (line[len(line) // 2] if len(line) % 2 else
                      ((line[len(line) // 2 - 1][0] + line[len(line) // 2][0]) / 2,
                       (line[len(line) // 2 - 1][1] + line[len(line) // 2][1]) / 2))
            size = int(st.get("fontSize", 9))
            t = canvas.create_text(z(mx), z(my), text=label, width=z(170), font=("Helvetica", -max(8, int(z(size) * 1.3))),
                                   fill=st.get("fontColor", "#333333"), justify="center")
            bbox = canvas.bbox(t)
            bg = canvas.create_rectangle(bbox, fill="#ffffff", outline="")
            canvas.tag_lower(bg, t)

    for cid, (x, y, w, h) in rects.items():
        c = cells[cid]
        st = _drawio_style(c.get("style", ""))
        fill = st.get("fillColor", "#ffffff")
        outline = st.get("strokeColor", "#666666")
        label = _drawio_text(c.get("value", ""))
        is_text = "text" in st and "fillColor" not in st
        if not is_text:
            canvas.create_rectangle(
                z(x), z(y), z(x + w), z(y + h),
                fill="" if fill == "none" else fill, outline="" if outline == "none" else outline,
                width=1.5, dash=(6, 4) if st.get("dashed") == "1" else None,
            )
        if label:
            size = int(st.get("fontSize", 11))
            align = st.get("verticalAlign", "middle")
            canvas.create_text(
                z(x + w / 2), z(y + (14 if align == "top" else h / 2)), text=label, width=max(20, z(w - 12)),
                font=("Helvetica", -max(8, int(z(size) * 1.3))), fill=st.get("fontColor", "#1a1a1a"), justify="center",
                anchor="n" if align == "top" else "center",
            )
    bbox = canvas.bbox("all")
    if bbox:
        canvas.configure(scrollregion=(bbox[0] - 20, bbox[1] - 20, bbox[2] + 20, bbox[3] + 20))


def draw_flow_diagram(canvas: tk.Canvas, current: str, dark: bool) -> None:
    """Zeichnet den Lebenszyklus eines ADR (Status, Taetigkeiten, Verzweigungen)."""
    fg = "#e6e6e6" if dark else "#1a1a1a"
    muted = "#9a9a9a" if dark else "#666666"
    act_fill = "#2f2f2f" if dark else "#f4f4f4"
    font_b = ("Helvetica", 11, "bold")
    font_n = ("Helvetica", 10)
    font_s = ("Helvetica", 9)

    def status_box(status: str, cx: int, cy: int, w: int = 190, h: int = 56) -> None:
        active = status == current
        canvas.create_rectangle(
            cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2,
            fill=STATUS_TINT[status][1 if dark else 0], outline="#2f6fb3" if active else STATUS_COLOR[status],
            width=4 if active else 2,
        )
        canvas.create_text(cx, cy, text=f"{STATUS_BADGE[status]} {status}", width=w - 14, font=font_b, fill=fg, justify="center")

    def action_box(text: str, cx: int, cy: int, w: int = 230, h: int = 92) -> None:
        canvas.create_rectangle(cx - w // 2, cy - h // 2, cx + w // 2, cy + h // 2, fill=act_fill, outline=muted, dash=(4, 3))
        canvas.create_text(cx, cy, text=text, width=w - 16, font=font_n, fill=fg, justify="left")

    def arrow(x1: int, y1: int, x2: int, y2: int, dash: bool = False) -> None:
        canvas.create_line(x1, y1, x2, y2, arrow=tk.LAST, fill=fg, width=2, dash=(5, 4) if dash else None)

    canvas.create_text(
        20, 16, anchor="w", font=("Helvetica", 14, "bold"), fill=fg,
        text="ADR- und Implementierungsprozess" + (f"   (aktuell gewähltes ADR: {current})" if current else ""),
    )

    # Reihe A: Entscheidung bis Test (links -> rechts), Reihe B: Reviews bis Abnahme (rechts -> links)
    ya, yb = 100, 420
    xs = {STATUS_PROPOSED: 120, STATUS_ACCEPTED: 400, STATUS_IMPLEMENTED: 680, STATUS_TESTED: 950}
    xb = {STATUS_SECURITY: 950, STATUS_GDPR: 680, STATUS_FULL: 400}
    for st, x in xs.items():
        status_box(st, x, ya)
    for st, x in xb.items():
        status_box(st, x, yb)
    order = [STATUS_PROPOSED, STATUS_ACCEPTED, STATUS_IMPLEMENTED, STATUS_TESTED]
    for l, r in zip(order, order[1:]):
        arrow(xs[l] + 95, ya, xs[r] - 95, ya)
    arrow(xs[STATUS_TESTED], ya + 28, xb[STATUS_SECURITY], yb - 28)
    canvas.create_text(xs[STATUS_TESTED] + 8, (ya + yb) // 2, anchor="w", text="Reviews\nstarten", font=font_s, fill=muted)
    arrow(xb[STATUS_SECURITY] - 95, yb, xb[STATUS_GDPR] + 95, yb)
    arrow(xb[STATUS_GDPR] - 95, yb, xb[STATUS_FULL] + 95, yb)

    mids = [(xs[a] + xs[b]) // 2 for a, b in zip(order, order[1:])]
    for m, text in zip(mids, ["Freigabe durch den Menschen", "Umsetzung fertig", "Tests grün + Abnahme"]):
        canvas.create_text(m, ya - 22, text=text, font=font_s, fill=muted, width=150)
    midb = [(xb[STATUS_SECURITY] + xb[STATUS_GDPR]) // 2, (xb[STATUS_GDPR] + xb[STATUS_FULL]) // 2]
    for m, text in zip(midb, ["Sicherheit bestanden", "Datenschutz bestanden"]):
        canvas.create_text(m, yb - 22, text=text, font=font_s, fill=muted, width=150)

    ay = 235
    action_box("1. ADR schreiben (Kontext, Entscheidung, Konsequenzen, Alternativen) und im Architektur-Board prüfen.\n"
               "Keine Feinplanung ohne Accepted.", mids[0], ay)
    action_box("2. Implementierungsplan unter docs/features erzeugen und im ADR verlinken (Pflicht ab Accepted).\n"
               "3. Implementierung nach diesem Plan.", mids[1] - 20, ay, w=300, h=110)
    action_box("4. Tests nach Plan und Teststrategie, Abnahme durch den Projektinhaber. Implementierungs-Commit im ADR eintragen.",
               mids[2] - 20, ay, w=230, h=110)
    for m, bx in zip(mids, [mids[0], mids[1] - 20, mids[2] - 20]):
        canvas.create_line(m, ya + 4, bx, ay - 58, fill=muted, dash=(2, 3))

    by = 545
    action_box("5. Sicherheitsprüfung (Skill it-security): Eingaben, Auth, Secrets, Abhängigkeiten. Befunde als Nacharbeit.",
               midb[0], by, w=260, h=96)
    action_box("6. DSGVO-/Compliance-Prüfung (Skill it-compliance): Datenflüsse, Rechtsgrundlage, Lizenzen, AI Act.",
               midb[1], by, w=220, h=96)
    action_box("7. Endabnahme: alle Prüfungen bestanden, Nachweise im Plan.", xb[STATUS_FULL] - 70, by, w=160, h=96)
    for m, bx in zip(midb, midb):
        canvas.create_line(m, yb + 4, bx, by - 50, fill=muted, dash=(2, 3))
    canvas.create_line(xb[STATUS_FULL] - 40, yb + 28, xb[STATUS_FULL] - 70, by - 50, fill=muted, dash=(2, 3))

    # Verzweigungen links
    status_box(STATUS_REJECTED, xs[STATUS_PROPOSED], 330)
    arrow(xs[STATUS_PROPOSED], ya + 28, xs[STATUS_PROPOSED], 330 - 28, dash=True)
    canvas.create_text(xs[STATUS_PROPOSED] + 8, 190, anchor="w", text="abgelehnt", font=font_s, fill=muted)
    status_box(STATUS_DEPRECATED, xs[STATUS_PROPOSED], 470)
    canvas.create_text(xs[STATUS_PROPOSED], 525, text="aus jedem Status ab Accepted,\nwenn die Entscheidung abgelöst wird",
                       font=font_s, fill=muted, justify="center")

    canvas.create_text(
        20, 660, anchor="w", font=font_s, fill=muted, justify="left",
        text="Nacharbeit: Implemented → Accepted (Plan ändern); Tested, Security Review und GDPR/Compliance Review → Implemented (Mängel). Rejected → Proposed zur Neubewertung.\n"
             "Statuswechsel nur einen Schritt weiter, keine Sprünge. Accepted und alle Abnahmen setzt der Mensch (nicht der Agent). Ab Tested ist der Implementierungs-Commit Pflicht.\n"
             "Beispiel-ADRs (Nr. ≥ 900000) sind von Plan- und Commitpflicht ausgenommen.",
    )


class ADRManagerApp(ctk.CTk):
    LIST_WRAP = 250  # Umbruchbreite der Listeneintraege (Listenpanel: 340 px minus Scrollbar/Padding)

    def __init__(self) -> None:
        super().__init__()
        ctk.set_appearance_mode("System")
        ctk.set_default_color_theme("blue")

        self.title("ADR-Manager")
        self.geometry("1100x700")
        self.minsize(800, 500)

        self.adr_dir: Path = get_adr_dir()
        self.plan_dir: Path = get_plan_dir()
        self.adrs: List[ADR] = []
        self.current: Optional[ADR] = None
        self.row_buttons: List[ctk.CTkButton] = []

        self._build_layout()
        self.refresh_list()
        self.new_adr()

    # ---------- layout ----------

    def _build_layout(self) -> None:
        self.grid_columnconfigure(0, weight=0, minsize=340)
        self.grid_columnconfigure(1, weight=1)
        self.grid_rowconfigure(0, weight=1)

        self._build_list_panel()
        self._build_form_panel()

    def _build_list_panel(self) -> None:
        panel = ctk.CTkFrame(self)
        panel.grid(row=0, column=0, sticky="nsew", padx=(10, 5), pady=10)
        panel.grid_rowconfigure(3, weight=1)
        panel.grid_columnconfigure(0, weight=1)

        header_row = ctk.CTkFrame(panel, fg_color="transparent")
        header_row.grid(row=0, column=0, sticky="ew", padx=10, pady=(10, 0))
        header_row.grid_columnconfigure(0, weight=1)
        ctk.CTkLabel(header_row, text="ADRs", font=ctk.CTkFont(size=18, weight="bold")).grid(
            row=0, column=0, sticky="w"
        )
        ctk.CTkButton(
            header_row, text="⟳", width=32, fg_color="transparent", hover_color="gray30", command=self.reload_all
        ).grid(row=0, column=1, sticky="e")
        ctk.CTkButton(
            header_row, text="Ablauf", width=60, fg_color="transparent", hover_color="gray30", command=self.open_flow
        ).grid(row=0, column=2, sticky="e")
        ctk.CTkButton(
            header_row, text="⚙", width=32, fg_color="transparent", hover_color="gray30", command=self.open_settings
        ).grid(row=0, column=3, sticky="e")

        filter_row = ctk.CTkFrame(panel, fg_color="transparent")
        filter_row.grid(row=1, column=0, sticky="new", padx=10, pady=8)
        filter_row.grid_columnconfigure(0, weight=1)

        self.search_var = ctk.StringVar()
        self.search_var.trace_add("write", lambda *_: self._render_rows())
        search_entry = ctk.CTkEntry(filter_row, placeholder_text="Suchen…", textvariable=self.search_var)
        search_entry.grid(row=0, column=0, sticky="ew")

        # Statusfilter: Chips mit Anzahl, Mehrfachauswahl; keine Auswahl = alle
        self.status_filters: set = set()
        self.chip_buttons: dict = {}
        chip_frame = ctk.CTkFrame(panel, fg_color="transparent")
        chip_frame.grid(row=2, column=0, sticky="ew", padx=10, pady=(0, 6))
        for i, st in enumerate(STATUS_OPTIONS):
            chip = ctk.CTkButton(
                chip_frame, text=st, width=88, height=24, corner_radius=12, border_width=2,
                font=ctk.CTkFont(size=11), command=lambda s=st: self._toggle_status_filter(s),
            )
            chip.grid(row=i // 3, column=i % 3, padx=2, pady=2, sticky="ew")
            self.chip_buttons[st] = chip
        for c in range(3):
            chip_frame.grid_columnconfigure(c, weight=1)

        self.list_frame = ctk.CTkScrollableFrame(panel, label_text="")
        self.list_frame.grid(row=3, column=0, sticky="nsew", padx=10, pady=(0, 10))
        self.list_frame.grid_columnconfigure(0, weight=1)

        btn_row = ctk.CTkFrame(panel, fg_color="transparent")
        btn_row.grid(row=4, column=0, sticky="ew", padx=10, pady=(0, 10))
        btn_row.grid_columnconfigure((0, 1), weight=1)
        ctk.CTkButton(btn_row, text="+ Neu", command=self.new_adr).grid(row=0, column=0, sticky="ew", padx=(0, 5))
        ctk.CTkButton(btn_row, text="Ordner öffnen", command=self.open_folder, fg_color="gray30", hover_color="gray20").grid(
            row=0, column=1, sticky="ew", padx=(5, 0)
        )

    def _build_form_panel(self) -> None:
        panel = ctk.CTkFrame(self)
        panel.grid(row=0, column=1, sticky="nsew", padx=(5, 10), pady=10)
        panel.grid_columnconfigure(1, weight=1)
        panel.grid_rowconfigure(9, weight=1)

        def label(text: str, row: int) -> None:
            ctk.CTkLabel(panel, text=text, anchor="w").grid(row=row, column=0, sticky="w", padx=(15, 10), pady=8)

        label("ADR-Nummer", 0)
        self.entry_number = ctk.CTkEntry(panel, placeholder_text="0001")
        self.entry_number.grid(row=0, column=1, sticky="ew", padx=(0, 15), pady=8)

        label("Titel", 1)
        self.entry_title = ctk.CTkEntry(panel, placeholder_text="Kurzer, prägnanter Titel")
        self.entry_title.grid(row=1, column=1, sticky="ew", padx=(0, 15), pady=8)

        label("Status", 2)
        status_row = ctk.CTkFrame(panel, fg_color="transparent")
        status_row.grid(row=2, column=1, sticky="ew", padx=(0, 15), pady=8)
        self.status_var = ctk.StringVar(value=STATUS_OPTIONS[0])
        self.option_status = ctk.CTkOptionMenu(status_row, values=STATUS_OPTIONS, variable=self.status_var)
        self.option_status.pack(side="left")

        label("Datum", 3)
        date_row = ctk.CTkFrame(panel, fg_color="transparent")
        date_row.grid(row=3, column=1, sticky="ew", padx=(0, 15), pady=8)
        self.entry_datum = ctk.CTkEntry(date_row, placeholder_text="YYYY-MM-DD", width=150)
        self.entry_datum.pack(side="left")
        ctk.CTkButton(date_row, text="Heute", width=70, command=self._set_today).pack(side="left", padx=(8, 0))

        label("Entscheider", 4)
        self.entry_entscheider = ctk.CTkEntry(panel, placeholder_text="z.B. Arch-Board")
        self.entry_entscheider.grid(row=4, column=1, sticky="ew", padx=(0, 15), pady=8)

        label("Impl.-Plan", 5)
        plan_row = ctk.CTkFrame(panel, fg_color="transparent")
        plan_row.grid(row=5, column=1, sticky="ew", padx=(0, 15), pady=8)
        plan_row.grid_columnconfigure(0, weight=1)
        self.entry_plan = ctk.CTkEntry(plan_row, placeholder_text="Pfad relativ zum ADR-Ordner, z.B. ../features/xyz.md")
        self.entry_plan.grid(row=0, column=0, sticky="ew")
        ctk.CTkButton(plan_row, text="Ansehen", width=75, command=self.view_plan).grid(row=0, column=1, padx=(8, 0))
        ctk.CTkButton(plan_row, text="Wählen…", width=75, command=self.choose_plan).grid(row=0, column=2, padx=(6, 0))
        ctk.CTkButton(plan_row, text="Plan anlegen", width=95, command=self.create_plan).grid(
            row=0, column=3, padx=(6, 0)
        )

        label("C4-Diagramm", 6)
        c4_row = ctk.CTkFrame(panel, fg_color="transparent")
        c4_row.grid(row=6, column=1, sticky="ew", padx=(0, 15), pady=8)
        c4_row.grid_columnconfigure(0, weight=1)
        self.entry_c4 = ctk.CTkEntry(c4_row, placeholder_text="optional: draw.io-Datei, z.B. diagramme/ADR-0016-c4.drawio")
        self.entry_c4.grid(row=0, column=0, sticky="ew")
        ctk.CTkButton(c4_row, text="Ansehen", width=75, command=self.view_c4).grid(row=0, column=1, padx=(8, 0))
        ctk.CTkButton(c4_row, text="Wählen…", width=75, command=self.choose_c4).grid(row=0, column=2, padx=(6, 0))
        ctk.CTkButton(c4_row, text="Entfernen", width=75, fg_color="gray30", hover_color="gray20",
                      command=lambda: self.entry_c4.delete(0, "end")).grid(row=0, column=3, padx=(6, 0))

        label("Commit(s)", 7)
        commit_row = ctk.CTkFrame(panel, fg_color="transparent")
        commit_row.grid(row=7, column=1, sticky="ew", padx=(0, 15), pady=8)
        commit_row.grid_columnconfigure(0, weight=1)
        self.entry_commits = ctk.CTkEntry(
            commit_row, placeholder_text="Hash(es), kommagetrennt; Pflicht ab 'Implementation Tested and Acceptance'"
        )
        self.entry_commits.grid(row=0, column=0, sticky="ew")
        ctk.CTkButton(commit_row, text="Aus Git vorschlagen", width=140, command=self.suggest_commits_ui).grid(
            row=0, column=1, padx=(8, 0)
        )

        label("Inhalt", 8)
        format_toolbar = ctk.CTkFrame(panel, fg_color="transparent")
        format_toolbar.grid(row=8, column=1, sticky="w", padx=(0, 15), pady=(0, 4))
        self.editor = RichTextEditor(panel)

        def do_toggle(fn) -> None:
            fn()
            self.editor.text.focus_set()

        ctk.CTkButton(
            format_toolbar, text="Überschrift", width=90, command=lambda: do_toggle(self.editor.toggle_heading)
        ).pack(side="left")
        self.btn_bold = ctk.CTkButton(
            format_toolbar,
            text="Fett",
            width=50,
            font=ctk.CTkFont(weight="bold"),
            command=lambda: do_toggle(self.editor.toggle_bold),
        )
        self.btn_bold.pack(side="left", padx=(6, 0))
        self.btn_italic = ctk.CTkButton(
            format_toolbar,
            text="Kursiv",
            width=60,
            font=ctk.CTkFont(slant="italic"),
            command=lambda: do_toggle(self.editor.toggle_italic),
        )
        self.btn_italic.pack(side="left", padx=(6, 0))
        ctk.CTkButton(
            format_toolbar, text="• Liste", width=60, command=lambda: do_toggle(self.editor.toggle_bullet)
        ).pack(side="left", padx=(6, 0))
        ctk.CTkButton(
            format_toolbar, text="Tabelle", width=70, command=lambda: do_toggle(self.editor.insert_table_at_cursor)
        ).pack(side="left", padx=(6, 0))

        self._btn_default_fg = self.btn_bold.cget("fg_color")
        self.editor.on_pending_change = self._on_pending_format_change

        self.editor.grid(row=9, column=0, columnspan=2, sticky="nsew", padx=15, pady=(0, 10))

        action_row = ctk.CTkFrame(panel, fg_color="transparent")
        action_row.grid(row=10, column=0, columnspan=2, sticky="ew", padx=15, pady=(0, 10))
        ctk.CTkButton(action_row, text="Speichern", command=self.save_adr).pack(side="left")
        ctk.CTkButton(
            action_row, text="⟳ Neu laden", fg_color="gray30", hover_color="gray20", command=self.reload_document
        ).pack(side="left", padx=(8, 0))
        ctk.CTkButton(
            action_row, text="Löschen", fg_color="#a13a3a", hover_color="#7d2c2c", command=self.delete_adr
        ).pack(side="left", padx=8)

        self.status_label = ctk.CTkLabel(panel, text="", anchor="w", text_color="gray60")
        self.status_label.grid(row=11, column=0, columnspan=2, sticky="ew", padx=15, pady=(0, 10))

    # ---------- data / list handling ----------

    def refresh_list(self) -> None:
        self.adrs = load_all_adrs(self.adr_dir)
        self._render_rows()

    def _render_rows(self) -> None:
        for child in self.list_frame.winfo_children():
            child.destroy()

        query = self.search_var.get().strip().lower()
        self._update_chips()
        for adr in self.adrs:
            if self.status_filters and adr.status not in self.status_filters:
                continue
            haystack = f"{adr.number} {adr.title} {adr.status}".lower()
            if query and query not in haystack:
                continue
            badge = STATUS_BADGE.get(adr.status, "🔵")
            text = f"{badge}  ADR-{adr.number} – {adr.title}\n     {adr.status or '–'} · {adr.datum or '–'}"
            is_current = self.current is not None and self.current.path == adr.path
            tint = STATUS_TINT.get(adr.status, ("#eeeeee", "#2b2b2b"))
            btn = ctk.CTkButton(
                self.list_frame,
                text=text,
                anchor="w",
                fg_color=tint,
                border_width=3 if is_current else 1,
                border_color="#ffffff" if is_current and ctk.get_appearance_mode() == "Dark" else STATUS_COLOR.get(adr.status, "#5a8fd6"),
                text_color=("black", "white"),
                hover_color=("gray70", "gray35"),
                command=lambda a=adr: self.load_adr(a),
            )
            # CTkButton bricht Text nicht um: Titel wuerden sonst den Viewport ueberragen.
            btn._text_label.configure(wraplength=self.LIST_WRAP, justify="left")
            btn.pack(fill="x", pady=2)

    def reload_all(self) -> None:
        """Liest die Liste und das geoeffnete ADR neu von der Platte (z.B. nach Aenderungen durch den Agenten)."""
        path = self.current.path if self.current else None
        self.refresh_list()
        if path and path.is_file():
            self.load_adr(parse_adr(path))
            self._set_status(f"Neu geladen: {path.name} ({len(self.adrs)} ADRs)")
        else:
            self._set_status(f"Liste neu geladen ({len(self.adrs)} ADRs)")

    def reload_document(self) -> None:
        """Liest nur das geoeffnete ADR neu (verwirft ungespeicherte Aenderungen)."""
        path = self.current.path if self.current else None
        if not path or not path.is_file():
            messagebox.showinfo("Nichts zu laden", "Dieses ADR ist noch nicht gespeichert.")
            return
        self.reload_all()

    def _toggle_status_filter(self, status: str) -> None:
        if status in self.status_filters:
            self.status_filters.discard(status)
        else:
            self.status_filters.add(status)
        self._render_rows()

    def _update_chips(self) -> None:
        counts = {st: sum(1 for a in self.adrs if a.status == st) for st in STATUS_OPTIONS}
        for st, chip in self.chip_buttons.items():
            active = st in self.status_filters
            color = STATUS_COLOR[st]
            chip.configure(
                text=f"{STATUS_SHORT.get(st, st)} {counts[st]}",
                fg_color=color if active else "transparent",
                border_color=color,
                hover_color=color,
                text_color="white" if active else ("black", "white"),
            )

    def load_adr(self, adr: ADR) -> None:
        self.current = adr
        self.entry_number.delete(0, "end")
        self.entry_number.insert(0, adr.number)
        self.entry_title.delete(0, "end")
        self.entry_title.insert(0, adr.title)
        self.status_var.set(adr.status if adr.status in STATUS_OPTIONS else STATUS_OPTIONS[0])
        self.entry_datum.delete(0, "end")
        self.entry_datum.insert(0, adr.datum)
        self.entry_entscheider.delete(0, "end")
        self.entry_entscheider.insert(0, adr.entscheider)
        self.entry_plan.delete(0, "end")
        self.entry_plan.insert(0, adr.plan)
        self.entry_c4.delete(0, "end")
        self.entry_c4.insert(0, adr.c4)
        self.entry_commits.delete(0, "end")
        self.entry_commits.insert(0, adr.commits)
        self.editor.load_markdown(adr.body)
        self._set_status(f"Geladen: {adr.path.name if adr.path else ''}")
        self._render_rows()

    def new_adr(self) -> None:
        self.current = None
        self.entry_number.delete(0, "end")
        self.entry_number.insert(0, next_number(self.adrs))
        self.entry_title.delete(0, "end")
        self.status_var.set(STATUS_OPTIONS[0])
        self.entry_datum.delete(0, "end")
        self.entry_datum.insert(0, date.today().isoformat())
        self.entry_entscheider.delete(0, "end")
        self.entry_plan.delete(0, "end")
        self.entry_c4.delete(0, "end")
        self.entry_commits.delete(0, "end")
        self.editor.load_markdown(DEFAULT_BODY_TEMPLATE)
        self._set_status("Neue ADR – noch nicht gespeichert.")
        self._render_rows()

    def _on_pending_format_change(self, pending: set) -> None:
        self.btn_bold.configure(fg_color="#2f6fb3" if "bold" in pending else self._btn_default_fg)
        self.btn_italic.configure(fg_color="#2f6fb3" if "italic" in pending else self._btn_default_fg)

    def _set_today(self) -> None:
        self.entry_datum.delete(0, "end")
        self.entry_datum.insert(0, date.today().isoformat())

    # ---------- persistence ----------

    def save_adr(self) -> None:
        number = self.entry_number.get().strip()
        title = self.entry_title.get().strip()
        if not number or not title:
            messagebox.showwarning("Fehlende Angaben", "ADR-Nummer und Titel werden benötigt.")
            return

        adr = ADR(
            number=number,
            title=title,
            status=self.status_var.get(),
            datum=self.entry_datum.get().strip(),
            entscheider=self.entry_entscheider.get().strip(),
            body=self.editor.to_markdown(),
            plan=self.entry_plan.get().strip(),
            c4=self.entry_c4.get().strip(),
            commits=self.entry_commits.get().strip(),
        )

        err = commit_required_error(adr) or (commit_errors(adr.commits)[0] if commit_errors(adr.commits) else None)
        if err:
            messagebox.showerror("Commit fehlt oder ungültig", err)
            return

        if self.current:
            err = transition_error(self.current.status, adr.status)
            if err:
                messagebox.showerror("Statuswechsel nicht erlaubt", err)
                return

        old_path = self.current.path if self.current else None
        new_path = self.adr_dir / adr.filename

        if new_path != old_path and new_path.exists():
            messagebox.showerror("Datei existiert bereits", f"{new_path.name} ist bereits vorhanden.")
            return

        self.adr_dir.mkdir(parents=True, exist_ok=True)
        created_plan = None
        try:
            created_plan = ensure_plan(adr, self.adr_dir, self.plan_dir)
        except FileExistsError as exc:
            messagebox.showerror("Plan existiert bereits", str(exc))
            return
        new_path.write_text(render_adr(adr), encoding="utf-8")
        if created_plan:
            messagebox.showinfo(
                "Implementierungsplan angelegt",
                f"Fuer Status '{adr.status}' ist ein Plan Pflicht. Geruest angelegt und verlinkt:\n{created_plan}",
            )
        if old_path and old_path.exists() and old_path != new_path:
            old_path.unlink()

        adr.path = new_path
        self.current = adr
        self.refresh_list()
        self.load_adr(adr)
        problems = plan_problems(adr, self.adr_dir)
        self._set_status(f"Gespeichert: {new_path.name}" + (f" – Achtung: {problems[0]}" if problems else ""))

    # ---------- implementierungsplan ----------

    def _current_plan_path(self) -> Optional[Path]:
        raw = self.entry_plan.get().strip()
        return (self.adr_dir / raw).resolve() if raw else None

    def choose_plan(self) -> None:
        chosen = filedialog.askopenfilename(
            initialdir=str(self.plan_dir), filetypes=[("Markdown", "*.md"), ("Alle", "*.*")], title="Implementierungsplan"
        )
        if chosen:
            self.entry_plan.delete(0, "end")
            self.entry_plan.insert(0, plan_ref(Path(chosen), self.adr_dir))

    def create_plan(self) -> None:
        number, title = self.entry_number.get().strip(), self.entry_title.get().strip()
        if not number or not title:
            messagebox.showwarning("Fehlende Angaben", "ADR-Nummer und Titel werden benötigt.")
            return
        if self.entry_plan.get().strip() and not messagebox.askyesno(
            "Plan ersetzen", "Es ist schon ein Plan eingetragen. Trotzdem ein neues Geruest anlegen?"
        ):
            return
        adr = ADR(number, title, self.status_var.get(), "", "", "")
        try:
            target = create_plan_stub(adr, self.adr_dir, self.plan_dir)
        except FileExistsError as exc:
            messagebox.showerror("Datei existiert bereits", str(exc))
            return
        self.entry_plan.delete(0, "end")
        self.entry_plan.insert(0, adr.plan)
        self._set_status(f"Plan angelegt: {target.name} – ADR speichern, um den Link zu übernehmen.")

    def view_plan(self) -> None:
        target = self._current_plan_path()
        if target is None or not target.is_file():
            messagebox.showinfo("Kein Plan", "Es ist kein vorhandener Implementierungsplan verlinkt.")
            return
        dialog = ctk.CTkToplevel(self)
        dialog.title(f"Implementierungsplan – {target.name}")
        dialog.geometry("980x720")
        dialog.transient(self)
        dialog.grid_columnconfigure(0, weight=1)
        dialog.grid_rowconfigure(1, weight=1)
        bar = ctk.CTkFrame(dialog, fg_color="transparent")
        bar.grid(row=0, column=0, sticky="ew", padx=10, pady=(10, 0))
        ctk.CTkLabel(bar, text=str(target), anchor="w", text_color="gray60").pack(side="left")
        ctk.CTkButton(bar, text="Extern öffnen", width=110, command=lambda: self._open_external(target)).pack(
            side="right"
        )
        viewer = RichTextEditor(dialog)
        viewer.grid(row=1, column=0, sticky="nsew", padx=10, pady=10)

        def reload_plan() -> None:
            viewer.text.configure(state="normal")
            if not target.is_file():
                messagebox.showerror("Plan fehlt", str(target), parent=dialog)
                return
            viewer.load_markdown(target.read_text(encoding="utf-8").rstrip("\n"))
            viewer.make_read_only()

        ctk.CTkButton(bar, text="⟳ Aktualisieren", width=120, command=reload_plan).pack(side="right", padx=(0, 8))
        reload_plan()

    # ---------- c4-diagramm (draw.io) ----------

    def suggest_commits_ui(self) -> None:
        number = self.entry_number.get().strip()
        found = suggest_commits(number) if number else []
        if not found:
            messagebox.showinfo("Keine Treffer", f"Kein Commit nennt ADR-{number} in der Nachricht.")
            return
        if messagebox.askyesno(
            "Commits übernehmen",
            f"{len(found)} Commit(s) nennen ADR-{number} in der Nachricht:\n\n{', '.join(found)}\n\n"
            "Übernehmen? (Bitte prüfen: nicht jeder Treffer ist Teil der Implementierung.)",
        ):
            self.entry_commits.delete(0, "end")
            self.entry_commits.insert(0, ", ".join(found))

    def choose_c4(self) -> None:
        chosen = filedialog.askopenfilename(
            initialdir=str(self.adr_dir / "diagramme") if (self.adr_dir / "diagramme").is_dir() else str(self.adr_dir),
            filetypes=[("draw.io", "*.drawio *.xml"), ("Alle", "*.*")],
            title="C4-Diagramm (draw.io)",
        )
        if chosen:
            self.entry_c4.delete(0, "end")
            self.entry_c4.insert(0, plan_ref(Path(chosen), self.adr_dir))

    def view_c4(self) -> None:
        raw = self.entry_c4.get().strip()
        target = (self.adr_dir / raw).resolve() if raw else None
        if target is None or not target.is_file():
            messagebox.showinfo("Kein Diagramm", "Es ist kein vorhandenes C4-Diagramm verlinkt.")
            return
        try:
            pages = parse_drawio(target)
        except (ET.ParseError, zlib.error, ValueError, OSError) as exc:
            messagebox.showerror("Diagramm nicht lesbar", f"{target.name}: {exc}")
            return
        if not pages:
            messagebox.showinfo("Leer", "Die Datei enthält keine Diagrammseite.")
            return

        dialog = ctk.CTkToplevel(self)
        dialog.title(f"C4-Diagramm – {target.name}")
        dialog.geometry("1180x760")
        dialog.transient(self)
        dialog.grid_columnconfigure(0, weight=1)
        dialog.grid_rowconfigure(1, weight=1)
        state = {"zoom": 1.0, "page": 0}

        canvas_frame = tk.Frame(dialog)
        canvas_frame.grid(row=1, column=0, sticky="nsew", padx=10, pady=10)
        canvas_frame.grid_rowconfigure(0, weight=1)
        canvas_frame.grid_columnconfigure(0, weight=1)
        canvas = tk.Canvas(canvas_frame, bg="#ffffff", highlightthickness=0)
        vbar = tk.Scrollbar(canvas_frame, orient="vertical", command=canvas.yview)
        hbar = tk.Scrollbar(canvas_frame, orient="horizontal", command=canvas.xview)
        canvas.configure(yscrollcommand=vbar.set, xscrollcommand=hbar.set)
        canvas.grid(row=0, column=0, sticky="nsew")
        vbar.grid(row=0, column=1, sticky="ns")
        hbar.grid(row=1, column=0, sticky="ew")
        canvas.bind("<MouseWheel>", lambda e: canvas.yview_scroll(-1 if e.delta > 0 else 1, "units"))
        canvas.bind("<Button-4>", lambda _e: canvas.yview_scroll(-1, "units"))
        canvas.bind("<Button-5>", lambda _e: canvas.yview_scroll(1, "units"))
        canvas.bind("<ButtonPress-1>", lambda e: canvas.scan_mark(e.x, e.y))
        canvas.bind("<B1-Motion>", lambda e: canvas.scan_dragto(e.x, e.y, gain=1))

        zoom_label = ctk.CTkLabel(dialog, text="100 %", width=50)

        def redraw() -> None:
            draw_drawio_page(canvas, pages[state["page"]][1], state["zoom"])
            zoom_label.configure(text=f"{round(state['zoom'] * 100)} %")

        def set_zoom(delta: float) -> None:
            state["zoom"] = min(3.0, max(0.3, state["zoom"] + delta))
            redraw()

        def select_page(name: str) -> None:
            state["page"] = [n for n, _ in pages].index(name)
            redraw()

        bar = ctk.CTkFrame(dialog, fg_color="transparent")
        bar.grid(row=0, column=0, sticky="ew", padx=10, pady=(10, 0))
        if len(pages) > 1:
            ctk.CTkOptionMenu(bar, values=[n for n, _ in pages], command=select_page, width=260).pack(side="left")
        ctk.CTkButton(bar, text="−", width=32, command=lambda: set_zoom(-0.1)).pack(side="left", padx=(12, 0))
        zoom_label.pack(in_=bar, side="left")
        ctk.CTkButton(bar, text="+", width=32, command=lambda: set_zoom(0.1)).pack(side="left")
        def reload_c4() -> None:
            try:
                new_pages = parse_drawio(target)
            except (ET.ParseError, zlib.error, ValueError, OSError) as exc:
                messagebox.showerror("Diagramm nicht lesbar", f"{target.name}: {exc}", parent=dialog)
                return
            if new_pages:
                pages[:] = new_pages
                state["page"] = min(state["page"], len(pages) - 1)
                redraw()

        ctk.CTkButton(bar, text="⟳ Aktualisieren", width=120, command=reload_c4).pack(side="right", padx=(0, 8))
        ctk.CTkButton(bar, text="Extern öffnen (draw.io)", width=160, command=lambda: self._open_external(target)).pack(
            side="right"
        )
        ctk.CTkLabel(bar, text=str(target), text_color="gray60").pack(side="right", padx=10)
        redraw()

    def _open_external(self, path: Path) -> None:
        try:
            if sys.platform.startswith("linux"):
                subprocess.Popen(["xdg-open", str(path)])
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(path)])
            elif sys.platform.startswith("win"):
                os.startfile(str(path))  # type: ignore[attr-defined]
        except OSError:
            messagebox.showinfo("Pfad", str(path))

    # ---------- ablaufdiagramm ----------

    def open_flow(self) -> None:
        dialog = ctk.CTkToplevel(self)
        dialog.title("Ablauf ADR und Implementierung")
        dialog.geometry("1100x740")
        dialog.transient(self)
        dialog.grid_columnconfigure(0, weight=1)
        dialog.grid_rowconfigure(0, weight=1)
        current = self.status_var.get() if self.current else ""
        canvas = tk.Canvas(dialog, highlightthickness=0, bg="#242424" if ctk.get_appearance_mode() == "Dark" else "#ffffff")
        canvas.grid(row=0, column=0, sticky="nsew")
        draw_flow_diagram(canvas, current, ctk.get_appearance_mode() == "Dark")

    def delete_adr(self) -> None:
        if not self.current or not self.current.path:
            messagebox.showinfo("Nichts zu löschen", "Diese ADR wurde noch nicht gespeichert.")
            return
        if not messagebox.askyesno("ADR löschen", f"{self.current.path.name} wirklich löschen?"):
            return
        self.current.path.unlink(missing_ok=True)
        self.refresh_list()
        self.new_adr()

    def open_folder(self) -> None:
        self.adr_dir.mkdir(parents=True, exist_ok=True)
        try:
            if sys.platform.startswith("linux"):
                subprocess.Popen(["xdg-open", str(self.adr_dir)])
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(self.adr_dir)])
            elif sys.platform.startswith("win"):
                subprocess.Popen(["explorer", str(self.adr_dir)])
        except OSError:
            messagebox.showinfo("Pfad", str(self.adr_dir))

    def open_settings(self) -> None:
        dialog = ctk.CTkToplevel(self)
        dialog.title("Einstellungen")
        dialog.geometry("560x260")
        dialog.transient(self)
        dialog.grab_set()
        dialog.grid_columnconfigure(0, weight=1)

        ctk.CTkLabel(dialog, text="ADR-Ordner", anchor="w").grid(
            row=0, column=0, columnspan=2, sticky="w", padx=15, pady=(15, 5)
        )

        path_row = ctk.CTkFrame(dialog, fg_color="transparent")
        path_row.grid(row=1, column=0, columnspan=2, sticky="ew", padx=15)
        path_row.grid_columnconfigure(0, weight=1)
        path_var = ctk.StringVar(value=str(self.adr_dir))
        path_entry = ctk.CTkEntry(path_row, textvariable=path_var)
        path_entry.grid(row=0, column=0, sticky="ew")

        def browse() -> None:
            chosen = filedialog.askdirectory(initialdir=str(self.adr_dir), parent=dialog)
            if chosen:
                path_var.set(chosen)

        ctk.CTkButton(path_row, text="Durchsuchen…", width=110, command=browse).grid(row=0, column=1, padx=(8, 0))

        ctk.CTkLabel(dialog, text="Ordner der Implementierungspläne", anchor="w").grid(
            row=4, column=0, columnspan=2, sticky="w", padx=15, pady=(10, 5)
        )
        plan_row = ctk.CTkFrame(dialog, fg_color="transparent")
        plan_row.grid(row=5, column=0, columnspan=2, sticky="ew", padx=15)
        plan_row.grid_columnconfigure(0, weight=1)
        plan_var = ctk.StringVar(value=str(self.plan_dir))
        ctk.CTkEntry(plan_row, textvariable=plan_var).grid(row=0, column=0, sticky="ew")

        def browse_plan() -> None:
            chosen = filedialog.askdirectory(initialdir=str(self.plan_dir), parent=dialog)
            if chosen:
                plan_var.set(chosen)

        ctk.CTkButton(plan_row, text="Durchsuchen…", width=110, command=browse_plan).grid(row=0, column=1, padx=(8, 0))

        hint = ctk.CTkLabel(
            dialog,
            text="Ordner mit den ADR-*.md-Dateien. Wird dauerhaft gespeichert.",
            anchor="w",
            text_color="gray60",
        )
        hint.grid(row=2, column=0, columnspan=2, sticky="w", padx=15, pady=(6, 0))

        def apply_and_close() -> None:
            new_dir = Path(path_var.get().strip()).expanduser()
            if not new_dir:
                messagebox.showwarning("Ungültiger Pfad", "Bitte einen Ordner angeben.", parent=dialog)
                return
            set_adr_dir(new_dir)
            new_plan_dir = Path(plan_var.get().strip()).expanduser()
            set_plan_dir(new_plan_dir)
            self.plan_dir = new_plan_dir
            self.adr_dir = new_dir
            self.refresh_list()
            self.new_adr()
            self._set_status(f"ADR-Ordner geändert: {new_dir}")
            dialog.destroy()

        button_row = ctk.CTkFrame(dialog, fg_color="transparent")
        button_row.grid(row=6, column=0, columnspan=2, sticky="e", padx=15, pady=15)
        ctk.CTkButton(button_row, text="Abbrechen", fg_color="gray30", hover_color="gray20", command=dialog.destroy).pack(
            side="left", padx=(0, 8)
        )
        ctk.CTkButton(button_row, text="Speichern", command=apply_and_close).pack(side="left")

    def _set_status(self, text: str) -> None:
        self.status_label.configure(text=text)


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="ADR-Manager – Verwaltung von Architecture Decision Records")
    parser.add_argument(
        "--new",
        metavar="TITEL",
        help="Legt sofort eine neue ADR mit diesem Titel an (ohne GUI) und beendet sich danach.",
    )
    parser.add_argument(
        "--status",
        default=STATUS_OPTIONS[0],
        choices=STATUS_OPTIONS,
        help=f"Status der neuen ADR (Standard: {STATUS_OPTIONS[0]}). Nur zusammen mit --new.",
    )
    parser.add_argument("--entscheider", default="", help="Entscheider der neuen ADR. Nur zusammen mit --new.")
    parser.add_argument(
        "--datum",
        default=None,
        help="Datum der neuen ADR im Format YYYY-MM-DD (Standard: heute). Nur zusammen mit --new.",
    )
    parser.add_argument(
        "--set-status",
        nargs=2,
        metavar=("NUMMER", "STATUS"),
        help="Setzt den Status eines ADR nach den Lebenszyklus-Regeln und legt bei Bedarf den Plan an.",
    )
    parser.add_argument(
        "--commit",
        metavar="HASHES",
        help="Implementierungs-Commit(s), kommagetrennt. Zusammen mit --set-status; Pflicht ab 'Implementation Tested and Acceptance'.",
    )
    parser.add_argument(
        "--suggest-commits",
        metavar="NUMMER",
        help="Listet Commits, deren Nachricht 'ADR-<nummer>' nennt (Vorschlag, bitte pruefen).",
    )
    parser.add_argument(
        "--create-plan",
        metavar="NUMMER",
        help="Legt ein Plan-Geruest in docs/features an und verlinkt es im ADR.",
    )
    parser.add_argument(
        "--link-c4",
        nargs=2,
        metavar=("NUMMER", "DATEI"),
        help="Verlinkt ein C4-Diagramm (draw.io-Datei, Pfad relativ zum ADR-Ordner oder absolut) im ADR.",
    )
    parser.add_argument(
        "--check",
        action="store_true",
        help="Prueft, ob jedes ADR ab Accepted einen vorhandenen, verlinkten Plan hat (Exit-Code 1 bei Verstoessen).",
    )
    return parser


def run_cli_actions(args: argparse.Namespace) -> bool:
    """Fuehrt die headless-Aktionen aus. True, wenn eine ausgefuehrt wurde."""
    adr_dir, plan_dir = get_adr_dir(), get_plan_dir()

    if args.check:
        problems = [p for a in load_all_adrs(adr_dir) for p in plan_problems(a, adr_dir)]
        for p in problems:
            print(p)
        for a in load_all_adrs(adr_dir):
            err = commit_required_error(a)
            if err:
                print(f"Hinweis ADR-{a.number}: {err}")
        print("OK: alle ADRs erfuellen die Planpflicht." if not problems else f"{len(problems)} Verstoss(e).")
        sys.exit(1 if problems else 0)

    if args.suggest_commits:
        n = args.suggest_commits.zfill(4)
        for h in suggest_commits(n):
            r = subprocess.run(["git", "-C", str(REPO_ROOT), "log", "-1", "--format=%h %ad %s", "--date=short", h],
                               capture_output=True, text=True)
            print(r.stdout.strip())
        return True

    if args.link_c4:
        adr = find_adr(load_all_adrs(adr_dir), args.link_c4[0])
        if adr is None or adr.path is None:
            print(f"Fehler: ADR {args.link_c4[0]} nicht gefunden.", file=sys.stderr)
            sys.exit(1)
        target = (adr_dir / args.link_c4[1]).resolve()
        if not target.is_file():
            print(f"Fehler: Datei nicht gefunden: {target}", file=sys.stderr)
            sys.exit(1)
        adr.c4 = plan_ref(target, adr_dir)
        adr.path.write_text(render_adr(adr), encoding="utf-8")
        print(f"ADR-{adr.number}: C4-Diagramm {adr.c4}")
        return True

    if args.set_status or args.create_plan:
        number = args.set_status[0] if args.set_status else args.create_plan
        adr = find_adr(load_all_adrs(adr_dir), number)
        if adr is None or adr.path is None:
            print(f"Fehler: ADR {number} nicht gefunden.", file=sys.stderr)
            sys.exit(1)
        if args.set_status:
            new_status = args.set_status[1]
            if new_status not in STATUS_OPTIONS:
                print(f"Fehler: unbekannter Status. Erlaubt: {', '.join(STATUS_OPTIONS)}", file=sys.stderr)
                sys.exit(1)
            err = transition_error(adr.status, new_status)
            if err:
                print(f"Fehler: {err}", file=sys.stderr)
                sys.exit(1)
            adr.status = new_status
            if args.commit:
                adr.commits = args.commit
            err = commit_required_error(adr)
            if err:
                print(f"Fehler: {err} Mit --commit <hash> angeben (Vorschlaege: --suggest-commits {adr.number}).", file=sys.stderr)
                sys.exit(1)
        try:
            if args.create_plan and not adr.plan:
                create_plan_stub(adr, adr_dir, plan_dir)
            else:
                ensure_plan(adr, adr_dir, plan_dir)
        except FileExistsError as exc:
            print(f"Fehler: Datei existiert bereits: {exc}", file=sys.stderr)
            sys.exit(1)
        adr.path.write_text(render_adr(adr), encoding="utf-8")
        print(f"ADR-{adr.number}: Status '{adr.status}', Plan: {adr.plan or '–'}")
        return True
    return False


def main() -> None:
    args = build_arg_parser().parse_args()

    if run_cli_actions(args):
        return

    if args.new:
        adr_dir = get_adr_dir()
        try:
            path = create_new_adr_headless(
                title=args.new,
                status=args.status,
                datum=args.datum or date.today().isoformat(),
                entscheider=args.entscheider,
                adr_dir=adr_dir,
            )
        except FileExistsError as exc:
            print(f"Fehler: Datei existiert bereits: {exc}", file=sys.stderr)
            sys.exit(1)
        print(f"Neue ADR erstellt: {path}")
        return

    app = ADRManagerApp()
    app.mainloop()


if __name__ == "__main__":
    main()
