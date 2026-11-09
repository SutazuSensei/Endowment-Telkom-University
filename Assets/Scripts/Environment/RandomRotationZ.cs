using UnityEngine;

public class RandomRotationZ : MonoBehaviour
{
    [SerializeField] float kecepatan = 2f;
    [SerializeField] float minZ = -60f;
    [SerializeField] float maxZ = 60f;

    float targetZ;

    void Start()
    {
        targetZ = Random.Range(minZ, maxZ);
    }

    void Update()
    {
        float z = Mathf.MoveTowardsAngle(
            transform.eulerAngles.z,
            targetZ,
            kecepatan * Time.deltaTime
        );

        transform.rotation = Quaternion.Euler(
            transform.eulerAngles.x,
            transform.eulerAngles.y,
            z
        );

        // Kalau sudah mendekati target, pilih sudut acak baru
        if (Mathf.Abs(Mathf.DeltaAngle(z, targetZ)) < 0.5f)
        {
            targetZ = Random.Range(minZ, maxZ);
        }
    }
}