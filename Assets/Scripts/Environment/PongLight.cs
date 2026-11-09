using UnityEngine;

public class PongLight : MonoBehaviour
{
    [SerializeField] float kecepatan = 50f;
    [SerializeField] float jarak = 90f;

    Vector3 posisiAwal;

    void Start()
    {
        posisiAwal = transform.localPosition;
    }

    void Update()
    {
        // Nilai -1 sampai 1
        float gerak = Mathf.PingPong(Time.time * kecepatan / 90f, 2f) - 1f;

        // Ambil arah Y dari rotasi object
        Vector3 arahY = transform.localRotation * Vector3.up;

        // Gerakkan object sepanjang sumbu Y yang sudah diputar
        transform.localPosition = posisiAwal + arahY * gerak * jarak;
    }
}