/**
 * Rich HTML API Changelog Report Generator
 *
 * 버전별 API 변화를 시각적으로 확인할 수 있는 self-contained HTML 리포트를 생성합니다.
 * - Changelog 탭: 버전 간 diff (추가/제거 API)
 * - Catalog 탭: 전체 버전별 API 카탈로그
 * - API 디테일 패널: 파라미터, 리턴 타입, 설명 등 상세 정보
 */

import type { ParsedAPI } from '../../../src/types.js';
import { getCategory } from '../../../src/categories.js';
import { mapToCSharpType } from '../../../src/validators/types.js';

interface SerializedParam {
  name: string;
  type: string;
  optional: boolean;
  description?: string;
}

interface SerializedAPI {
  name: string;
  pascalName: string;
  displayName: string;
  category: string;
  description?: string;
  returnDescription?: string;
  examples?: string[];
  parameters: SerializedParam[];
  returnType: string;
  isAsync: boolean;
  isCallbackBased?: boolean;
  isEventSubscription?: boolean;
  isDeprecated?: boolean;
  deprecatedMessage?: string;
  hasPermission: boolean;
  versions: string[];
}

function serializeAPI(api: ParsedAPI, versions: string[]): SerializedAPI {
  return {
    name: api.name,
    pascalName: api.pascalName,
    displayName: api.pascalName,
    category: getCategory(api.name),
    description: api.description,
    returnDescription: api.returnDescription,
    examples: api.examples,
    parameters: api.parameters.map(p => ({
      name: p.name,
      type: mapToCSharpType(p.type),
      optional: p.optional,
      description: p.description,
    })),
    returnType: mapToCSharpType(api.returnType),
    isAsync: api.isAsync,
    isCallbackBased: api.isCallbackBased,
    isEventSubscription: api.isEventSubscription,
    isDeprecated: api.isDeprecated,
    deprecatedMessage: api.deprecatedMessage,
    hasPermission: api.hasPermission,
    versions,
  };
}

interface APIChange {
  kind: 'param-added' | 'param-removed' | 'param-type-changed' | 'return-type-changed' | 'flag-changed';
  description: string;
}

interface ModifiedAPI {
  name: string;
  changes: APIChange[];
}

interface VersionDiff {
  from: string;
  to: string;
  added: string[];
  removed: string[];
  modified: ModifiedAPI[];
  totalApis: number;
}

function diffAPIs(prev: SerializedAPI, curr: SerializedAPI): APIChange[] {
  const changes: APIChange[] = [];

  // Compare parameters by name
  const prevParams = new Map(prev.parameters.map(p => [p.name, p]));
  const currParams = new Map(curr.parameters.map(p => [p.name, p]));

  for (const [name, cp] of currParams) {
    const pp = prevParams.get(name);
    if (!pp) {
      changes.push({ kind: 'param-added', description: `parameter added: ${name}: ${cp.type}${cp.optional ? '?' : ''}` });
    } else {
      if (pp.type !== cp.type) {
        changes.push({ kind: 'param-type-changed', description: `${name}: ${pp.type} → ${cp.type}` });
      }
      if (pp.optional !== cp.optional) {
        changes.push({ kind: 'param-type-changed', description: `${name}: ${pp.optional ? 'optional' : 'required'} → ${cp.optional ? 'optional' : 'required'}` });
      }
    }
  }
  for (const [name] of prevParams) {
    if (!currParams.has(name)) {
      changes.push({ kind: 'param-removed', description: `parameter removed: ${name}` });
    }
  }

  // Compare return type
  if (prev.returnType !== curr.returnType) {
    changes.push({ kind: 'return-type-changed', description: `return: ${prev.returnType} → ${curr.returnType}` });
  }

  // Compare flags
  const flags = ['isAsync', 'isDeprecated', 'isCallbackBased', 'isEventSubscription', 'hasPermission'] as const;
  for (const flag of flags) {
    if ((prev[flag] ?? false) !== (curr[flag] ?? false)) {
      changes.push({ kind: 'flag-changed', description: `${flag}: ${prev[flag] ?? false} → ${curr[flag] ?? false}` });
    }
  }

  return changes;
}

