// -----------------------------------------------------------------------
// <copyright file="AIT.ContactsViral.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - ContactsViral API
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace AppsInToss
{
    /// <summary>
    /// Apps in Toss Platform API - ContactsViral
    /// </summary>
    public static partial class AIT
    {
        /// <param name="paramsParam">연락처 공유 기능을 실행할 때 사용하는 파라미터예요. 옵션 설정과 이벤트 핸들러를 포함해요. 자세한 내용은 [ContactsViralParams](/bedrock/reference/native-modules/친구초대/ContactsViralParams.html) 문서를 참고하세요.</param>
        /// <returns>앱브릿지 cleanup 함수를 반환해요. 공유 기능이 끝나면 반드시 이 함수를 호출해서 리소스를 해제해야 해요.</returns>
        public static System.Action ContactsViral(ContactsViralParams paramsParam)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return __contactsViral_Internal(paramsParam);
#else
            // Unity Editor mock implementation
            UnityEngine.Debug.Log($"[AIT Mock] ContactsViral called");
            return () => { };
#endif
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern System.Action __contactsViral_Internal(ContactsViralParams paramsParam);
#endif
    }
}
