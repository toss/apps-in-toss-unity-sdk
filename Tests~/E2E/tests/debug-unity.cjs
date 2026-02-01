const { chromium } = require('playwright');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();

  const logs = [];
  const errors = [];

  page.on('console', msg => {
    const text = msg.text();
    logs.push('[' + msg.type() + '] ' + text);
    if (text.includes('RuntimeAPITester') || text.includes('exception') || text.includes('Error')) {
      console.log('[CONSOLE]', text);
    }
  });

  page.on('pageerror', err => {
    errors.push(err.message);
    console.log('[ERROR]', err.message);
  });

  await page.goto('http://localhost:4173/', { waitUntil: 'networkidle', timeout: 60000 });

  // Unity 초기화 대기
  try {
    await page.waitForFunction(() => typeof window['unityInstance'] !== 'undefined', { timeout: 60000 });
    console.log('[INFO] Unity instance initialized');
  } catch (e) {
    console.log('[ERROR] Unity instance not initialized:', e.message);
  }

  // 15초 대기하여 RuntimeAPITester 실행 확인
  await page.waitForTimeout(15000);

  // 결과 확인
  const hasResults = await page.evaluate(() => {
    return typeof window['__E2E_API_TEST_RESULTS__'] !== 'undefined';
  });

  console.log('[INFO] Has API test results:', hasResults);

  if (hasResults) {
    const results = await page.evaluate(() => window['__E2E_API_TEST_RESULTS__']);
    console.log('[INFO] API Test Results:', JSON.stringify(results, null, 2));
  }

  console.log('[INFO] Total error messages:', errors.length);
  errors.forEach((e, i) => console.log(`[ERROR ${i+1}]`, e));

  await browser.close();
  process.exit(hasResults ? 0 : 1);
})();
