# Tools~/NodeJS

이 폴더는 SDK에서 자동으로 관리되는 **Embedded Node.js 런타임**을 저장합니다.

## 개요

Apps in Toss Unity SDK는 빌드 파이프라인에서 `npm`, `granite` 등의 Node.js 기반 도구를 사용합니다. 사용자가 시스템에 Node.js를 설치하지 않아도 SDK가 동작할 수 있도록, portable Node.js 런타임을 자동으로 다운로드하여 이 폴더에 저장합니다.

## 폴더 구조

```
Tools~/NodeJS/
├── darwin-arm64/       # macOS Apple Silicon (M1/M2/M3)
│   ├── bin/
│   │   ├── node
│   │   ├── npm
│   │   └── npx
│   └── ...
├── darwin-x64/         # macOS Intel
│   ├── bin/
│   │   ├── node
│   │   ├── npm
│   │   └── npx
│   └── ...
├── win-x64/            # Windows 64-bit
│   ├── node.exe
│   ├── npm.cmd
│   └── ...
└── linux-x64/          # Linux 64-bit
    ├── bin/
    │   ├── node
    │   ├── npm
    │   └── npx
    └── ...
```

## 자동 다운로드

다음 상황에서 Node.js가 자동으로 다운로드됩니다:

1. **첫 빌드 시**: 시스템에 Node.js가 설치되어 있지 않은 경우
2. **사용자 선택**: "Node.js 자동 다운로드" 다이얼로그에서 "다운로드" 선택
3. **다운로드 소스** (폴백):
   - 1순위: https://nodejs.org (공식 사이트)
   - 2순위: https://cdn.npmmirror.com (npmmirror CDN)
   - 3순위: https://repo.huaweicloud.com (Huawei Mirror)

### 보안: SHA256 체크섬 검증 (필수)

**모든 다운로드는 SHA256 체크섬으로 검증됩니다.**

다운로드한 파일의 무결성을 보장하기 위해, SDK는 Node.js 공식 사이트의 체크섬과 비교 검증합니다:

- **체크섬 출처**: https://nodejs.org/dist/v24.11.1/SHASUMS256.txt
- **검증 방식**: 다운로드 완료 후 SHA256 해시 계산 → 공식 체크섬과 비교
- **불일치 시 동작**:
  - 다운로드한 파일 즉시 삭제
  - 보안 경고 메시지 표시
  - 빌드 중단

이를 통해 중간자 공격(MITM), 파일 변조, 다운로드 손상 등의 보안 위협으로부터 사용자를 보호합니다.

**체크섬 값** (`Editor/AITNodeJSDownloader.cs`에 하드코딩):
```
darwin-arm64: b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35
darwin-x64:   096081b6d6fcdd3f5ba0f5f1d44a47e83037ad2e78eada26671c252fe64dd111
win-x64:      5355ae6d7c49eddcfde7d34ac3486820600a831bf81dc3bdca5c8db6a9bb0e76
linux-x64:    60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8
```

## Node.js 버전

- **LTS 버전**: v24.11.1 (2025-11-19 기준)
- 버전은 `Editor/AITNodeJSDownloader.cs`의 `NODE_VERSION` 상수에서 관리됩니다.

## Git 관리

이 폴더의 플랫폼별 런타임은 `.gitignore`에 등록되어 있어 Git에 커밋되지 않습니다:

```gitignore
Tools~/NodeJS/darwin-arm64/
Tools~/NodeJS/darwin-x64/
Tools~/NodeJS/win-x64/
Tools~/NodeJS/linux-x64/
```

**이유**:
- 각 플랫폼별 런타임은 40-50MB로 크기가 큽니다
- 사용자 환경에 맞는 플랫폼만 다운로드하여 디스크 공간을 절약합니다
- SDK 업데이트 시 자동으로 필요한 버전을 다운로드합니다

## 우선순위

SDK는 다음 우선순위로 npm을 찾습니다:

1. **시스템 설치 Node.js** (가장 우선)
   - `/usr/local/bin/npm`
   - `/opt/homebrew/bin/npm`
   - `/usr/bin/npm`
   - `which npm` 결과

2. **Embedded Node.js** (폴백)
   - `Tools~/NodeJS/{platform}/bin/npm`
   - 없으면 자동 다운로드

이 방식으로 시스템 Node.js가 있으면 이를 우선 사용하고, 없으면 자동으로 portable 버전을 다운로드하여 사용합니다.

## 수동 삭제

Embedded Node.js를 삭제하고 싶다면:

```bash
# macOS/Linux
rm -rf Tools~/NodeJS/darwin-arm64
rm -rf Tools~/NodeJS/darwin-x64
rm -rf Tools~/NodeJS/linux-x64

# Windows (PowerShell)
Remove-Item -Recurse -Force Tools~/NodeJS/win-x64
```

다음 빌드 시 필요하면 자동으로 다시 다운로드됩니다.

## 문제 해결

### 다운로드 실패
- 인터넷 연결 확인
- 방화벽/프록시 설정 확인
- 수동으로 Node.js 설치: https://nodejs.org

### 권한 오류 (macOS/Linux)
```bash
chmod +x Tools~/NodeJS/darwin-arm64/bin/node
chmod +x Tools~/NodeJS/darwin-arm64/bin/npm
```

### 버전 확인
```bash
# macOS Apple Silicon
Tools~/NodeJS/darwin-arm64/bin/node --version
# v24.11.1

# Windows
Tools~/NodeJS/win-x64/node.exe --version
```

## 라이센스

Node.js는 MIT 라이센스로 배포되며, 재배포가 허용됩니다.
- Node.js 공식 사이트: https://nodejs.org
- 라이센스: https://github.com/nodejs/node/blob/main/LICENSE
