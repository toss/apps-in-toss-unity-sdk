#!/usr/bin/env node
/**
 * 벤치마크 결과 시각화 리포트 생성 스크립트
 *
 * macOS/Windows × 5개 Unity 버전의 벤치마크 데이터를 파싱하여
 * 마크다운 테이블, 프로그레스바, 차트 이미지를 생성합니다.
 */

import fs from "fs";
import path from "path";

const UNITY_VERSIONS = ["2021.3", "2022.3", "6000.0", "6000.2", "6000.3"];
const OS_LIST = ["macos", "windows"];

// 벤치마크 기준값
const THRESHOLDS = {
  BUILD_SIZE_MB: 50,
  MIN_FPS: 30,
  MAX_FPS: 60,
  MAX_MEMORY_MB: 512,
  MAX_LOAD_TIME_MS: 10000,
};

/**
 * artifacts 디렉토리에서 벤치마크 데이터 로드
 */
function loadBenchmarkData() {
  const data = {};

  for (const os of OS_LIST) {
    data[os] = {};
    for (const version of UNITY_VERSIONS) {
      const filePath = path.join(
        "artifacts",
        `benchmark-results-${os}-${version}`,
        "benchmark-results.json"
      );
      if (fs.existsSync(filePath)) {
        try {
          data[os][version] = JSON.parse(fs.readFileSync(filePath, "utf8"));
        } catch (e) {
          console.error(`Failed to parse ${filePath}: ${e.message}`);
        }
      }
    }
  }

  return data;
}

/**
 * 프로그레스바 생성
 * @param {number} value - 현재 값
 * @param {number} max - 최대 값
 * @param {number} width - 바 너비 (문자 수)
 * @returns {string} 프로그레스바 문자열
 */
function progressBar(value, max, width = 10) {
  if (value === null || value === undefined || isNaN(value)) {
    return "[" + "░".repeat(width) + "]";
  }
  const ratio = Math.min(Math.max(value / max, 0), 1);
  const filled = Math.round(ratio * width);
  const empty = width - filled;
  return "[" + "█".repeat(filled) + "░".repeat(empty) + "]";
}

/**
 * 상태 이모지 반환
 */
function statusEmoji(passed) {
  return passed ? "✅" : "❌";
}

/**
 * 경고 상태 이모지 반환
 */
function warningEmoji(value, threshold, isLowerBetter = true) {
  if (value === null || value === undefined) return "⏳";
  if (isLowerBetter) {
    return value <= threshold ? "✅" : "⚠️";
  }
  return value >= threshold ? "✅" : "⚠️";
}

/**
 * QuickChart.io URL 생성
 */
function generateQuickChartUrl(config) {
  const encoded = encodeURIComponent(JSON.stringify(config));
  return `https://quickchart.io/chart?c=${encoded}&w=600&h=300&bkg=white`;
}

/**
 * 빌드 크기 비교 막대 차트 URL 생성
 */
function generateBuildSizeChart(data) {
  const macosData = UNITY_VERSIONS.map(
    (v) => data.macos[v]?.buildSize?.toFixed(2) || 0
  );
  const windowsData = UNITY_VERSIONS.map(
    (v) => data.windows[v]?.buildSize?.toFixed(2) || 0
  );

  const config = {
    type: "bar",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "macOS",
          data: macosData,
          backgroundColor: "rgba(59, 130, 246, 0.8)",
        },
        {
          label: "Windows",
          data: windowsData,
          backgroundColor: "rgba(239, 68, 68, 0.8)",
        },
      ],
    },
    options: {
      title: { display: true, text: "Build Size by Unity Version (MB)" },
      scales: { yAxes: [{ ticks: { beginAtZero: true } }] },
    },
  };

  return generateQuickChartUrl(config);
}

/**
 * FPS 성능 비교 라인 차트 URL 생성
 */
