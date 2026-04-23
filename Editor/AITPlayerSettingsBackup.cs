using System;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 호환용 얇은 래퍼. 실제 구현은 PlayerSettingsSnapshot 로 이전됨.
    /// 신규 호출부는 PlayerSettingsSnapshot 를 직접 사용.
    /// Task 5에서 AITConvertCore 호출을 교체한 뒤 Task N에서 이 파일을 제거한다.
    /// </summary>
    [Obsolete("Use PlayerSettingsSnapshot in AITBuildSession.cs")]
    internal struct AITPlayerSettingsBackup
    {
        private PlayerSettingsSnapshot inner;
        public static AITPlayerSettingsBackup Capture()
            => new AITPlayerSettingsBackup { inner = PlayerSettingsSnapshot.Capture() };
        public void Restore() => inner.Restore();
    }
}
