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
URL_ATTRS = {"src", "href", "poster", "data-src", "data-href", "data-poster", "data", "action", "formaction",
             "background", "cite", "longdesc", "usemap", "profile", "manifest", "codebase", "classid", "code",
             "lowsrc", "dynsrc", "icon"}
REFS = [re.compile(p, re.I | re.M) for p in (
    r"""\b(?:src|href|poster)\s*=\s*["']([^"']+)["']""",
    r"""\burl\(\s*["']?([^)"']+?)["']?\s*\)""",
    r"""@import\s+["']([^"']+)["']""",
    r"""\bnew\s+(?:Shared)?Worker\(\s*["']([^"']+)["']""",
    r"""\bimportScripts\(\s*["']([^"']+)["']""",
)]
JS_IDENT = re.compile(r"(?:[^\W\d]|\$)(?:\w|\$)*")
# Consume only a numeric token: swallowing arbitrary words/dots hides member access such as
# 0..constructor.constructor(...). Numeric separators, radix prefixes and exponents stay inert.
JS_NUMBER = re.compile(r"(?:0[xX][0-9a-fA-F_]+n?|0[bB][01_]+n?|0[oO][0-7_]+n?|"
                       r"\d[\d_]*(?:\.[\d_]*)?(?:[eE][+-]?[\d_]+)?n?)")
# Identifiers that take one literal URL argument (checked against the allowlist) and the APIs that are never allowed.
JS_CALLS = {"fetch", "import", "importScripts", "Worker", "SharedWorker", "register"}
JS_DENIED = {"WebSocket", "WebSocketStream", "WebTransport", "EventSource", "sendBeacon", "XMLHttpRequest"}
JS_CODE = {"eval", "Function"}
JS_TIMERS = {"setTimeout", "setInterval"}
# Reject computed access AND aliases of globals; a direct dotted access is still lexed normally.
JS_GLOBALS = {"window", "globalThis", "self", "navigator", "document", "defaultView"}
# These names also occur as ordinary coordinates, instance identity, etc.
# Computed lookup is rejected; bare `this` is further restricted below.
JS_COMPUTED_GLOBALS = {"this", "top", "parent", "frames"}
JS_LINE_END = re.compile(r"[\r\n\u2028\u2029]")
JS_PARAMS = r"\(\s*(?:[\w$]+\s*(?:,\s*[\w$]+\s*)*)?\)"
JS_ARROW = re.compile(r"(?:async\s+)?(?:[\w$]+|" + JS_PARAMS + r")\s*=>")
JS_FUNCTION = re.compile(r"function\b\s*(?:[\w$]+\s*)?" + JS_PARAMS + r"\s*\{")
# After one of these a `/` starts a regular expression; after any other identifier, number, `)` or `]` it divides.
JS_REGEX_AFTER = {"return", "typeof", "instanceof", "in", "of", "new", "delete", "void", "throw", "case", "do",
                  "else", "yield", "await", "default"}
# A `/` after `}` or `)` may divide or start a regex; if the regex reading would swallow any of these, reject.
JS_SLASH_RISK = re.compile(r"""["'`]|\b(?:""" + "|".join(sorted(
    JS_CALLS | JS_DENIED | JS_CODE | JS_TIMERS | JS_GLOBALS | JS_COMPUTED_GLOBALS |
    {"serviceWorker", "export", "constructor"})) + r")\b")
JS_EXPORT = re.compile(r"""\s*(?:\*(?:\s*as\s+[\w$]+)?|\{[^}'"`/]*\})""")
JS_EXPORT_FROM = re.compile(r"""\s*from\s*(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)')""")
JS_SCRIPT_TYPES = ("javascript", "ecmascript", "jscript", "livescript")
JS_LITERAL = re.compile(r"""\s*(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)'|`([^`\\\r\n]*)`)\s*(?=[,)])""")
JS_PROPERTY = re.compile(r"""(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)')""")
JS_IMPORT = re.compile(r"""[ \t]+(?:[^;\r\n]*?\bfrom[ \t]+)?(?:"([^"\\\r\n]*)"|'([^'\\\r\n]*)')[ \t]*(?=;|\r?$)""", re.M)


