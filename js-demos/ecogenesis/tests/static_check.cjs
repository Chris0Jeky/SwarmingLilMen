"use strict";
const fs=require("node:fs"),path=require("node:path"),vm=require("node:vm"),crypto=require("node:crypto"),assert=require("node:assert/strict");
const root=path.resolve(__dirname,".."),html=fs.readFileSync(path.join(root,"index.html"),"utf8");
const scripts=[...html.matchAll(/<script\b([^>]*)>([\s\S]*?)<\/script>/gi)];
assert.ok(scripts.length>0,"No executable scripts found");
for(const [_,attrs,source] of scripts){assert.ok(!/\bsrc\s*=/i.test(attrs),"Standalone script may not depend on an external file");new vm.Script(source);}
assert.ok(!/<link[^>]+rel=["']stylesheet/i.test(html),"External stylesheet dependency");
assert.ok(!/<(?:script|img|iframe)[^>]+(?:src|href)=["']https?:/i.test(html),"Runtime network asset");
new vm.Script(fs.readFileSync(path.join(__dirname,"checks.js"),"utf8"));
console.log(JSON.stringify({scriptsParsed:scripts.length,htmlSHA256:crypto.createHash("sha256").update(html).digest("hex"),status:"passed"}));
