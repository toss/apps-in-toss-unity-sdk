#!/usr/bin/env node
/**
 * 벤치마크 결과 시각화 리포트 생성 스크립트
 *
 * macOS × 5개 Unity 버전의 벤치마크 데이터를 파싱하여
 * 마크다운 테이블, 프로그레스바, 차트 이미지를 생성합니다.
 */

import fs from "fs";
import path from "path";

const UNITY_VERSIONS = ["2021.3", "2022.3", "6000.0", "6000.2", "6000.3"];
const OS_LIST = ["macos"];

// 벤치마크 기준값
const THRESHOLDS = {
  BUILD_SIZE_MB: 50,
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

  const config = {
    type: "bar",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "Build Size (MB)",
          data: macosData,
          backgroundColor: "rgba(59, 130, 246, 0.8)",
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
 * 로드 시간 비교 차트 URL 생성
 */
function generateLoadTimeChart(data) {
  const pageLoadTime = UNITY_VERSIONS.map(
    (v) => (data.macos[v]?.pageLoadTime / 1000)?.toFixed(2) || 0
  );
  const unityLoadTime = UNITY_VERSIONS.map(
    (v) => (data.macos[v]?.unityLoadTime / 1000)?.toFixed(2) || 0
  );

  const config = {
    type: "bar",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "Page Load (sec)",
          data: pageLoadTime,
          backgroundColor: "rgba(59, 130, 246, 0.8)",
        },
        {
          label: "Unity Init (sec)",
          data: unityLoadTime,
          backgroundColor: "rgba(168, 85, 247, 0.8)",
        },
      ],
    },
    options: {
      title: { display: true, text: "Load Time by Unity Version (sec)" },
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
  md += `| Unity Version | Tests | Build Size | APIs |\n`;
  md += "|:--------------|:-----:|:----------:|:----:|\n";

  for (const version of UNITY_VERSIONS) {
    const result = data.macos[version];

    const testStatus = result
      ? `${statusEmoji(result.testsPassed === result.testsTotal)} ${result.testsPassed}/${result.testsTotal}`
      : "⏳";
    const buildSize = result?.buildSize ? `${result.buildSize.toFixed(1)} MB` : "-";

    const api = result?.apiTestResults;
    const apiStatus = api
      ? `${statusEmoji(api.unexpectedErrorCount === 0)} ${api.successCount}/${api.totalAPIs}`
      : "-";

    md += `| ${version} | ${testStatus} | ${buildSize} | ${apiStatus} |\n`;
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
  md += `![Load Time Chart](${generateLoadTimeChart(data)})\n\n`;

  // ===== 빌드 크기 테이블 =====
  md += "### 📦 Build Size\n\n";
  md += `| Unity Version | Build Size (MB) | Status |\n`;
  md += "|:--------------|----------------:|:------:|\n";

  for (const version of UNITY_VERSIONS) {
    const macosSize = data.macos[version]?.buildSize;
    const status = macosSize != null
      ? warningEmoji(macosSize, THRESHOLDS.BUILD_SIZE_MB, true)
      : "⏳";

    md += `| ${version} | ${formatNumber(macosSize, 2)} | ${status} |\n`;
  }
  md += "\n";

  // ===== 로드 시간 테이블 =====
  md += "### ⏱️ Load Time\n\n";
  md += `| Unity Version | Page Load (ms) | Unity Init (ms) | Total (sec) |\n`;
  md += "|:--------------|---------------:|----------------:|------------:|\n";

  for (const version of UNITY_VERSIONS) {
    const m = data.macos[version];
    const total = m?.pageLoadTime ? (m.pageLoadTime / 1000).toFixed(2) : "-";

    md += `| ${version} | ${formatNumber(m?.pageLoadTime, 0)} | ${formatNumber(m?.unityLoadTime, 0)} | ${total} |\n`;
  }
  md += "\n";

  // ===== 프로그레스바 시각화 =====
  md += "### 🎯 Performance Overview\n\n";
  md += "| Version | Build Size | Load Time |\n";
  md += "|:--------|:-----------|:----------|\n";

  for (const version of UNITY_VERSIONS) {
    const d = data.macos[version];

    if (d) {
      const buildSize = d.buildSize;
      const loadTime = d.pageLoadTime;

      const buildBar = `${progressBar(buildSize, THRESHOLDS.BUILD_SIZE_MB)} ${formatNumber(buildSize, 1)}MB`;
      const loadBar = `${progressBar(loadTime, THRESHOLDS.MAX_LOAD_TIME_MS)} ${formatNumber(loadTime / 1000, 1)}s`;

      md += `| ${version} | ${buildBar} | ${loadBar} |\n`;
    } else {
      md += `| ${version} | ⏳ | ⏳ |\n`;
    }
  }
  md += "\n";

  // ===== API 테스트 결과 =====
  md += "### 🔌 API Test Results\n\n";
  md += `| Unity Version | Status | APIs Tested |\n`;
  md += "|:--------------|:------:|:-----------:|\n";

  for (const version of UNITY_VERSIONS) {
    const m = data.macos[version]?.apiTestResults;

    const formatApiResult = (api) => {
      if (!api) return { status: "⏳", count: "-" };
      if (api.totalAPIs != null && api.successCount != null) {
        return {
          status: statusEmoji(api.unexpectedErrorCount === 0),
          count: `${api.successCount}/${api.totalAPIs}`,
        };
      }
      return {
        status: statusEmoji(api.unexpectedErrorCount === 0),
        count: api.unexpectedErrorCount === 0 ? "Pass" : "Fail",
      };
    };

    const result = formatApiResult(m);
    md += `| ${version} | ${result.status} | ${result.count} |\n`;
  }
  md += "\n";

  // ===== WebGL 환경 정보 =====
  md += "### 🖥️ WebGL Environment\n\n";
  md += "| Version | Renderer | Vendor |\n";
  md += "|:--------|:---------|:-------|\n";

  for (const version of UNITY_VERSIONS) {
    const d = data.macos[version];
    if (d?.webgl) {
      const renderer = d.webgl.renderer || "-";
      const vendor = d.webgl.vendor || "-";
      const shortRenderer =
        renderer.length > 50 ? renderer.substring(0, 50) + "..." : renderer;
      const shortVendor =
        vendor.length > 30 ? vendor.substring(0, 30) + "..." : vendor;
      md += `| ${version} | ${shortRenderer} | ${shortVendor} |\n`;
    } else {
      md += `| ${version} | - | - |\n`;
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
