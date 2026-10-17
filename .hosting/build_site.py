"""Build _site/ from .hosting/publish-allowlist.txt (stdlib only).

    py -3 .hosting/build_site.py [--out _site]

Fails (exit 1, nothing written) if an allowlisted file is missing or disallowed, or if a listed page
references a local file that is not on the allowlist, or a remote one (demos must work offline).
"""
import argparse
import html
import posixpath
import re
import shutil
import sys
from html.parser import HTMLParser
from pathlib import Path

ALLOWLIST = ".hosting/publish-allowlist.txt"
PREFIX = "js-demos/"  # published layout drops this prefix: js-demos/x/index.html -> _site/x/index.html
EXTS = {".html", ".css", ".js", ".mjs", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp", ".ico", ".woff2"}
BAD_SEGMENTS = {"tests", "docs", "node_modules"}
SCANNED = {".html", ".css", ".js", ".mjs", ".svg"}
INERT = ("data:", "blob:", "mailto:", "javascript:", "about:")
REFS = [re.compile(p, re.I | re.M) for p in (
    r"""\burl\(\s*["']?([^)"']+?)["']?\s*\)""",
    r"""@import\s+["']([^"']+)["']""",
    r"""\bnew\s+(?:Shared)?Worker\(\s*["']([^"']+)["']""",
    r"""\bimportScripts\(\s*["']([^"']+)["']""",
)]
JS_TOKENS = re.compile(r"""(?P<ignored>//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')
    |(?P<template>`(?:\\.|[^`\\])*`)
    |(?P<construct>\b(?:fetch|import|serviceWorker\s*\.\s*register)\b)""", re.S | re.X)
JS_LITERAL = re.compile(r"""\s*(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)'|`([^`\\\r\n]*)`)\s*(?=[,)])""")
JS_IMPORT = re.compile(r"""[ \t]+(?:[^;\r\n]*?\bfrom[ \t]+)?(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)')[ \t]*(?=;|\r?$)""", re.M)


class AttributeRefs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.refs = []
        self.scripts = []
        self.in_script = False

    def handle_starttag(self, tag, attrs):
        if tag == "script":
            self.in_script = True
        for name, value in attrs:
            if not value:
                continue
            if name in {"src", "href", "poster"}:
                self.refs.append(value)
                if value.lstrip().lower().startswith("javascript:"):
                    self.scripts.append(value.split(":", 1)[1])
            elif name in {"srcset", "imagesrcset"}:
                while value.strip(" \t\r\n\f,"):
                    parts = value.lstrip(" \t\r\n\f,").split(None, 1)
                    url = parts[0]
                    rest = parts[1] if len(parts) == 2 else ""
                    self.refs.append(url.rstrip(","))
                    # Commas inside a URL (notably data:) are not candidate separators.
                    value = rest if url.endswith(",") else rest.partition(",")[2]
            elif name.startswith("on"):
                self.scripts.append(value)
        if tag == "meta" and (dict(attrs).get("http-equiv") or "").lower() == "refresh":
            content = dict(attrs).get("content") or ""
            refresh = re.split(r"[;,]\s*(?:url\s*=\s*)?", content, maxsplit=1, flags=re.I)
            if len(refresh) == 2:
                self.refs.append(refresh[1].strip().strip("\"'"))

    def handle_endtag(self, tag):
        if tag == "script":
            self.in_script = False

    def handle_data(self, data):
        if self.in_script:
            self.scripts.append(data)


def js_refs(text):
    for token in JS_TOKENS.finditer(text):
        if token.lastgroup == "ignored":
            continue
        if token.lastgroup == "template":
            # Conservatively scan interpolation code rather than treating it as inert text.
            yield from js_refs(token.group().partition("${")[2])
            continue
        construct = token.group("construct")
        tail = text[token.end():]
        if construct == "import" and re.match(r"\s*\.", tail):
            continue  # import.meta is not a request.
        call = re.match(r"\s*\(", tail)
        literal = (JS_LITERAL.match(text, token.end() + call.end()) if call else
                   JS_IMPORT.match(text, token.end()) if construct == "import" else None)
        hint = "; multi-line imports are unsupported" if construct == "import" else ""
        ref = next(g for g in literal.groups() if g is not None) if literal else None
        if ref is None or "${" in ref or (construct == "import" and "\n" in text[token.start():literal.end()]):
            raise ValueError(f"cannot statically resolve {construct} reference: use a single literal URL{hint}")
        yield ref


def read_allowlist(root):
    lines = (root / ALLOWLIST).read_text(encoding="utf-8").splitlines()
    return sorted({l.strip() for l in lines if l.strip() and not l.lstrip().startswith("#")})


def check_entry(root, rel):
    parts = rel.split("/")
    if "\\" in rel or posixpath.normpath(rel) != rel or rel.startswith("/") or not rel.startswith(PREFIX):
        return f"{rel}: must be a normalised repo-relative path under {PREFIX}"
    if Path(rel).suffix.lower() not in EXTS or BAD_SEGMENTS & set(parts) or any(p.startswith(".") for p in parts):
        return f"{rel}: not a publishable runtime asset type or location"
    path = root / rel
    if any(q.is_symlink() for q in [path, *path.parents] if q != root and root in q.parents):
        return f"{rel}: symlinks are not published"
    if not path.is_file():
        return f"{rel}: allowlisted file is missing"
    if root.resolve() / PREFIX.rstrip("/") not in path.resolve().parents:
        return f"{rel}: resolves outside {PREFIX}"
    return None


def local_refs(rel, text):
    """Yield (ref, kind, target), or raise ValueError for unresolved JavaScript references."""
    refs = [html.unescape(m.group(1)) for rx in REFS for m in rx.finditer(text)]
    scripts = [text] if Path(rel).suffix.lower() in {".js", ".mjs"} else []
    if Path(rel).suffix.lower() in {".html", ".svg"}:
        parser = AttributeRefs()
        parser.feed(text)
        parser.close()
        refs.extend(parser.refs)
        scripts = parser.scripts
    for script in scripts:
        refs.extend(js_refs(script))
    for ref in refs:
        ref = ref.strip()
        if not ref or ref.startswith("#") or ref.lower().startswith(INERT):
            continue
        if ref.startswith("//") or re.match(r"[a-z][a-z0-9+.-]*:", ref, re.I):
            yield ref, "remote", None
        elif ref.startswith("/"):
            yield ref, "root", None
        else:
            clean = re.split(r"[?#]", ref)[0]
            yield ref, "local", posixpath.normpath(posixpath.join(posixpath.dirname(rel), clean))


def build(root, out):
    root, out = Path(root), Path(out)
    if out.name != "_site":
        return [f"refusing to write to {out}: output directory must be named _site"]
    listed = read_allowlist(root)
    errors = [e for e in (check_entry(root, rel) for rel in listed) if e]
    if errors:
        return errors
    allowed = set(listed)
    for rel in listed:
        if Path(rel).suffix.lower() not in SCANNED:
            continue
        text = (root / rel).read_text(encoding="utf-8", errors="replace")
        try:
            refs = list(local_refs(rel, text))
        except ValueError as e:
            errors.append(f"{rel}: {e}")
            continue
        for ref, kind, target in refs:
            if kind == "remote":
                errors.append(f"{rel}: remote reference {ref!r} (demos must work offline)")
            elif kind == "root":
                errors.append(f"{rel}: root-absolute reference {ref!r} breaks under a project-site path")
            elif target not in allowed:
                errors.append(f"{rel}: references {target!r}, which is not on the allowlist")
    if errors:
        return errors
    if out.exists():
        shutil.rmtree(out)
    links = []
    for rel in listed:
        dest = out / rel[len(PREFIX):]
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(root / rel, dest)
        if rel.count("/") == 2 and rel.endswith("/index.html"):
            m = re.search(r"<title>(.*?)</title>", (root / rel).read_text(encoding="utf-8"), re.I | re.S)
            title = html.unescape(m.group(1)).strip() if m else rel.split("/")[1]
            links.append((rel[len(PREFIX):], title))
    items = "\n".join(f'    <li><a href="{html.escape(h)}">{html.escape(t)}</a></li>' for h, t in links)
    (out / "index.html").write_text(INDEX.format(items=items), encoding="utf-8")
    return []


INDEX = """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>SwarmingLilMen browser demos</title>
  <style>
    :root {{ color-scheme: light dark; }}
    body {{ font: 16px/1.5 system-ui, sans-serif; max-width: 40rem; margin: 2rem auto; padding: 0 1rem; }}
  </style>
</head>
<body>
  <h1>SwarmingLilMen browser demos</h1>
  <p>Standalone browser demonstrations of swarm and emergence models. They run entirely in your
  browser, offline, and are separate from the native C#/.NET simulation engine in the
  <a href="https://github.com/Chris0Jeky/SwarmingLilMen">source repository</a>, which is not hosted here.
  These pages make no performance claims.</p>
  <ul>
{items}
  </ul>
</body>
</html>
"""


def main():
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("--out", default="_site")
    args = ap.parse_args()
    root = Path(__file__).resolve().parents[1]
    errors = build(root, root / args.out)
    for e in errors:
        print("ERROR:", e, file=sys.stderr)
    if not errors:
        print(f"built {args.out}/ from {len(read_allowlist(root))} allowlisted files")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main())
