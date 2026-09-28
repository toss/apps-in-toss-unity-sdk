// @ts-check
// e2e-full-pipeline.test.js에서 추출한 PlayerPrefs 프로브 유틸리티. 함수 본문은 원본과 동일하다.
// 이 디렉터리(lib/) 파일은 이름이 *.test.js/*.spec.js가 되면 안 된다(playwright 기본 testMatch에 잡힘).

/**
 * window.__E2E_PLAYERPREFS_DATA__를 지운 뒤 triggerFn()을 실행하고,
 * 지정한 op으로 결과가 도착할 때까지 폴링한다 (PlayerPrefsTester → E2ETestBridge.jslib 계약).
 * 같은 page를 여러 케이스에서 재사용할 때 이전 결과 잔재를 읽지 않도록 매번 지우고 시작한다.
 */
export async function triggerPlayerPrefsAndWait(page, triggerFn, expectedOp, timeoutMs = 10000) {
  await page.evaluate(() => { delete window['__E2E_PLAYERPREFS_DATA__']; });
  await triggerFn();
  await page.waitForFunction((op) => {
    const d = window['__E2E_PLAYERPREFS_DATA__'];
    return d !== undefined && d !== null && d.op === op;
  }, expectedOp, { timeout: timeoutMs });
  return page.evaluate(() => window['__E2E_PLAYERPREFS_DATA__']);
}

/**
 * mock 백킹(localStorage)에 기록된 AIT 매니페스트의 /PlayerPrefs 엔트리 수.
 * 매니페스트가 아직 없으면 null (= 아무것도 기록하지 않음)이고, 0이면 **빈 매니페스트**다.
 * 빈 매니페스트는 다음 부팅을 'present' 분기로 보내 레거시 마이그레이션 창을 영구히
 * 닫아버리므로, 실을 데이터가 없는 부팅에서는 애초에 기록되지 않아야 한다.
 */
export async function scopedFileCountInManifest(page, prefix) {
  return page.evaluate((p) => {
    const raw = window.localStorage.getItem(p + 'AITUnityFS_v1_manifest');
    if (raw === null) return null;
    try {
      const files = JSON.parse(JSON.parse(raw).inline).files || {};
      return Object.keys(files).filter((k) => /\/PlayerPrefs$/.test(k)).length;
    } catch (e) {
      return -1; // 우리 포맷이 아니다 — 이 단언의 관심사가 아니므로 0이 아닌 값으로
    }
  }, prefix);
}

/**
 * IDBFS 백킹 IndexedDB(DB명 '/idbfs', 오브젝트스토어 'FILE_DATA')에서 PlayerPrefs
 * 엔트리의 원본 바이트를 읽는다. 9-8이 손으로 만든 바이트가 아니라 **실제 Unity가 쓴**
 * PlayerPrefs 포맷을 레거시 덤프로 재사용하기 위한 추출기다.
 *
 * open이 onsuccess/onerror/onupgradeneeded 중 아무것도 발화하지 않는 무응답 사례가
 * 실측돼 자체 타임박스를 둔다(2021.3 계열 순정 IDBFS 세션 노화 — TODO.md P2 참조).
 */
export async function readPlayerPrefsEntryFromIdb(page, timeoutMs) {
  return page.evaluate((limit) => {
    const probe = new Promise((resolve, reject) => {
      try {
        const req = indexedDB.open('/idbfs');
        req.onerror = () => reject(req.error || new Error('idb open failed'));
        req.onsuccess = () => {
          try {
            const db = req.result;
            const names = Array.from(db.objectStoreNames);
            const store = names.includes('FILE_DATA') ? 'FILE_DATA' : names[0];
            const tx = db.transaction(store, 'readonly');
            let found = null;
            const cur = tx.objectStore(store).openCursor();
            cur.onsuccess = () => {
              const c = cur.result;
              if (c) {
                const k = String(c.key);
                if (/\/PlayerPrefs$/.test(k)) {
                  const v = c.value || {};
                  found = {
                    mode: v.mode,
                    timestamp: v.timestamp ? new Date(v.timestamp).getTime() : 0,
                    contents: v.contents ? Array.from(v.contents) : []
                  };
                }
                c.continue();
              } else {
                db.close();
                if (found) resolve(found); else reject(new Error('PlayerPrefs entry not found in IDBFS'));
              }
            };
            cur.onerror = () => { db.close(); reject(cur.error); };
          } catch (e) { reject(e); }
        };
      } catch (e) { reject(e); }
    });
    return Promise.race([
      probe,
      new Promise((_, reject) => setTimeout(() => reject(new Error('idb probe timeout')), limit))
    ]);
  }, timeoutMs);
}

/**
 * mock 백킹(localStorage)의 AIT 매니페스트에서 PlayerPrefs 엔트리를 직접 추출한다.
 * readPlayerPrefsEntryFromIdb(IndexedDB 프로브)와 달리 이 경로는 IDBFS를 전혀 거치지
 * 않는다 — 레이어 push는 collectScoped가 MEMFS를 직접 읽어 매니페스트를 만들므로
 * IDBFS 세션 노화 결함(TODO.md P2)과 무관하다. 9-1/9-2가 2021.3에서도 green인 것과
 * 같은 이유로, 9-8 1단계의 seed 추출은 이 경로를 1차로 쓴다(라운드 7, run 32662771953).
 */
export async function readPlayerPrefsEntryFromMockManifest(page, mockPrefix) {
  return page.evaluate((prefix) => {
    const raw = window.localStorage.getItem(prefix + 'AITUnityFS_v1_manifest');
    if (raw === null) throw new Error('mock manifest not found for prefix ' + prefix);
    const envelope = JSON.parse(raw);
    const snapshot = JSON.parse(envelope.inline);
    const files = snapshot.files || {};
    const key = Object.keys(files).find((k) => /\/PlayerPrefs$/.test(k));
    if (!key) throw new Error('no /PlayerPrefs entry in mock manifest for prefix ' + prefix);
    const f = files[key];
    const bin = atob(f.d || '');
    const bytes = new Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return { mode: f.m, timestamp: f.t || 0, contents: bytes };
  }, mockPrefix);
}
