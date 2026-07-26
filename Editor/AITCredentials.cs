using UnityEngine;
using UnityEditor;
using System.IO;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss 민감한 인증 정보 저장 오브젝트
    /// 이 파일은 .gitignore에 추가되어야 하며, Git에 커밋되면 안 됩니다.
    /// </summary>
    [System.Serializable]
    public class AITCredentials : ScriptableObject
    {
        [Header("배포 설정")]
        [Tooltip("Apps in Toss 콘솔에서 발급받은 배포 키")]
        public string deploymentKey = "";

        /// <summary>
        /// 배포 키가 유효한지 확인
        /// </summary>
        public bool IsDeploymentKeyValid()
        {
            return !string.IsNullOrWhiteSpace(deploymentKey);
        }
    }

    /// <summary>
    /// AITCredentials 유틸리티 클래스
    /// </summary>
    public static class AITCredentialsUtil
    {
        private const string CREDENTIALS_PATH = "Assets/AppsInToss/Editor/AITCredentials.asset";

        /// <summary>
        /// AITCredentials 인스턴스를 가져옵니다. 없으면 생성합니다.
        /// </summary>
        public static AITCredentials GetCredentials()
        {
            var credentials = AssetDatabase.LoadAssetAtPath<AITCredentials>(CREDENTIALS_PATH);

            if (credentials == null)
            {
                credentials = CreateCredentials();
            }

            return credentials;
        }

        /// <summary>
        /// AITCredentials 파일을 생성합니다.
        /// </summary>
        private static AITCredentials CreateCredentials()
        {
            // 디렉토리 확인 및 생성
            string directory = Path.GetDirectoryName(CREDENTIALS_PATH);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var credentials = ScriptableObject.CreateInstance<AITCredentials>();
            AssetDatabase.CreateAsset(credentials, CREDENTIALS_PATH);
            AssetDatabase.SaveAssets();

            Debug.Log($"[AIT] AITCredentials.asset 파일이 생성되었습니다: {CREDENTIALS_PATH}");

            return credentials;
        }

        /// <summary>
        /// 배포 키를 가져옵니다.
        /// </summary>
        public static string GetDeploymentKey()
        {
            var credentials = GetCredentials();
            return credentials?.deploymentKey ?? "";
        }

        /// <summary>
        /// 배포 준비가 완료되었는지 확인합니다.
        /// </summary>
        public static bool IsReadyForDeploy()
        {
            var credentials = GetCredentials();
            return credentials != null && credentials.IsDeploymentKeyValid();
        }
    }
}
