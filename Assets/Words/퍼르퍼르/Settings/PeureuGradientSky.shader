// [퍼르퍼르] 그라데이션 하늘 (Skybox)
//  - 위: 하늘색 → 중간: 연한 하늘색 → 지평선: 살구빛(안개와 같은 색) 으로 부드럽게 이어지는 하늘
//  - 지평선 색을 안개 색과 똑같이 두면, 땅과 하늘의 경계가 안개 속으로 사라진다.
//  - 사용법: '하늘' 재질 → Shader 드롭다운 → Peureu/GradientSky
Shader "Peureu/GradientSky"
{
    Properties
    {
        _TopColor     ("하늘 위 (Top)", Color) = (0.561, 0.765, 0.902, 1)      // #8FC3E6
        _MidColor     ("하늘 중간 (Middle)", Color) = (0.812, 0.902, 0.949, 1)  // #CFE6F2
        _HorizonColor ("지평선 (Horizon) = 안개 색과 같게", Color) = (0.953, 0.863, 0.784, 1) // #F3DCC8
        _BottomColor  ("지평선 아래 (Bottom)", Color) = (0.953, 0.863, 0.784, 1)

        _MidHeight ("중간 색이 시작되는 높이", Range(0.01, 1)) = 0.18
        _TopHeight ("위 색이 시작되는 높이", Range(0.01, 1)) = 0.75

        _SunDir       ("해 방향 (Directional Light 반대쪽)", Vector) = (0.83, 0.29, 0.48, 0)
        _SunColor     ("햇무리 색", Color) = (1, 0.78, 0.55, 1)
        _SunIntensity ("해 밝기", Range(0, 3)) = 0.8
        _SunSharpness ("해 크기 (클수록 작고 또렷)", Range(2, 200)) = 40
        _GlowIntensity("햇무리 퍼짐 밝기", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _TopColor, _MidColor, _HorizonColor, _BottomColor;
            float _MidHeight, _TopHeight;
            float4 _SunDir, _SunColor;
            float _SunIntensity, _SunSharpness, _GlowIntensity;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;

                float3 c = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, _MidHeight, h));
                c = lerp(c, _TopColor.rgb, smoothstep(_MidHeight, _TopHeight, h));

                // 지평선 아래
                float below = saturate(-h / 0.15);
                c = lerp(c, _BottomColor.rgb, below);

                // 해와 햇무리
                float s = saturate(dot(d, normalize(_SunDir.xyz)));
                c += _SunColor.rgb * (pow(s, _SunSharpness) * _SunIntensity + pow(s, 4.0) * _GlowIntensity);

                return float4(c, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