function generateFpsChart(data) {
  const macosAvgFps = UNITY_VERSIONS.map(
    (v) => data.macos[v]?.benchmarkData?.avgFps?.toFixed(1) || 0
  );
  const windowsAvgFps = UNITY_VERSIONS.map(
    (v) => data.windows[v]?.benchmarkData?.avgFps?.toFixed(1) || 0
  );

  const config = {
    type: "line",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "macOS Avg FPS",
          data: macosAvgFps,
          borderColor: "rgba(59, 130, 246, 1)",
          backgroundColor: "rgba(59, 130, 246, 0.1)",
          fill: true,
        },
        {
          label: "Windows Avg FPS",
          data: windowsAvgFps,
          borderColor: "rgba(239, 68, 68, 1)",
          backgroundColor: "rgba(239, 68, 68, 0.1)",
          fill: true,
        },
      ],
    },
    options: {
      title: { display: true, text: "Average FPS by Unity Version" },
      scales: { yAxes: [{ ticks: { beginAtZero: true } }] },
    },
  };

  return generateQuickChartUrl(config);
}

/**
 * 로드 시간 비교 차트 URL 생성
 */
function generateLoadTimeChart(data) {
  const macosPageLoad = UNITY_VERSIONS.map(
    (v) => (data.macos[v]?.pageLoadTime / 1000)?.toFixed(2) || 0
  );
  const windowsPageLoad = UNITY_VERSIONS.map(
    (v) => (data.windows[v]?.pageLoadTime / 1000)?.toFixed(2) || 0
  );

  const config = {
    type: "bar",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "macOS",
          data: macosPageLoad,
          backgroundColor: "rgba(59, 130, 246, 0.8)",
        },
        {
          label: "Windows",
          data: windowsPageLoad,
          backgroundColor: "rgba(239, 68, 68, 0.8)",
        },
      ],
    },
    options: {
      title: { display: true, text: "Page Load Time by Unity Version (sec)" },
      scales: { yAxes: [{ ticks: { beginAtZero: true } }] },
    },
  };

  return generateQuickChartUrl(config);
}

/**
 * 숫자 포맷팅 (소수점 처리)
 */
function formatNumber(value, decimals = 1) {
  if (value === null || value === undefined || isNaN(value)) return "-";
  return Number(value).toFixed(decimals);
}

/**
 * 테스트 실패 여부 확인
 */
function hasAnyTestFailure(data) {
  for (const os of OS_LIST) {
    for (const version of UNITY_VERSIONS) {
      const result = data[os][version];
      if (result && result.testsPassed !== result.testsTotal) {
        return true;
      }
    }
  }
  return false;
}

/**
 * Test Summary 섹션 생성
 */
function generateTestSummary(data) {
  let md = "";
  md += "### 📈 Test Summary\n\n";
  md += "| Unity Version | macOS | Windows |\n";
  md += "|:--------------|:-----:|:-------:|\n";

  for (const version of UNITY_VERSIONS) {
    const macosResult = data.macos[version];
    const windowsResult = data.windows[version];

    const macosStatus = macosResult
      ? `${statusEmoji(macosResult.testsPassed === macosResult.testsTotal)} ${macosResult.testsPassed}/${macosResult.testsTotal}`
      : "⏳";
    const windowsStatus = windowsResult
      ? `${statusEmoji(windowsResult.testsPassed === windowsResult.testsTotal)} ${windowsResult.testsPassed}/${windowsResult.testsTotal}`
      : "⏳";

    md += `| ${version} | ${macosStatus} | ${windowsStatus} |\n`;
  }
  md += "\n";
  return md;
}

/**
 * 상세 리포트 섹션 생성 (차트, 테이블 등)
 */
