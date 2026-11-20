Shader "Endowment/FishTierLit"
{
    Properties
    {
        [Header(Base Texture and Mask)]
        _fish_texture ("Fish Texture", 2D) = "white" {}
        _fish_mask ("Fish Mask", 2D) = "white" {}
        _FishColor ("Fish Color", Color) = (0.15, 0.45, 0.95, 1)
        _Cutoff ("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _BaseColor ("Base Color (Compat)", Color) = (1, 1, 1, 1)

        [Header(Surface PBR)]
        _Smoothness ("Smoothness", Range(0.0, 1.0)) = 0.5
        _Metallic ("Metallic", Range(0.0, 1.0)) = 0.0

        [Header(Tier Fresnel Rim Glow)]
        _RimColor ("Rim Glow Color", Color) = (0.6, 0.92, 1.0, 1)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _RimIntensity ("Rim Intensity", Range(0.0, 5.0)) = 0.0

        [Header(Tier Bioluminescence and Pulse)]
        _GlowColor ("Bioluminescence Color", Color) = (0.4, 0.85, 1.0, 1)
        _GlowStrength ("Bioluminescence Strength", Range(0.0, 4.0)) = 0.0
        _MaskGlowWeight ("Mask Glow Weight", Range(0.0, 1.0)) = 0.65
        _PulseSpeed ("Glow Pulse Speed", Range(0.0, 10.0)) = 2.2

        [Header(Tier Underwater Shimmer and Shine)]
        _IridescenceStrength ("Iridescence Strength", Range(0.0, 2.0)) = 0.0
        _ShineSpeed ("Shine Sweep Speed", Range(0.0, 4.0)) = 0.45
        _ShineStrength ("Shine Sweep Strength", Range(0.0, 3.0)) = 0.0

        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull Mode", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 relativeWS : TEXCOORD3;
            };

            TEXTURE2D(_fish_texture);
            SAMPLER(sampler_fish_texture);
            TEXTURE2D(_fish_mask);
            SAMPLER(sampler_fish_mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _fish_texture_ST;
                float4 _fish_mask_ST;
                half4 _FishColor;
                half4 _BaseColor;
                half _Cutoff;
                half _Smoothness;
                half _Metallic;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _GlowColor;
                half _GlowStrength;
                half _MaskGlowWeight;
                half _PulseSpeed;
                half _IridescenceStrength;
                half _ShineSpeed;
                half _ShineStrength;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = normInputs.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _fish_texture);

                float3 originWS = float3(UNITY_MATRIX_M[0][3], UNITY_MATRIX_M[1][3], UNITY_MATRIX_M[2][3]);
                output.relativeWS = posInputs.positionWS - originWS;
                return output;
            }

            half3 SampleIridescentTint(half fresnel)
            {
                half phase = fresnel * 4.5h + _Time.y * 0.7h;
                half3 irid = 0.5h + 0.5h * cos(phase + half3(0.0h, 2.094h, 4.188h));
                return irid;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 texCol = SAMPLE_TEXTURE2D(_fish_texture, sampler_fish_texture, input.uv);
                clip(texCol.a - _Cutoff);

                half maskR = SAMPLE_TEXTURE2D(_fish_mask, sampler_fish_mask, input.uv).r;
                half3 tinted = texCol.rgb * _FishColor.rgb;
                half3 albedo = lerp(texCol.rgb, tinted, maskR);

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndotv = saturate(dot(normalWS, viewDirWS));
                half fresnel = pow(1.0h - ndotv, max(0.5h, _RimPower));

                // Subtle pearlescent/iridescent scale shift along view edges (preserves center body hue)
                half3 iridColor = SampleIridescentTint(fresnel);
                albedo = lerp(albedo, albedo * (0.82h + iridColor * 0.52h), saturate(fresnel * _IridescenceStrength));

                // Tier Rim Glow + Color-Preserving Bioluminescence Pulse + Underwater Shine Sweep
                half pulse = 0.78h + 0.22h * sin(_Time.y * _PulseSpeed);
                half maskFactor = lerp(0.35h, maskR, _MaskGlowWeight);

                half3 rimEmission = _RimColor.rgb * (fresnel * _RimIntensity * pulse);
                half3 bodyGlow = (albedo + 0.10h) * _GlowColor.rgb * (_GlowStrength * maskFactor * pulse);

                // Periodic diagonal underwater shimmer band sweeping cleanly across the fish
                float sweepCoord = dot(input.relativeWS, float3(0.75, 0.45, 0.35)) * 0.055;
                float cycle = frac(_Time.y * max(0.01, _ShineSpeed)) * 4.2 - 2.1;
                float distToBand = abs(sweepCoord - cycle);
                half shineMask = (half)smoothstep(0.40, 0.0, distToBand);
                half3 shineEmission = lerp(_RimColor.rgb, half3(1.0h, 0.98h, 0.88h), 0.55h) * (shineMask * _ShineStrength);

                half3 totalEmission = rimEmission + bodyGlow + shineEmission;

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.metallic = _Metallic;
                surfaceData.specular = half3(0.0h, 0.0h, 0.0h);
                surfaceData.smoothness = _Smoothness;
                surfaceData.normalTS = half3(0.0h, 0.0h, 1.0h);
                surfaceData.occlusion = 1.0h;
                surfaceData.emission = totalEmission;
                surfaceData.alpha = texCol.a;

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = viewDirWS;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                half4 finalColor = UniversalFragmentPBR(inputData, surfaceData);
                finalColor.a = 1.0h;
                return finalColor;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_fish_texture);
            SAMPLER(sampler_fish_texture);
            TEXTURE2D(_fish_mask);
            SAMPLER(sampler_fish_mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _fish_texture_ST;
                float4 _fish_mask_ST;
                half4 _FishColor;
                half4 _BaseColor;
                half _Cutoff;
                half _Smoothness;
                half _Metallic;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _GlowColor;
                half _GlowStrength;
                half _MaskGlowWeight;
                half _PulseSpeed;
                half _IridescenceStrength;
                half _ShineSpeed;
                half _ShineStrength;
            CBUFFER_END

            float3 _LightDirection;

            Varyings ShadowVert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                output.uv = TRANSFORM_TEX(input.uv, _fish_texture);
                return output;
            }

            half4 ShadowFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_fish_texture, sampler_fish_texture, input.uv).a;
                clip(alpha - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_fish_texture);
            SAMPLER(sampler_fish_texture);
            TEXTURE2D(_fish_mask);
            SAMPLER(sampler_fish_mask);

            CBUFFER_START(UnityPerMaterial)
                float4 _fish_texture_ST;
                float4 _fish_mask_ST;
                half4 _FishColor;
                half4 _BaseColor;
                half _Cutoff;
                half _Smoothness;
                half _Metallic;
                half4 _RimColor;
                half _RimPower;
                half _RimIntensity;
                half4 _GlowColor;
                half _GlowStrength;
                half _MaskGlowWeight;
                half _PulseSpeed;
                half _IridescenceStrength;
                half _ShineSpeed;
                half _ShineStrength;
            CBUFFER_END

            Varyings DepthVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _fish_texture);
                return output;
            }

            half DepthFrag(Varyings input) : SV_Target
            {
                half alpha = SAMPLE_TEXTURE2D(_fish_texture, sampler_fish_texture, input.uv).a;
                clip(alpha - _Cutoff);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