export function generateChangelogHTML(
  versionApis: Map<string, ParsedAPI[]>,
  categoryOrder: string[],
): string {
  const versions = [...versionApis.keys()];

  // API 인덱스 구축: 최신 버전 데이터 우선, 제거된 API도 보존
  const apiIndex = new Map<string, { api: ParsedAPI; versions: string[] }>();
  for (const [version, apis] of versionApis) {
    for (const api of apis) {
      const existing = apiIndex.get(api.name);
      if (existing) {
        existing.versions.push(version);
        existing.api = api; // 최신 버전 데이터로 갱신
      } else {
        apiIndex.set(api.name, { api, versions: [version] });
      }
    }
  }

  // Serialized API 데이터
  const serializedApis: Record<string, SerializedAPI> = {};
  for (const [name, { api, versions: apiVersions }] of apiIndex) {
    serializedApis[name] = serializeAPI(api, apiVersions);
  }

  // Diff 계산
  const diffs: VersionDiff[] = [];
  for (let i = 1; i < versions.length; i++) {
    const prevApis = versionApis.get(versions[i - 1])!;
    const currApis = versionApis.get(versions[i])!;
    const prevNames = new Set(prevApis.map(a => a.name));
    const currNames = new Set(currApis.map(a => a.name));
    const added = [...currNames].filter(n => !prevNames.has(n)).sort();
    const removed = [...prevNames].filter(n => !currNames.has(n)).sort();

    // Modified 감지: 양쪽에 모두 존재하는 API의 시그니처 비교
    const modified: ModifiedAPI[] = [];
    const commonNames = [...currNames].filter(n => prevNames.has(n));
    const prevApiMap = new Map(prevApis.map(a => [a.name, a]));
    const currApiMap = new Map(currApis.map(a => [a.name, a]));
    for (const name of commonNames) {
      const prevSerialized = serializeAPI(prevApiMap.get(name)!, [versions[i - 1]]);
      const currSerialized = serializeAPI(currApiMap.get(name)!, [versions[i]]);
      const changes = diffAPIs(prevSerialized, currSerialized);
      if (changes.length > 0) {
        modified.push({ name, changes });
      }
    }
    modified.sort((a, b) => a.name.localeCompare(b.name));

    if (added.length > 0 || removed.length > 0 || modified.length > 0) {
      diffs.push({
        from: versions[i - 1],
        to: versions[i],
        added,
        removed,
        modified,
        totalApis: currNames.size,
      });
    }
  }

  // 카테고리별 API 그룹핑 (버전별) — getCategory()로 정확한 분류
  const versionCatalog = new Map<string, Map<string, string[]>>();
  for (const [version, apis] of versionApis) {
    const catMap = new Map<string, string[]>();
    for (const api of apis) {
      const cat = getCategory(api.name);
      if (!catMap.has(cat)) catMap.set(cat, []);
      catMap.get(cat)!.push(api.name);
    }
    // 카테고리 내 API 정렬
    for (const [, apiList] of catMap) apiList.sort();
    // categoryOrder 순서로 정렬된 Map 생성
    const orderedMap = new Map<string, string[]>();
    for (const cat of categoryOrder) {
      if (catMap.has(cat)) orderedMap.set(cat, catMap.get(cat)!);
    }
    // categoryOrder에 없는 카테고리 추가
    for (const [cat, apiList] of catMap) {
      if (!orderedMap.has(cat)) orderedMap.set(cat, apiList);
    }
    versionCatalog.set(version, orderedMap);
  }

  const firstVersion = versions[0];
  const firstApis = versionApis.get(firstVersion)!;

  return `<!DOCTYPE html>
<html lang="ko">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1.0">
<title>Apps in Toss Unity SDK — API Changelog</title>
<style>
:root {
  --color-bg: #ffffff;
  --color-bg-secondary: #f6f8fa;
  --color-bg-tertiary: #f0f2f5;
  --color-border: #d1d9e0;
  --color-border-light: #e8ecf0;
  --color-text: #1f2328;
  --color-text-secondary: #656d76;
  --color-text-tertiary: #8b949e;
  --color-accent: #0969da;
  --color-accent-bg: #ddf4ff;
  --color-green: #1a7f37;
  --color-green-bg: #dafbe1;
  --color-green-border: #4ac26b;
  --color-red: #cf222e;
  --color-red-bg: #ffebe9;
  --color-red-border: #f87171;
  --color-yellow: #9a6700;
  --color-yellow-bg: #fff8c5;
  --color-purple: #8250df;
  --color-purple-bg: #fbefff;
  --font-sans: -apple-system, BlinkMacSystemFont, "Segoe UI", "Noto Sans", Helvetica, Arial, sans-serif;
  --font-mono: ui-monospace, SFMono-Regular, "SF Mono", Menlo, Consolas, "Liberation Mono", monospace;
  --radius: 6px;
  --shadow: 0 1px 3px rgba(0,0,0,0.08), 0 1px 2px rgba(0,0,0,0.06);
  --shadow-lg: 0 4px 12px rgba(0,0,0,0.12);
  --panel-width: 480px;
}

* { margin: 0; padding: 0; box-sizing: border-box; }

body {
  font-family: var(--font-sans);
  color: var(--color-text);
  background: var(--color-bg);
  line-height: 1.5;
  font-size: 14px;
}

.header {
  background: var(--color-bg);
  border-bottom: 1px solid var(--color-border);
  padding: 16px 24px;
  position: sticky;
  top: 0;
  z-index: 100;
}

.header h1 {
  font-size: 20px;
  font-weight: 600;
  margin-bottom: 4px;
}

.header .subtitle {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.tabs {
  display: flex;
  gap: 0;
  border-bottom: 1px solid var(--color-border);
  background: var(--color-bg);
  padding: 0 24px;
  position: sticky;
  top: 63px;
  z-index: 99;
}

.tab {
  padding: 10px 16px;
  cursor: pointer;
  font-size: 14px;
  font-weight: 500;
  color: var(--color-text-secondary);
  border-bottom: 2px solid transparent;
  transition: all 0.15s;
  user-select: none;
}

.tab:hover { color: var(--color-text); }
.tab.active {
  color: var(--color-accent);
  border-bottom-color: var(--color-accent);
}

.content { padding: 24px; max-width: 960px; }
.tab-panel { display: none; }
.tab-panel.active { display: block; }

/* Changelog */
.diff-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  margin-bottom: 16px;
  overflow: hidden;
}

.diff-header {
  background: var(--color-bg-secondary);
  padding: 12px 16px;
  font-weight: 600;
  font-size: 14px;
  display: flex;
  justify-content: space-between;
  align-items: center;
  border-bottom: 1px solid var(--color-border-light);
}

.diff-header .arrow { color: var(--color-text-tertiary); margin: 0 8px; }
.diff-stats { font-weight: 400; font-size: 12px; color: var(--color-text-secondary); }
.diff-stats .added-count { color: var(--color-green); }
.diff-stats .modified-count { color: var(--color-yellow); }
.diff-stats .removed-count { color: var(--color-red); }

.diff-body { padding: 0; }

.diff-line {
  padding: 6px 16px;
  font-family: var(--font-mono);
  font-size: 13px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 8px;
  transition: background 0.1s;
}

.diff-line:hover { background: var(--color-bg-tertiary); }

.diff-line.added {
  background: var(--color-green-bg);
  color: var(--color-green);
}
.diff-line.added:hover { background: #c8f7d5; }

.diff-line.removed {
  background: var(--color-red-bg);
  color: var(--color-red);
}
.diff-line.removed:hover { background: #fdd; }

.diff-line.modified {
  background: var(--color-yellow-bg);
  color: var(--color-yellow);
}
.diff-line.modified:hover { background: #fff0b3; }

.diff-line .sign {
  font-weight: 700;
  width: 14px;
  text-align: center;
  flex-shrink: 0;
}

.diff-line .api-name { flex: 1; }
.diff-changes { font-size: 12px; color: var(--color-text-secondary); margin-left: 4px; }

.no-changes {
  text-align: center;
  padding: 48px 24px;
  color: var(--color-text-tertiary);
}

/* Catalog */
.catalog-toolbar {
  display: flex;
  gap: 8px;
  margin-bottom: 16px;
}

.catalog-toolbar button {
  padding: 6px 12px;
  font-size: 12px;
  font-weight: 500;
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  background: var(--color-bg);
  color: var(--color-text-secondary);
  cursor: pointer;
  transition: all 0.15s;
}

.catalog-toolbar button:hover {
  background: var(--color-bg-secondary);
  color: var(--color-text);
}

.version-section {
  margin-bottom: 8px;
}

.version-section > summary {
  padding: 10px 16px;
  font-weight: 600;
  font-size: 14px;
  cursor: pointer;
  border: 1px solid var(--color-border);
  border-radius: var(--radius);
  background: var(--color-bg-secondary);
  list-style: none;
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}

.version-section > summary::-webkit-details-marker { display: none; }

.version-section > summary::before {
  content: '▶';
  font-size: 10px;
  color: var(--color-text-tertiary);
  transition: transform 0.15s;
  display: inline-block;
  width: 14px;
}

.version-section[open] > summary::before {
  transform: rotate(90deg);
}

.version-section > summary .api-count {
  color: var(--color-text-tertiary);
  font-weight: 400;
  font-size: 12px;
  margin-left: auto;
}

.version-content {
  padding: 8px 0 8px 16px;
  border-left: 2px solid var(--color-border-light);
  margin-left: 22px;
  margin-top: 4px;
}

.category-group { margin-bottom: 8px; }

.category-label {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-text-tertiary);
  padding: 4px 0;
}

.api-item {
  padding: 4px 8px;
  font-family: var(--font-mono);
  font-size: 13px;
  cursor: pointer;
  border-radius: 4px;
  transition: background 0.1s;
  display: inline-flex;
  align-items: center;
  gap: 6px;
}

.api-item:hover { background: var(--color-accent-bg); color: var(--color-accent); }

/* Base version info */
.base-version {
  padding: 12px 16px;
  border: 1px solid var(--color-border-light);
  border-radius: var(--radius);
  background: var(--color-bg-secondary);
  margin-bottom: 16px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.base-version strong { color: var(--color-text); }

/* Detail panel */
.detail-overlay {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.3);
  z-index: 200;
}

.detail-overlay.open { display: block; }

.detail-panel {
  position: fixed;
  top: 0;
  right: 0;
  width: var(--panel-width);
  height: 100vh;
  background: var(--color-bg);
  border-left: 1px solid var(--color-border);
  box-shadow: var(--shadow-lg);
  z-index: 201;
  transform: translateX(100%);
  transition: transform 0.2s ease;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
}

.detail-panel.open { transform: translateX(0); }

.detail-header {
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border);
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  flex-shrink: 0;
}

.detail-header h2 {
  font-size: 18px;
  font-family: var(--font-mono);
  word-break: break-all;
}

.close-btn {
  background: none;
  border: none;
  font-size: 20px;
  cursor: pointer;
  color: var(--color-text-tertiary);
  padding: 4px;
  line-height: 1;
  border-radius: 4px;
  flex-shrink: 0;
}

.close-btn:hover { background: var(--color-bg-tertiary); color: var(--color-text); }

.detail-body {
  padding: 20px;
  flex: 1;
  overflow-y: auto;
}

.detail-section { margin-bottom: 20px; }
.detail-section:last-child { margin-bottom: 0; }

.detail-section h3 {
  font-size: 11px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  color: var(--color-text-tertiary);
  margin-bottom: 8px;
}

.badges {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
  margin-bottom: 16px;
}

.badge {
  display: inline-flex;
  padding: 2px 8px;
  font-size: 11px;
  font-weight: 500;
  border-radius: 12px;
  border: 1px solid;
}

.badge.async { background: var(--color-accent-bg); color: var(--color-accent); border-color: #b6d4fe; }
.badge.callback { background: var(--color-purple-bg); color: var(--color-purple); border-color: #d8b4fe; }
.badge.event { background: var(--color-purple-bg); color: var(--color-purple); border-color: #d8b4fe; }
.badge.deprecated { background: var(--color-yellow-bg); color: var(--color-yellow); border-color: #ecd06f; }
.badge.permission { background: var(--color-green-bg); color: var(--color-green); border-color: var(--color-green-border); }
.badge.category-badge { background: var(--color-bg-secondary); color: var(--color-text-secondary); border-color: var(--color-border); }

.description { color: var(--color-text); line-height: 1.6; }

.param-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}

.param-table th {
  text-align: left;
  font-weight: 600;
  padding: 6px 8px;
  border-bottom: 2px solid var(--color-border);
  font-size: 12px;
  color: var(--color-text-secondary);
}

.param-table td {
  padding: 6px 8px;
  border-bottom: 1px solid var(--color-border-light);
  vertical-align: top;
}

.param-table .param-name {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--color-accent);
  white-space: nowrap;
}

.param-table .param-type {
  font-family: var(--font-mono);
  font-size: 12px;
  color: var(--color-text-secondary);
  word-break: break-all;
}

.param-table .optional-tag {
  font-size: 10px;
  color: var(--color-text-tertiary);
}

.return-type {
  font-family: var(--font-mono);
  font-size: 13px;
  padding: 8px 12px;
  background: var(--color-bg-secondary);
  border-radius: var(--radius);
  border: 1px solid var(--color-border-light);
  word-break: break-all;
}

.return-desc {
  margin-top: 6px;
  font-size: 13px;
  color: var(--color-text-secondary);
}

.code-example {
  font-family: var(--font-mono);
  font-size: 12px;
  padding: 12px;
  background: #24292e;
  color: #e1e4e8;
  border-radius: var(--radius);
  overflow-x: auto;
  white-space: pre;
  line-height: 1.5;
}

.version-list {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
}

.version-tag {
  padding: 2px 8px;
  font-size: 11px;
  font-family: var(--font-mono);
  background: var(--color-bg-secondary);
  border: 1px solid var(--color-border-light);
  border-radius: 12px;
  color: var(--color-text-secondary);
}

/* Summary */
.summary-bar {
  display: flex;
  gap: 16px;
  padding: 12px 24px;
  background: var(--color-bg-secondary);
  border-bottom: 1px solid var(--color-border);
  font-size: 13px;
  color: var(--color-text-secondary);
  flex-wrap: wrap;
}

.summary-bar .stat { display: flex; align-items: center; gap: 4px; }
.summary-bar .stat strong { color: var(--color-text); font-weight: 600; }

/* Responsive */
@media (max-width: 768px) {
  .content { padding: 16px; }
  .detail-panel { width: 100%; }
  .detail-overlay { background: rgba(0,0,0,0.5); }
  .header { padding: 12px 16px; }
  .tabs { padding: 0 16px; }
  .summary-bar { padding: 8px 16px; }
}
</style>
</head>
<body>

<div class="header">
  <h1>Apps in Toss Unity SDK — API Changelog</h1>
  <div class="subtitle">Generated ${new Date().toISOString().split('T')[0]} · ${versions.length} versions · ${apiIndex.size} unique APIs</div>
</div>

<div class="summary-bar">
  <span class="stat">Versions: <strong>${versions[0]}</strong> → <strong>${versions[versions.length - 1]}</strong></span>
  <span class="stat">Total APIs: <strong>${apiIndex.size}</strong></span>
  <span class="stat">Changes: <strong>${diffs.length}</strong> version transitions with diffs</span>
</div>

<div class="tabs">
  <div class="tab active" data-tab="changelog">Changelog</div>
  <div class="tab" data-tab="catalog">Catalog</div>
</div>

<div class="content">
  <!-- Changelog Tab -->
  <div id="tab-changelog" class="tab-panel active">
    <div class="base-version">
      Base version: <strong>v${firstVersion}</strong> — ${firstApis.length} APIs
    </div>
${diffs.length === 0
  ? '    <div class="no-changes">No API changes detected across versions.</div>'
  : diffs.map(d => `    <div class="diff-card">
      <div class="diff-header">
        <span>v${d.from} <span class="arrow">→</span> v${d.to}</span>
        <span class="diff-stats">${d.totalApis} APIs · <span class="added-count">+${d.added.length}</span> / <span class="modified-count">~${d.modified.length}</span> / <span class="removed-count">-${d.removed.length}</span></span>
      </div>
      <div class="diff-body">
${d.added.map(a => `        <div class="diff-line added" data-api="${escapeAttr(a)}"><span class="sign">+</span><span class="api-name">${escapeHtml(serializedApis[a]?.displayName ?? a)}</span></div>`).join('\n')}
${d.modified.map(m => `        <div class="diff-line modified" data-api="${escapeAttr(m.name)}"><span class="sign">~</span><span class="api-name">${escapeHtml(serializedApis[m.name]?.displayName ?? m.name)}</span><span class="diff-changes">${escapeHtml(m.changes.map(c => c.description).join('; '))}</span></div>`).join('\n')}
${d.removed.map(r => `        <div class="diff-line removed" data-api="${escapeAttr(r)}"><span class="sign">−</span><span class="api-name">${escapeHtml(serializedApis[r]?.displayName ?? r)}</span></div>`).join('\n')}
      </div>
    </div>`).join('\n')}
  </div>

  <!-- Catalog Tab -->
  <div id="tab-catalog" class="tab-panel">
    <div class="catalog-toolbar">
      <button onclick="expandAll()">Expand All</button>
      <button onclick="collapseAll()">Collapse All</button>
    </div>
${[...versionCatalog.entries()].reverse().map(([version, catMap]) => {
  const totalApis = [...catMap.values()].reduce((s, a) => s + a.length, 0);
  return `    <details class="version-section">
      <summary>v${version} <span class="api-count">${totalApis} APIs</span></summary>
      <div class="version-content">
${[...catMap.entries()].map(([cat, apis]) => `        <div class="category-group">
          <div class="category-label">${escapeHtml(cat)} (${apis.length})</div>
${apis.map(a => `          <div class="api-item" data-api="${escapeAttr(a)}">${escapeHtml(serializedApis[a]?.displayName ?? a)}</div>`).join('\n')}
        </div>`).join('\n')}
      </div>
    </details>`;
}).join('\n')}
  </div>
</div>

<!-- Detail Panel -->
<div class="detail-overlay" id="detailOverlay"></div>
<div class="detail-panel" id="detailPanel">
  <div class="detail-header">
    <h2 id="detailTitle"></h2>
    <button class="close-btn" onclick="closeDetail()">&times;</button>
  </div>
  <div class="detail-body" id="detailBody"></div>
</div>

<script type="application/json" id="apiData">${JSON.stringify(serializedApis)}</script>

<script>
(function() {
  const apiData = JSON.parse(document.getElementById('apiData').textContent);

  // Tab switching
  document.querySelectorAll('.tab').forEach(tab => {
    tab.addEventListener('click', () => {
      document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
      document.querySelectorAll('.tab-panel').forEach(p => p.classList.remove('active'));
      tab.classList.add('active');
      document.getElementById('tab-' + tab.dataset.tab).classList.add('active');
    });
  });

  // API click → detail panel
  document.addEventListener('click', (e) => {
    const el = e.target.closest('[data-api]');
    if (el) openDetail(el.dataset.api);
  });

  function openDetail(name) {
    const api = apiData[name];
    if (!api) return;

    const panel = document.getElementById('detailPanel');
    const overlay = document.getElementById('detailOverlay');
    document.getElementById('detailTitle').textContent = api.displayName || api.name;

    let html = '';

    // Badges
    const badges = [];
    badges.push('<span class="badge category-badge">' + esc(api.category) + '</span>');
    if (api.isAsync) badges.push('<span class="badge async">async</span>');
    if (api.isCallbackBased) badges.push('<span class="badge callback">callback</span>');
    if (api.isEventSubscription) badges.push('<span class="badge event">event</span>');
    if (api.isDeprecated) badges.push('<span class="badge deprecated">deprecated</span>');
    if (api.hasPermission) badges.push('<span class="badge permission">permission</span>');
    html += '<div class="badges">' + badges.join('') + '</div>';

    // Deprecated message
    if (api.isDeprecated && api.deprecatedMessage) {
      html += '<div class="detail-section"><div class="description" style="color:var(--color-yellow);">' + esc(api.deprecatedMessage) + '</div></div>';
    }

    // Description
    if (api.description) {
      html += '<div class="detail-section"><h3>Description</h3><div class="description">' + esc(api.description) + '</div></div>';
    }

    // Parameters
    if (api.parameters.length > 0) {
      html += '<div class="detail-section"><h3>Parameters</h3><table class="param-table"><thead><tr><th>Name</th><th>Type</th><th>Description</th></tr></thead><tbody>';
      for (const p of api.parameters) {
        html += '<tr><td class="param-name">' + esc(p.name) + (p.optional ? ' <span class="optional-tag">optional</span>' : '') + '</td>';
        html += '<td class="param-type">' + esc(p.type) + '</td>';
        html += '<td>' + (p.description ? esc(p.description) : '<span style="color:var(--color-text-tertiary)">—</span>') + '</td></tr>';
      }
      html += '</tbody></table></div>';
    }

    // Return type
    html += '<div class="detail-section"><h3>Return Type</h3><div class="return-type">' + esc(api.returnType) + '</div>';
    if (api.returnDescription) {
      html += '<div class="return-desc">' + esc(api.returnDescription) + '</div>';
    }
    html += '</div>';

    // Code examples
    if (api.examples && api.examples.length > 0) {
      html += '<div class="detail-section"><h3>Examples</h3>';
      for (const ex of api.examples) {
        html += '<div class="code-example">' + esc(ex) + '</div>';
      }
      html += '</div>';
    }

    // Versions
    html += '<div class="detail-section"><h3>Available in Versions</h3><div class="version-list">';
    for (const v of api.versions) {
      html += '<span class="version-tag">v' + esc(v) + '</span>';
    }
    html += '</div></div>';

    document.getElementById('detailBody').innerHTML = html;
    panel.classList.add('open');
    overlay.classList.add('open');
  }

  window.closeDetail = function() {
    document.getElementById('detailPanel').classList.remove('open');
    document.getElementById('detailOverlay').classList.remove('open');
  };

  document.getElementById('detailOverlay').addEventListener('click', closeDetail);
  document.addEventListener('keydown', (e) => {
    if (e.key === 'Escape') closeDetail();
  });

  // Catalog controls
  window.expandAll = function() {
    document.querySelectorAll('.version-section').forEach(d => d.open = true);
  };
  window.collapseAll = function() {
    document.querySelectorAll('.version-section').forEach(d => d.open = false);
  };

  function esc(s) {
    if (!s) return '';
    const d = document.createElement('div');
    d.textContent = String(s);
    return d.innerHTML;
  }
})();
</script>
</body>
</html>`;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function escapeAttr(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/"/g, '&quot;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}