function generateDetailedReport(data) {
  let md = "";

  // ===== 차트 섹션 =====
  md += "### 📊 Charts\n\n";
  md += `![Build Size Chart](${generateBuildSizeChart(data)})\n\n`;
  md += `![FPS Chart](${generateFpsChart(data)})\n\n`;
  md += `![Load Time Chart](${generateLoadTimeChart(data)})\n\n`;

  // ===== 빌드 크기 테이블 =====
  md += "### 📦 Build Size (MB)\n\n";
  md += "| Unity Version | macOS | Windows | Diff |\n";
  md += "|:--------------|------:|--------:|-----:|\n";

  for (const version of UNITY_VERSIONS) {
    const macosSize = data.macos[version]?.buildSize;
    const windowsSize = data.windows[version]?.buildSize;

    let diff = "-";
    if (macosSize != null && windowsSize != null) {
      const diffValue = windowsSize - macosSize;
      diff = (diffValue >= 0 ? "+" : "") + diffValue.toFixed(2);
    }

    md += `| ${version} | ${formatNumber(macosSize, 2)} | ${formatNumber(windowsSize, 2)} | ${diff} |\n`;
  }
  md += "\n";

  // ===== 로드 시간 테이블 =====
  md += "### ⏱️ Load Time (ms)\n\n";
  md += "| Unity Version | macOS Page | macOS Unity | Windows Page | Windows Unity |\n";
  md += "|:--------------|----------:|-----------:|-------------:|-------------:|\n";

  for (const version of UNITY_VERSIONS) {
    const m = data.macos[version];
    const w = data.windows[version];

    md += `| ${version} | ${formatNumber(m?.pageLoadTime, 0)} | ${formatNumber(m?.unityLoadTime, 0)} | ${formatNumber(w?.pageLoadTime, 0)} | ${formatNumber(w?.unityLoadTime, 0)} |\n`;
  }
  md += "\n";

  // ===== FPS 성능 테이블 =====
  md += "### ⚡ Performance (FPS)\n\n";
  md += "| Unity Version | macOS Avg | macOS Min | Windows Avg | Windows Min |\n";
  md += "|:--------------|----------:|----------:|------------:|------------:|\n";

  for (const version of UNITY_VERSIONS) {
    const m = data.macos[version]?.benchmarkData;
    const w = data.windows[version]?.benchmarkData;

    md += `| ${version} | ${formatNumber(m?.avgFps)} | ${formatNumber(m?.minFps)} | ${formatNumber(w?.avgFps)} | ${formatNumber(w?.minFps)} |\n`;
  }
  md += "\n";

  // ===== 프로그레스바 시각화 =====
  md += "### 🎯 Performance Overview\n\n";

  for (const os of OS_LIST) {
    const osEmoji = os === "macos" ? "🍎" : "🪟";
    const osName = os === "macos" ? "macOS" : "Windows";

    md += `#### ${osEmoji} ${osName}\n\n`;
    md += "| Version | Build Size | Avg FPS | Memory | Load Time |\n";
    md += "|:--------|:-----------|:--------|:-------|:----------|\n";

    for (const version of UNITY_VERSIONS) {
      const d = data[os][version];

      if (d) {
        const buildSize = d.buildSize;
        const avgFps = d.benchmarkData?.avgFps;
        const memoryMB = d.benchmarkData?.memoryUsageMB;
        const loadTime = d.pageLoadTime;

        const buildBar = `${progressBar(buildSize, THRESHOLDS.BUILD_SIZE_MB)} ${formatNumber(buildSize, 1)}MB`;
        const fpsBar = `${progressBar(avgFps, THRESHOLDS.MAX_FPS)} ${formatNumber(avgFps, 0)}`;
        const memBar = `${progressBar(memoryMB, THRESHOLDS.MAX_MEMORY_MB)} ${formatNumber(memoryMB, 0)}MB`;
        const loadBar = `${progressBar(loadTime, THRESHOLDS.MAX_LOAD_TIME_MS)} ${formatNumber(loadTime / 1000, 1)}s`;

        md += `| ${version} | ${buildBar} | ${fpsBar} | ${memBar} | ${loadBar} |\n`;
      } else {
        md += `| ${version} | ⏳ | ⏳ | ⏳ | ⏳ |\n`;
      }
    }
    md += "\n";
  }

  // ===== API 테스트 결과 =====
  md += "### 🔌 API Test Results\n\n";
  md += "| Unity Version | macOS | Windows |\n";
  md += "|:--------------|:-----:|:-------:|\n";

  for (const version of UNITY_VERSIONS) {
    const m = data.macos[version]?.apiTestResults;
    const w = data.windows[version]?.apiTestResults;

    // totalAPIs가 있으면 상세 표시, 없으면 unexpectedErrorCount만으로 판단
    const formatApiResult = (api) => {
      if (!api) return "⏳";
      if (api.totalAPIs != null && api.successCount != null) {
        return `${statusEmoji(api.unexpectedErrorCount === 0)} ${api.successCount}/${api.totalAPIs}`;
      }
      // totalAPIs가 없는 경우 (이전 버전 호환)
      return `${statusEmoji(api.unexpectedErrorCount === 0)} ${api.unexpectedErrorCount === 0 ? "Pass" : "Fail"}`;
    };

    md += `| ${version} | ${formatApiResult(m)} | ${formatApiResult(w)} |\n`;
  }
  md += "\n";

  // ===== WebGL 환경 정보 =====
  md += "### 🖥️ WebGL Environment\n\n";
  md += "| OS | Version | Renderer | Vendor |\n";
  md += "|:---|:--------|:---------|:-------|\n";

  for (const os of OS_LIST) {
    for (const version of UNITY_VERSIONS) {
      const d = data[os][version];
      if (d?.webgl) {
        const osName = os === "macos" ? "macOS" : "Windows";
        const renderer = d.webgl.renderer || "-";
        const vendor = d.webgl.vendor || "-";
        const shortRenderer =
          renderer.length > 40 ? renderer.substring(0, 40) + "..." : renderer;
        const shortVendor =
          vendor.length > 30 ? vendor.substring(0, 30) + "..." : vendor;
        md += `| ${osName} | ${version} | ${shortRenderer} | ${shortVendor} |\n`;
      }
    }
  }
  md += "\n";

  return md;
}

