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
// Windows E2E 테스트가 제거되어 macOS만 리포트
const OS_LIST = ["macos"];

// OS별 표시 이름 (테스트 환경 표시)
const OS_DISPLAY_NAMES = {
  macos: "macOS (Mobile Emulation)",
};
const OS_SHORT_NAMES = {
  macos: "macOS",
};

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
 * FPS 성능 비교 라인 차트 URL 생성 (Baseline, Physics+Memory, Rendering+Memory, Full Load)
 * 새로운 통합 성능 테스트 데이터 구조 지원
 */
function generateFpsChart(data) {
  // 새로운 comprehensivePerfData 구조 우선 사용
  const baselineFps = UNITY_VERSIONS.map((v) => {
    const perf = data.macos[v]?.comprehensivePerfData;
    return perf?.baseline?.avgFps?.toFixed(1) || data.macos[v]?.benchmarkData?.avgFps?.toFixed(1) || 0;
  });
  const physicsFps = UNITY_VERSIONS.map((v) => {
    const perf = data.macos[v]?.comprehensivePerfData;
    return perf?.physicsWithMemory?.avgFps?.toFixed(1) || data.macos[v]?.benchmarkData?.physicsAvgFps?.toFixed(1) || 0;
  });
  const renderingFps = UNITY_VERSIONS.map((v) => {
    const perf = data.macos[v]?.comprehensivePerfData;
    return perf?.renderingWithMemory?.avgFps?.toFixed(1) || data.macos[v]?.benchmarkData?.renderingAvgFps?.toFixed(1) || 0;
  });
  const fullLoadFps = UNITY_VERSIONS.map((v) => {
    const perf = data.macos[v]?.comprehensivePerfData;
    return perf?.fullLoad?.avgFps?.toFixed(1) || data.macos[v]?.benchmarkData?.combinedAvgFps?.toFixed(1) || 0;
  });

  const config = {
    type: "line",
    data: {
      labels: UNITY_VERSIONS,
      datasets: [
        {
          label: "Baseline",
          data: baselineFps,
          borderColor: "rgba(34, 197, 94, 1)",
          backgroundColor: "rgba(34, 197, 94, 0.1)",
          fill: false,
        },
        {
          label: "Physics+Memory",
          data: physicsFps,
          borderColor: "rgba(59, 130, 246, 1)",
          backgroundColor: "rgba(59, 130, 246, 0.1)",
          fill: false,
        },
        {
          label: "Rendering+Memory",
          data: renderingFps,
          borderColor: "rgba(168, 85, 247, 1)",
          backgroundColor: "rgba(168, 85, 247, 0.1)",
          fill: false,
        },
        {
          label: "Full Load",
          data: fullLoadFps,
          borderColor: "rgba(239, 68, 68, 1)",
          backgroundColor: "rgba(239, 68, 68, 0.1)",
          fill: false,
        },
      ],
    },
    options: {
      title: { display: true, text: "Comprehensive Performance FPS by Unity Version" },
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
  md += `| Unity Version | Tests | Build Size | Full Load FPS | Allocated (MB) | OOM |\n`;
  md += "|:--------------|:-----:|:----------:|:-------------:|:--------------:|:---:|\n";

  for (const version of UNITY_VERSIONS) {
    const result = data.macos[version];

    const testStatus = result
      ? `${statusEmoji(result.testsPassed === result.testsTotal)} ${result.testsPassed}/${result.testsTotal}`
      : "⏳";
    const buildSize = result?.buildSize ? `${result.buildSize.toFixed(1)} MB` : "-";

    // 새로운 comprehensivePerfData 구조 우선 사용
    const perf = result?.comprehensivePerfData;
    const fullLoadFps = perf?.fullLoad?.avgFps
      ? `${perf.fullLoad.avgFps.toFixed(0)} FPS`
      : result?.benchmarkData?.combinedAvgFps
        ? `${result.benchmarkData.combinedAvgFps.toFixed(0)} FPS`
        : "-";

    // WASM + JS 할당량 합계
    const fullLoad = perf?.fullLoad;
    const totalAllocatedMB = (fullLoad?.wasmAllocatedMB || 0) + (fullLoad?.jsAllocatedMB || 0) + (fullLoad?.canvasEstimatedMB || 0);
    const allocatedStr = totalAllocatedMB > 0 ? `${totalAllocatedMB.toFixed(0)}` : "-";

    const oomStatus = perf?.oomOccurred !== undefined
      ? (perf.oomOccurred ? "❌" : "✅")
      : "-";

    md += `| ${version} | ${testStatus} | ${buildSize} | ${fullLoadFps} | ${allocatedStr} | ${oomStatus} |\n`;
  }
  md += "\n";
  return md;
}

/**
 * 메모리 압박 테스트 차트 URL 생성
 */
function generateMemoryPressureChart(data) {
  // 각 Unity 버전별 메모리 압박 테스트 결과에서 단계별 FPS 추출
  const datasets = [];
  const colors = [
    "rgba(34, 197, 94, 1)",   // green
    "rgba(59, 130, 246, 1)",  // blue
    "rgba(168, 85, 247, 1)",  // purple
    "rgba(239, 68, 68, 1)",   // red
    "rgba(245, 158, 11, 1)",  // amber
  ];

  // 첫 번째 데이터에서 스텝 이름 추출
  let stepLabels = [];
  for (const version of UNITY_VERSIONS) {
    const memPressure = data.macos[version]?.memoryPressureData;
    if (memPressure?.steps?.length > 0) {
      stepLabels = memPressure.steps.map(s => s.stepName);
      break;
    }
  }

  if (stepLabels.length === 0) {
    return null; // 데이터 없음
  }

  UNITY_VERSIONS.forEach((version, idx) => {
    const memPressure = data.macos[version]?.memoryPressureData;
    if (memPressure?.steps) {
      datasets.push({
        label: `Unity ${version}`,
        data: memPressure.steps.map(s => s.avgFps?.toFixed(1) || 0),
        borderColor: colors[idx % colors.length],
        fill: false,
      });
    }
  });

  if (datasets.length === 0) return null;

  const config = {
    type: "line",
    data: {
      labels: stepLabels,
      datasets: datasets,
    },
    options: {
      title: { display: true, text: "Memory Pressure Test - FPS by Step" },
      scales: { yAxes: [{ ticks: { beginAtZero: true } }] },
    },
  };

  return generateQuickChartUrl(config);
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

  // 메모리 압박 테스트 차트 (데이터가 있는 경우에만)
  const memPressureChart = generateMemoryPressureChart(data);
  if (memPressureChart) {
    md += `![Memory Pressure Chart](${memPressureChart})\n\n`;
  }

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

  // ===== 종합 성능 FPS 상세 테이블 =====
  md += "### ⚡ Comprehensive Performance FPS Detail\n\n";
  md += `| Unity Version | Baseline | Physics+Mem | Rendering+Mem | Full Load | Min FPS |\n`;
  md += "|:--------------|:--------:|:-----------:|:-------------:|:---------:|:-------:|\n";

  for (const version of UNITY_VERSIONS) {
    // 새로운 comprehensivePerfData 구조 우선 사용
    const perf = data.macos[version]?.comprehensivePerfData;
    const oldBench = data.macos[version]?.benchmarkData;

    const baseline = perf?.baseline?.avgFps ?? oldBench?.avgFps;
    const physics = perf?.physicsWithMemory?.avgFps ?? oldBench?.physicsAvgFps;
    const rendering = perf?.renderingWithMemory?.avgFps ?? oldBench?.renderingAvgFps;
    const fullLoad = perf?.fullLoad?.avgFps ?? oldBench?.combinedAvgFps;
    const minFps = perf?.fullLoad?.minFps ?? oldBench?.minFps;

    md += `| ${version} | ${formatNumber(baseline)} | ${formatNumber(physics)} | ${formatNumber(rendering)} | ${formatNumber(fullLoad)} | ${formatNumber(minFps)} |\n`;
  }
  md += "\n";

  // ===== 프로그레스바 시각화 (macOS만) =====
  md += "### 🎯 Performance Overview\n\n";
  md += "| Version | Build Size | Baseline FPS | Full Load FPS | Load Time |\n";
  md += "|:--------|:-----------|:-------------|:--------------|:----------|\n";

  for (const version of UNITY_VERSIONS) {
    const d = data.macos[version];

    if (d) {
      const buildSize = d.buildSize;
      // 새로운 comprehensivePerfData 구조 우선 사용
      const perf = d.comprehensivePerfData;
      const oldBench = d.benchmarkData;
      const baselineFps = perf?.baseline?.avgFps ?? oldBench?.avgFps;
      const fullLoadFps = perf?.fullLoad?.avgFps ?? oldBench?.combinedAvgFps;
      const loadTime = d.pageLoadTime;

      const buildBar = `${progressBar(buildSize, THRESHOLDS.BUILD_SIZE_MB)} ${formatNumber(buildSize, 1)}MB`;
      const baselineBar = `${progressBar(baselineFps, THRESHOLDS.MAX_FPS)} ${formatNumber(baselineFps, 0)}`;
      const fullLoadBar = `${progressBar(fullLoadFps, THRESHOLDS.MAX_FPS)} ${formatNumber(fullLoadFps, 0)}`;
      const loadBar = `${progressBar(loadTime, THRESHOLDS.MAX_LOAD_TIME_MS)} ${formatNumber(loadTime / 1000, 1)}s`;

      md += `| ${version} | ${buildBar} | ${baselineBar} | ${fullLoadBar} | ${loadBar} |\n`;
    } else {
      md += `| ${version} | ⏳ | ⏳ | ⏳ | ⏳ |\n`;
    }
  }
  md += "\n";

  // ===== 메모리 압박 + 종합 성능 테스트 결과 =====
  md += "### 🧠 Memory & Load Test Results\n\n";

  // 새로운 comprehensivePerfData 또는 기존 memoryPressureData 확인
  let hasPerfData = false;
  let hasLegacyMemoryData = false;
  for (const version of UNITY_VERSIONS) {
    if (data.macos[version]?.comprehensivePerfData) {
      hasPerfData = true;
      break;
    }
    if (data.macos[version]?.memoryPressureData) {
      hasLegacyMemoryData = true;
    }
  }

  if (hasPerfData) {
    // 새로운 종합 성능 테스트 결과 표시
    md += `| Unity Version | OOM | Full Load FPS | WASM (MB) | JS (MB) | Canvas (MB) |\n`;
    md += "|:--------------|:---:|:-------------:|:---------:|:-------:|:-----------:|\n";

    for (const version of UNITY_VERSIONS) {
      const perf = data.macos[version]?.comprehensivePerfData;
      if (perf) {
        const oomStatus = perf.oomOccurred ? "❌" : "✅";
        const fullLoad = perf.fullLoad;
        md += `| ${version} | ${oomStatus} | ${formatNumber(fullLoad?.avgFps)} | ${formatNumber(fullLoad?.wasmAllocatedMB)} | ${formatNumber(fullLoad?.jsAllocatedMB)} | ${formatNumber(fullLoad?.canvasEstimatedMB)} |\n`;
      } else {
        md += `| ${version} | ⏳ | - | - | - | - |\n`;
      }
    }
    md += "\n";
  } else if (hasLegacyMemoryData) {
    // 기존 memoryPressureData 형식 표시 (하위 호환성)
    md += `| Unity Version | OOM | Combined Avg FPS | Combined Min FPS | Steps |\n`;
    md += "|:--------------|:---:|:----------------:|:----------------:|:-----:|\n";

    for (const version of UNITY_VERSIONS) {
      const mp = data.macos[version]?.memoryPressureData;
      if (mp) {
        const oomStatus = mp.oomOccurred ? "❌" : "✅";
        md += `| ${version} | ${oomStatus} | ${formatNumber(mp.combinedPressureAvgFps)} | ${formatNumber(mp.combinedPressureMinFps)} | ${mp.totalSteps || 0} |\n`;
      } else {
        md += `| ${version} | ⏳ | - | - | - |\n`;
      }
    }
    md += "\n";

    // 메모리 압박 단계별 상세 (첫 번째 버전만 예시로 표시)
    for (const version of UNITY_VERSIONS) {
      const mp = data.macos[version]?.memoryPressureData;
      if (mp?.steps?.length > 0) {
        md += `<details>\n<summary>📊 Memory Pressure Steps (Unity ${version})</summary>\n\n`;
        md += `| Step | Category | Avg FPS | Min FPS | Max FPS |\n`;
        md += `|:-----|:---------|:-------:|:-------:|:-------:|\n`;
        for (const step of mp.steps) {
          md += `| ${step.stepName} | ${step.category} | ${formatNumber(step.avgFps)} | ${formatNumber(step.minFps)} | ${formatNumber(step.maxFps)} |\n`;
        }
        md += `\n</details>\n\n`;
        break; // 하나만 표시
      }
    }
  } else {
    md += "> ⏳ Memory/load test data not available\n\n";
  }

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
