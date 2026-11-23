using UnityEngine.Scripting;

// IL2CPP 링커는 루트 어셈블리(게임 코드)가 참조하지 않는 어셈블리를 처리하지 않고 통째로 지운다.
// 게임은 보통 AppsInTossSDK 의 API 만 부르고 이 어셈블리의 타입은 직접 참조하지 않으므로, 이 속성이 없으면
// [RuntimeInitializeOnLoadMethod] 로만 진입하는 자동 훅(VisibilityHelper, PerformanceLogger 등)이 빌드에서 빠진다.
// RuntimeInitializeOnLoads.json 에는 훅이 등록된 채로 남아 오류 없이 조용히 돌지 않는다.
[assembly: AlwaysLinkAssembly]