class AttributeRefs(HTMLParser):
    def __init__(self):
        super().__init__()
        self.refs = []
        self.scripts = []
        self.styles = []
        self.in_style = False
        self.block = None  # raw text of ANY <script>; HTML escape states also apply to data blocks
        self.executable = False

    def handle_starttag(self, tag, attrs):
        if tag == "style":
            self.in_style = True
            self.styles.append("")
        if tag == "script":
            # Browsers read the first duplicate attribute; being unsure which one wins, any executable one counts.
            types = [value for name, value in attrs if name == "type"] or [None]
            self.executable = any([script_is_executable(value) for value in types])
            self.block = []
        for name, value in attrs:
            name = name.rsplit(":", 1)[-1]
            if name == "srcdoc":
                raise ValueError("srcdoc embedded HTML is not supported")
            if not value:
                continue
            if name in URL_ATTRS:
                if ((tag == "script" and name in {"src", "href"}) or
                    (tag in {"iframe", "object", "embed"} and name in {"src", "data"})):
                    if value.strip().lower().startswith(INERT):
                        raise ValueError(f"<{tag} {name}> inline or opaque resource URLs are not supported")
                self.refs.append(value)
                if value.lstrip().lower().startswith("javascript:"):
                    self.scripts.append(value.split(":", 1)[1])
            elif name in {"ping", "archive", "attributionsrc"}:
                self.refs.extend(value.split())
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
            elif name == "style":
                self.styles.append(value)  # HTMLParser has already decoded character references.
        # Browsers keep the first occurrence, including an empty/valueless attribute.
        first_attrs = {}
        for name, value in attrs:
            first_attrs.setdefault(name, value)
        if tag == "meta" and (first_attrs.get("http-equiv") or "").lower() == "refresh":
            content = first_attrs.get("content") or ""
            refresh = re.split(r"[;,]\s*(?:url\s*=\s*)?", content, maxsplit=1, flags=re.I)
            if len(refresh) == 2:
                self.refs.append(refresh[1].strip().strip("\"'"))

    def handle_endtag(self, tag):
        if tag == "style":
            self.in_style = False
        if tag == "script" and self.block is not None:
            body = "".join(self.block)
            # HTMLParser lacks the browser's escaped/double-escaped script states.
            # Reject even markers inside JS strings or non-JS blocks: HTML sees them too.
            if "<!--" in body or "-->" in body:
                raise ValueError("HTML comment markers in script data are not supported")
            if self.executable:
                self.scripts.append(body)  # the parser may deliver one script in several chunks
            self.block = None

    def handle_data(self, data):
        if self.block is not None:
            self.block.append(data)
        elif self.in_style:
            self.styles[-1] += data

    def close(self):
        super().close()
        self.handle_endtag("script")  # a script left open at end of input is still code


def script_is_executable(type_attr):
    """False only for a <script type> that is certainly a data block; anything unsure is scanned as code."""
    kind = (type_attr or "").strip().lower()
    if kind in {"importmap", "speculationrules"}:
        raise ValueError(f"<script type={kind}> is not supported: it can make the browser load other resources")
    return kind in {"", "module"} or any(word in kind for word in JS_SCRIPT_TYPES)


def _quoted(text, i):
    """Index after the string literal opening at text[i]; an unescaped newline or end of input is an error."""
    quote, i = text[i], i + 1
    while i < len(text) and text[i] != quote:
        if text[i] in "\r\n":
            break
        i += 2 if text[i] == "\\" else 1
    if i >= len(text) or text[i] != quote:
        raise ValueError("cannot analyse script: unterminated string literal")
    return i + 1


def _template_text(text, i):
    """Index after the closing backtick, or after `${`, plus whether an expression follows. Text is inert."""
    while i < len(text):
        if text[i] == "`":
            return i + 1, False
        if text.startswith("${", i):
            return i + 2, True
        i += 2 if text[i] == "\\" else 1
    raise ValueError("cannot analyse script: unterminated template literal")


def _regex(text, i):
    """Index after the regular-expression literal opening at text[i] (flags are left to the identifier scan)."""
    i, in_class = i + 1, False
    while i < len(text) and text[i] not in "\r\n\u2028\u2029" and (in_class or text[i] != "/"):
        in_class = (text[i] == "[") or (in_class and text[i] != "]")
        i += 2 if text[i] == "\\" else 1
    if i >= len(text) or text[i] != "/":
        raise ValueError("cannot analyse script: unterminated regular expression literal")
    return i + 1