/**
 * 마크다운 리포트 생성
 */
function generateReport(data) {
  let md = "";

  // 헤더
  md += "## 📊 Benchmark Results\n\n";
  md += `> Generated: ${new Date().toISOString()}\n\n`;

  // 데이터 존재 여부 확인
  const hasData = OS_LIST.some((os) =>
    UNITY_VERSIONS.some((v) => data[os][v])
  );

  if (!hasData) {
    md += "⚠️ No benchmark results available\n";
    return md;
  }

  // 실패 여부 확인
  const hasFailure = hasAnyTestFailure(data);

  if (hasFailure) {
    // 실패 시: Test Summary는 펼쳐서 보여주고, 나머지는 접기
    md += generateTestSummary(data);
    md += "<details>\n<summary>📋 View detailed benchmark report</summary>\n\n";
    md += generateDetailedReport(data);
    md += "</details>\n";
  } else {
    // 성공 시: 전체를 접기
    md += "<details>\n<summary>✅ All tests passed - Click to view details</summary>\n\n";
    md += generateTestSummary(data);
    md += generateDetailedReport(data);
    md += "</details>\n";
  }

  return md;
}

// ===== 메인 실행 =====
console.log("Loading benchmark data from artifacts/...");
const data = loadBenchmarkData();

// 로드된 데이터 요약 출력
let loadedCount = 0;
for (const os of OS_LIST) {
  for (const version of UNITY_VERSIONS) {
    if (data[os][version]) {
      loadedCount++;
      console.log(`  ✓ ${os}-${version}`);
    }
  }
}
console.log(`Loaded ${loadedCount}/${OS_LIST.length * UNITY_VERSIONS.length} benchmark files`);

console.log("Generating report...");
const report = generateReport(data);

fs.writeFileSync("benchmark-report.md", report);
console.log("Report generated: benchmark-report.md");
