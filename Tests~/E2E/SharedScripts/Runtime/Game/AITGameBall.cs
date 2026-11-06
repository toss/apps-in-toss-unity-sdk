using UnityEngine;

/// <summary>공 오브젝트의 충돌 메시지를 AITGamePlay 로 넘긴다(런타임에 AddComponent).</summary>
public class AITGameBall : MonoBehaviour
{
    public AITGamePlay owner;

    private void OnCollisionEnter(Collision c)
    {
        if (owner != null) owner.OnBallCollision(c);
    }
}