def _literal_ref(text, construct, start, end):
    """The single literal URL argument of a fetch/import/Worker/... token, or None for import.meta."""
    tail = text[end:]
    if construct == "import" and re.match(r"\s*\.", tail):
        return None  # import.meta is not a request.
    call = re.match(r"\s*\(", tail)
    literal = (JS_LITERAL.match(text, end + call.end()) if call else
               JS_IMPORT.match(text, end) if construct == "import" else None)
    hint = "; multi-line imports are unsupported" if construct == "import" else ""
    ref = next(g for g in literal.groups() if g is not None) if literal else None
    multi = construct == "importScripts" and literal and text[literal.end()] != ")"  # takes any number of URLs
    if ref is None or multi or "${" in ref or (construct == "import" and "\n" in text[start:literal.end()]):
        raise ValueError(f"cannot statically resolve {construct} reference: use a single literal URL{hint}")
    return ref


def _slash(text, i):
    """Index after a `/` that follows `)` or `}` and so may divide or open a regex. Treated as division;
    rejected if the regex reading would swallow a quote or a checked name, since the two readings differ."""
    try:
        end = _regex(text, i)
    except ValueError:
        return i + 1
    if JS_SLASH_RISK.search(text, i, end):
        raise ValueError("cannot analyse script: a `/` after `)` or `}` may be division or a regular expression")
    return i + 1


def _skip_js_trivia(text, i):
    """Skip only whitespace and JS comments, preserving all four JS line terminators."""
    while i < len(text):
        if text[i].isspace():
            i += 1
        elif text.startswith("//", i):
            end = JS_LINE_END.search(text, i)
            i = end.start() if end else len(text)
        elif text.startswith("/*", i):
            end = text.find("*/", i + 2)
            if end < 0:
                raise ValueError("cannot analyse script: unterminated comment")
            i = end + 2
        else:
            break
    return i


