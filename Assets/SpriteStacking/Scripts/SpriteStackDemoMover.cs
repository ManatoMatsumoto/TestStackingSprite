using UnityEngine;

/// <summary>
/// デモ用: オブジェクトを円周上で走らせる、またはその場で回転させる。
/// モデルの前方はスライス画像の上方向（ローカル +Y）とする。
/// </summary>
public class SpriteStackDemoMover : MonoBehaviour
{
    public enum Mode { Circle, Spin }

    public Mode mode = Mode.Circle;
    public Vector2 center;
    public float radius = 3.5f;
    [Tooltip("度/秒。正で反時計回り")]
    public float speed = 40f;
    public float angle;

    void Update()
    {
        angle += speed * Time.deltaTime;
        if (mode == Mode.Spin)
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            return;
        }

        float r = angle * Mathf.Deg2Rad;
        var p = center + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * radius;
        transform.position = new Vector3(p.x, p.y, transform.position.z);
        // 進行方向（接線）に +Y を向ける
        transform.rotation = Quaternion.Euler(0f, 0f, speed >= 0f ? angle : angle - 180f);
    }
}
