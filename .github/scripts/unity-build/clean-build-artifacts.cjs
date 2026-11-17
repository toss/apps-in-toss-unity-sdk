const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');

const projectPath = path.join('Tests~', 'E2E', `${process.env.PROJECT_DIR_PREFIX}-${process.env.UNITY_VERSION}`);
const rm = (rel) => {
  const p = path.join(projectPath, rel);
  if (fs.existsSync(p)) {
    fs.rmSync(p, { recursive: true, force: true });
    console.log(`  removed: ${p}`);
  }
};

console.log('Cleaning previous build artifacts...');
rm('ait-build');
rm('webgl');

if (process.env.CLEAN_LIBRARY === 'true') {
  console.log('Cleaning Library cache (requested via input)...');
  rm('Library');
  console.log('Done.');
  return;
}

let beforeRef = process.env.BEFORE_SHA;
if (!beforeRef || /^0+$/.test(beforeRef)) beforeRef = 'HEAD~1';

let changed = null;
let diffFailed = false;
try {
  execFileSync('git', ['rev-parse', '--verify', beforeRef], { stdio: 'ignore' });
  changed = execFileSync('git', [
    'diff', '--name-only', beforeRef, 'HEAD', '--',
    'Runtime/SDK/', '*.asmdef', '*.jslib',
  ], { encoding: 'utf8' }).trim();
} catch {
  diffFailed = true;
}

if (diffFailed) {
  console.log('Cannot resolve git diff base — wiping Bee conservatively');
  rm(path.join('Library', 'Bee'));
} else if (changed && changed.length > 0) {
  console.log('SDK/asmdef/jslib changes detected — wiping Bee to prevent stale ref.dll:');
  for (const line of changed.split(/\r?\n/)) console.log('  ' + line);
  rm(path.join('Library', 'Bee'));
} else {
  console.log('No SDK/asmdef/jslib changes — preserving Bee for incremental build');
}
console.log('Done.');