def js_refs(text, *, function_body=False):
    """Yield the single literal URL of each fetch/import/Worker/... in code; raise ValueError for anything
    that cannot be resolved statically. Comments, strings, regex literals and template text are inert;
    `${...}` parts of a template are code. A heuristic lexer, not a parser: when unsure it must reject."""
    i, n, regex_ok, after_dot, templates = 0, len(text), True, False, []  # templates: open `{` count per `${`
    either = False  # the previous token was `)` or `}`: a following `/` is ambiguous
    braces = 0  # when recursively scanning a timer's function body, find its closing brace
    while i < n:
        i = _skip_js_trivia(text, i)
        if i == n:
            break
        c = text[i]
        if text.startswith(("<!--", "-->"), i):
            raise ValueError("HTML comment markers in JavaScript are not supported")
        if function_body and not templates:
            if c == "}":
                if braces == 0:
                    return i + 1
                braces -= 1
            elif c == "{":
                braces += 1
        dotted, after_dot = after_dot, c == "."
        ambiguous, either = either, either if c in "?." else c in ")}"
        if c == "/" and (regex_ok or ambiguous):
            i, regex_ok = (_slash(text, i), True) if ambiguous else (_regex(text, i), False)
        elif c in "\"'":
            i, regex_ok = _quoted(text, i), False
        elif c == "`" or (c == "}" and templates and templates[-1] == 0):
            if c == "}":
                templates.pop()
            i, expression = _template_text(text, i + 1)
            if expression:
                templates.append(0)
            regex_ok = expression
        elif c in "{}" and templates:
            templates[-1] += 1 if c == "{" else -1
            i, regex_ok = i + 1, True
        elif c.isdigit():
            i, regex_ok = JS_NUMBER.match(text, i).end(), False
        elif c == "\\":
            raise ValueError("cannot analyse script: unicode escape in an identifier")
        elif c == "[" and (not regex_ok or dotted):
            key = _skip_js_trivia(text, i + 1)
            # Dynamic array indices are used by the demos. Quoted keys must be
            # single, unescaped safe literals; call/function results are opaque.
            literal = JS_PROPERTY.match(text, key)
            safe_literal = (literal and text.startswith("]", _skip_js_trivia(text, literal.end())) and
                            next(g for g in literal.groups() if g is not None) not in
                            JS_CALLS | JS_DENIED | JS_CODE | JS_TIMERS | JS_GLOBALS | {"constructor", "serviceWorker"})
            if ambiguous or (key < n and text[key] in "\"'`" and not safe_literal):
                raise ValueError("computed string or function property access is not supported")
            i, regex_ok = i + 1, True
        elif word := JS_IDENT.match(text, i):
            name, start, i = word.group(), word.start(), word.end()
            regex_ok = name in JS_REGEX_AFTER and not dotted
            if name in JS_DENIED:
                raise ValueError(f"forbidden network API {name}: demos must work offline")
            if name in JS_CODE:
                raise ValueError(f"forbidden dynamic code API {name}")
            if name == "constructor" and dotted:
                raise ValueError("forbidden dynamic code property constructor")
            if name == "this":
                end = _skip_js_trivia(text, i)
                # Instance identity comparisons are used by boids. Any other bare
                # `this` could alias the global object, even through an expression.
                comparison = re.search(r"(?:===|!==|==|!=)\s*$", text[:start])
                if (end == n or text[end] != ".") and not (
                    comparison and (end == n or text[end] in ")};,&|")):
                    raise ValueError("computed or aliased global access this is not supported")
            if name in JS_GLOBALS:
                end = _skip_js_trivia(text, i)
                if (end == n or text[end] != "." or
                    not JS_IDENT.match(text, _skip_js_trivia(text, end + 1))):
                    raise ValueError(f"computed or aliased global access {name} is not supported: use a direct dot property")
            if name in JS_COMPUTED_GLOBALS:
                end = _skip_js_trivia(text, i)
                while text.startswith(")", end):
                    end = _skip_js_trivia(text, end + 1)
                if text.startswith("?.", end):
                    end = _skip_js_trivia(text, end + 2)
                if text.startswith("[", end):
                    raise ValueError(f"computed global access {name} is not supported")
            if name in JS_TIMERS:
                call = _skip_js_trivia(text, i)
                arg = _skip_js_trivia(text, call + 1) if call < n and text[call] == "(" else n
                arrow, func = JS_ARROW.match(text, arg), JS_FUNCTION.match(text, arg)
                if not arrow and not func:
                    raise ValueError(f"{name} requires a literal function callback; strings, computed callbacks and aliases are forbidden")
                if func:
                    # Do not exempt names in the header from the existing identifier checks.
                    yield from js_refs(text[arg:func.end()])
                    # A function expression can be coerced to a string: function(){} + code.
                    # Scan its whole body and require the next token to end the argument.
                    end = yield from js_refs(text[func.end():], function_body=True)
                    i = _skip_js_trivia(text, func.end() + end)
                    if i == n or text[i] not in ",)":
                        raise ValueError(f"{name} requires a literal function callback without further computation")
                    regex_ok, either = False, True
            if name == "serviceWorker":
                register = re.compile(r"\s*\.\s*register\b").match(text, i)
                if not register:
                    raise ValueError("serviceWorker may only appear as serviceWorker.register(<literal URL>)")
                name, i = "serviceWorker.register", register.end()
            if name == "export" and re.match(r"\s*[*{]", text[i:]):  # `export * from`, `export {a} from`
                head = JS_EXPORT.match(text, i)
                if not head:
                    raise ValueError("cannot statically resolve export reference: use a single literal URL")
                if re.compile(r"\s*from(?![\w$])").match(text, head.end()):
                    source = JS_EXPORT_FROM.match(text, head.end())
                    if not source:
                        raise ValueError("cannot statically resolve export reference: use a single literal URL")
                    yield next(g for g in source.groups() if g is not None)
            if name in JS_CALLS - {"register"} or name == "serviceWorker.register" or (name == "register" and dotted):
                ref = _literal_ref(text, name, start, i)
                if ref is not None:
                    yield ref
        elif c in "+-" and text.startswith(c * 2, i):
            i += 2  # ++ and -- keep the state: `a++ / 2` divides
        else:
            i, regex_ok = i + 1, c not in ")]"
    if templates:
        raise ValueError("cannot analyse script: unterminated template expression")
    if function_body:
        raise ValueError("cannot analyse script: unterminated timer function body")


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


