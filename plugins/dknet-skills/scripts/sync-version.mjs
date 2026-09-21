#!/usr/bin/env node
// Copies package.json's version into .claude-plugin/plugin.json. Runs automatically as the "version" npm script,
// so `npm version <x.y.z> --no-git-tag-version` stamps both files. The repository keeps the placeholder 0.0.0:
// the real version is the DKNet release version, injected by the publish-npm job in
// .github/workflows/dotnet-publish.yml right before `npm publish` — the same way -p:PackageVersion stamps NuGet.
import { readFileSync, writeFileSync } from 'node:fs'

const version = JSON.parse(readFileSync('package.json', 'utf8')).version
const manifest = JSON.parse(readFileSync('.claude-plugin/plugin.json', 'utf8'))
manifest.version = version
writeFileSync('.claude-plugin/plugin.json', JSON.stringify(manifest, null, 2) + '\n')
console.log(version)
