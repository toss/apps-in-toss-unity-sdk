#!/bin/bash
# C# 코드 컴파일 검증 스크립트 (.NET SDK 사용)
# Unity 없이도 빠르게 문법 오류를 확인할 수 있습니다

set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
SDK_DIR="$SCRIPT_DIR/../../Runtime/SDK"
TEMP_PROJECT="$SCRIPT_DIR/.temp-csharp-validation"

echo "🔍 C# 코드 컴파일 검증 시작"
echo "   SDK 경로: $SDK_DIR"
echo ""

# 임시 프로젝트 디렉토리 정리
rm -rf "$TEMP_PROJECT"
mkdir -p "$TEMP_PROJECT"

# .NET 콘솔 프로젝트 생성
cd "$TEMP_PROJECT"
dotnet new console -n CSharpValidation --force > /dev/null 2>&1

cd CSharpValidation

# Unity 관련 타입들을 모킹할 수 있도록 간단한 파일 추가
cat > UnityMock.cs << 'EOF'
// Unity API 모킹 (컴파일 검증용)
namespace UnityEngine
{
    public class Object
    {
        public static void DontDestroyOnLoad(object obj) { }
    }

    public class MonoBehaviour : Object { }

    public class GameObject
    {
        public GameObject(string name) { }
        public T AddComponent<T>() where T : new() => new T();
    }

    public static class Debug
    {
        public static void Log(object message) { }
        public static void LogError(object message) { }
        public static void LogWarning(object message) { }
    }

    public static class JsonUtility
    {
        public static T FromJson<T>(string json) => default(T)!;
        public static string ToJson(object obj) => "";
    }
}
EOF

# SDK 파일들을 복사
echo "📦 SDK 파일 복사 중..."
cp "$SDK_DIR"/*.cs . 2>/dev/null || true

# C# 컴파일
echo "🔨 C# 컴파일 중..."
dotnet build --verbosity quiet

if [ $? -eq 0 ]; then
    echo ""
    echo "✅ C# 컴파일 성공!"
    echo "   생성된 SDK 코드에 문법 오류가 없습니다."

    # 임시 파일 정리
    cd "$SCRIPT_DIR"
    rm -rf "$TEMP_PROJECT"

    exit 0
else
    echo ""
    echo "❌ C# 컴파일 실패"
    echo "   생성된 코드에 문법 오류가 있습니다."
    echo ""
    echo "   상세 로그:"
    dotnet build

    exit 1
fi
