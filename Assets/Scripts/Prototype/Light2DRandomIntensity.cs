using UnityEngine;

using UnityEngine.Rendering.Universal;

public class Light2DRandomIntensity : MonoBehaviour

{

    [SerializeField] float intensityMin = 0.1f;

    [SerializeField] float intensityMax = 1f;

    [SerializeField] float kecepatan = 0.5f;

    Light2D light2D;

    float targetIntensity;

    void Start()

    {

        light2D = GetComponent<Light2D>();

        // Mulai dari intensity random

        light2D.intensity = Random.Range(

            intensityMin,

            intensityMax

        );

        // Tentukan target random pertama

        targetIntensity = Random.Range(

            intensityMin,

            intensityMax

        );

    }

    void Update()

    {

        // Bergerak smooth menuju target

        light2D.intensity = Mathf.MoveTowards(

            light2D.intensity,

            targetIntensity,

            kecepatan * Time.deltaTime

        );

        // Kalau sudah sampai target,

        // buat target random baru

        if (Mathf.Approximately(

            light2D.intensity,

            targetIntensity))

        {

            targetIntensity = Random.Range(

                intensityMin,

                intensityMax

            );

        }

    }

}