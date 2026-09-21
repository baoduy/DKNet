// extract.mjs <out-dir> <markdown files...>
// Pull every ```csharp / ```cs fence out of the given Markdown files into <out-dir>/*.cs so they can be compiled.
// Rules: a fence whose first line contains "no-compile" is skipped. `using` lines are hoisted to the top.
// A fence with no top-level type declaration is treated as statements and wrapped in a static method
// (string[] args, IServiceCollection services, WebApplicationBuilder builder are NOT pre-declared: the
// fence must declare what it uses). A fence that declares its own `namespace X;` is emitted as-is.
import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { basename } from 'node:path'
const [outDir, ...files] = process.argv.slice(2)
mkdirSync(outDir, { recursive: true })
let count = 0, skipped = 0
const typeDecl = /^\s*(?:\[[^\]]*\]\s*)*(?:public|internal|private|protected|file|static|sealed|abstract|partial|readonly|record|ref)?\s*(?:public|internal|private|protected|file|static|sealed|abstract|partial|readonly)*\s*(class|record|interface|struct|enum|delegate)\b/m
for (const file of files) {
  const lines = readFileSync(file, 'utf8').split('\n')
  let i = 0, n = 0
  while (i < lines.length) {
    const m = lines[i].match(/^\s*```\s*(csharp|cs|c#)\s*$/i)
    if (!m) { i++; continue }
    const start = i + 1; let j = start
    while (j < lines.length && !/^\s*```\s*$/.test(lines[j])) j++
    const body = lines.slice(start, j); i = j + 1; n++
    if (body.length && /no-compile/i.test(body[0])) { skipped++; continue }
    const usings = body.filter(l => /^\s*(global\s+)?using\s+[^(]/.test(l) && /;\s*$/.test(l))
    const rest = body.filter(l => !usings.includes(l))
    const fileTag = basename(file).replace(/[^A-Za-z0-9]/g, "_"); const tag = `${fileTag}_${n}`
    let out
    if (rest.some(l => /^\s*namespace\s+[\w.]+\s*;/.test(l))) {
      out = [...usings, ...rest].join('\n')
    } else if (typeDecl.test(rest.join('\n'))) {
      out = [...usings, '', `namespace SnippetLab.${fileTag}`, '{', ...rest, '}'].join('\n')
    } else {
      out = [...usings, '', `namespace SnippetLab.${fileTag}`, '{', `public static class Wrapper_${tag}`, '{', `public static async Task RunAsync(string[] args)`, '{', ...rest, '}', '}', '}'].join('\n')
    }
    writeFileSync(`${outDir}/${tag}.cs`, `// SOURCE: ${file} (fence #${n}, starts at line ${start + 1})\n${out}\n`)
    count++
  }
}
console.log(`extracted ${count} fence(s), skipped ${skipped} (no-compile)`)
