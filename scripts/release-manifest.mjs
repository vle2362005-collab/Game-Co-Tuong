import { createHash } from 'node:crypto'
import { readdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join, relative } from 'node:path'
const root = 'artifacts/demo'
function entries(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    const path = join(directory, entry.name)
    return entry.isDirectory() ? entries(path) : [path]
  })
}
const files = entries(root).filter(path => !path.endsWith('manifest.json')).sort().map(path => ({
  path: relative(root, path).replaceAll('\\', '/'), sha256: createHash('sha256').update(readFileSync(path)).digest('hex')
}))
writeFileSync(join(root, 'manifest.json'), JSON.stringify({
  sourceCommit: readFileSync(join(root, 'SOURCE_COMMIT'), 'utf8').trim(),
  worktree: readFileSync(join(root, 'WORKTREE_STATE'), 'utf8').trim(), files
}, null, 2) + '\n')
console.log(`Demo package: ${files.length} files; SHA-256 manifest created.`)