def css_refs(text):
    """Decode CSS escapes before finding resource references in stylesheets or declarations."""
    def unescape(match):
        value = match.group(1)
        if value is None:
            return match.group(2) or ""
        code = int(value, 16)
        return chr(code) if 0 < code <= 0x10ffff and not 0xd800 <= code <= 0xdfff else "\ufffd"

    # Tokenize ORIGINAL source before decoding escapes. An escaped quote is string content,
    # and escaped / + * outside a string cannot manufacture a comment. Decoding first changes
    # those boundaries and can hide a later resource expression from the scan.
    escape = r"\\(?:([0-9a-fA-F]{1,6})(?:\r\n|[ \t\r\n\f])?|\r\n|[\r\n\f]|([^\r\n\f]))"
    strings = r'''"(?:[^"\\]|\\[\s\S])*"|'(?:[^'\\]|\\[\s\S])*' '''.rstrip()
    tokens = re.compile(r"/\*.*?(?:\*/|$)|" + strings + "|" + escape, re.S)
    normalized, code_parts, literals = [], [], {}
    end, offset = 0, 0
    for token in tokens.finditer(text):
        gap = text[end:token.start()]
        normalized.append(gap)
        code_parts.append(gap)
        offset += len(gap)
        raw = token.group()
        if raw.startswith("/*"):
            value, code_value = " ", " "
        elif raw[0] in "\"'":
            # Store values by TOKEN POSITION, not by a magic string that source could alias.
            literals[offset] = re.sub(escape, unescape, raw[1:-1])
            value, code_value = '"S"', "   "
        else:
            value = re.sub(escape, unescape, raw)
            code_value = value
        normalized.append(value)
        code_parts.append(code_value)
        offset += len(value)
        end = token.end()
    normalized.append(text[end:])
    code_parts.append(text[end:])
    uncommented, code = "".join(normalized), "".join(code_parts)

    # These resource functions may accept bare strings, not only url(...). None is used by the
    # reviewed demos; reject the whole construct until a dedicated grammar is supported.
    resource = re.search(r"(?<![\w-])((?:-webkit-)?(?:image-set|image|cross-fade)|src)\s*\(", code, re.I)
    if resource:
        raise ValueError(f"unsupported CSS resource function {resource.group(1)}: use url(<literal URL>)")

    urls = re.compile(r'''\burl\(\s*(?:(?P<quoted>"S")|(?P<bare>[^)"']*?))\s*\)''', re.I)
    for match in urls.finditer(uncommented):
        if match.group("quoted") is not None:
            position = match.start("quoted")
            if position not in literals:
                raise ValueError("unsupported CSS URL string boundary")
            yield literals[position]
        else:
            yield match.group("bare")
    for directive in re.finditer(r"@import(?![\w-])", code, re.I):
        tail = uncommented[directive.end():]
        literal = re.match(r'''\s*("S")''', tail)
        if literal:
            position = directive.end() + literal.start(1)
            if position not in literals:
                raise ValueError("unsupported CSS @import string boundary")
            yield literals[position]
        elif not re.match(r"\s*url\(", tail, re.I):
            raise ValueError("unsupported CSS @import: use a literal URL")


def local_refs(rel, text):
    """Yield (ref, kind, target), or raise ValueError for unresolved JavaScript references."""
    refs = [html.unescape(m.group(1)) for rx in REFS for m in rx.finditer(text)]
    scripts = [text] if Path(rel).suffix.lower() in {".js", ".mjs"} else []
    styles = [text] if Path(rel).suffix.lower() == ".css" else []
    if Path(rel).suffix.lower() in {".html", ".svg"}:
        parser = AttributeRefs()
        parser.feed(text)
        parser.close()
        refs.extend(parser.refs)
        scripts = parser.scripts
        styles = parser.styles
    for style in styles:
        refs.extend(css_refs(style))
    for script in scripts:
        refs.extend(js_refs(script))
    for ref in refs:
        ref = ref.strip()
        if ref.lower().startswith(("data:", "blob:")):
            raise ValueError("inline or opaque resource URLs are not supported")
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
