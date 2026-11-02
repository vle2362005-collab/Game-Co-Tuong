import { readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
const [source = 'artifacts/qa/backend.trx', target = 'artifacts/qa/performance.json'] = process.argv.slice(2)
const text = readFileSync(source, 'utf8').replace(/&quot;/g, '"').replace(/&lt;/g, '<').replace(/&gt;/g, '>').replace(/&amp;/g, '&')
const match = text.match(/GCT97_JSON=(\{[^\r\n]+\})/)
if (!match) throw new Error('No GCT-97 measurements found in this TRX report.')
const data = JSON.parse(match[1])
mkdirSync(dirname(target), { recursive: true })
writeFileSync(target, JSON.stringify(data, null, 2) + '\n')
console.log(JSON.stringify(data, null, 2))
