#!/bin/bash
# Apps in Toss 배포 스크립트
# 사용법: ./deploy.sh [unity-version]
# 예: ./deploy.sh 6000.2
# 인자 없이 실행하면 모든 버전 배포

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# .env 파일 로드
if [ -f .env ]; then
    export $(grep -v '^#' .env | xargs)
else
    echo "❌ .env 파일이 없습니다. AIT_DEPLOY_KEY를 설정해주세요."
    echo "예: echo 'AIT_DEPLOY_KEY=your_key' > Tests~/E2E/.env"
    exit 1
fi

# API key 확인
if [ -z "$AIT_DEPLOY_KEY" ]; then
    echo "❌ AIT_DEPLOY_KEY가 설정되지 않았습니다."
    exit 1
fi

deploy_project() {
    local project_dir="$1"
    local version="$(basename "$project_dir" | sed 's/SampleUnityProject-//')"

    if [ ! -d "$project_dir/ait-build/dist" ]; then
        echo "⏭️  [$version] ait-build/dist 없음 - 건너뜀"
        return 0
    fi

    echo "🚀 [$version] 배포 시작..."
    cd "$project_dir/ait-build"

    pnpm run deploy --api-key "$AIT_DEPLOY_KEY"

    echo "✅ [$version] 배포 완료!"
    cd "$SCRIPT_DIR"
}

if [ -n "$1" ]; then
    # 특정 버전만 배포
    PROJECT_DIR="$SCRIPT_DIR/SampleUnityProject-$1"
    if [ ! -d "$PROJECT_DIR" ]; then
        echo "❌ 프로젝트를 찾을 수 없습니다: $PROJECT_DIR"
        exit 1
    fi
    deploy_project "$PROJECT_DIR"
else
    # 모든 프로젝트 배포
    echo "📦 모든 Sample Project 배포 시작..."
    for project in "$SCRIPT_DIR"/SampleUnityProject-*/; do
        deploy_project "$project"
    done
    echo ""
    echo "🎉 모든 배포 완료!"
fi
