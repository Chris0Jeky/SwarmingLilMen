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
                                    '<link imagesrcset="data:image/png;base64,AA== 1x, ok.png 2x">'
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
        html = ('<a href="#x">x</a><img src="data:image/png;base64,AA==">'
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
                           ("navigator['serviceWorker']; sw.register(variable)", "register"),
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


if __name__ == "__main__":
    unittest.main()
