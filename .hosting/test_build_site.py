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


if __name__ == "__main__":
    unittest.main()
