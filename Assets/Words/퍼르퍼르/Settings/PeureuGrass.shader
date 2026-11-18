// [퍼르퍼르 풍경] 바람에 물결치는 풀꽃
//  - 뿌리(아래)는 '뿌리 색', 끝(위)은 메시에 들어 있는 색(풀끝 연두 / 꽃잎 색)으로 이어짐
//  - 바람 세기·방향은 PeureuWind 값을 받아서 (PeureuAir가 _PeureuWind, _PeureuWindDir을 넣어 줌)
//    들판 위로 바람결이 '스르르' 지나가듯 물결쳐요.
//  - 사용법: 풍경 만들기 버튼이 '풀꽃' 재질을 이 셰이더로 자동으로 만들어요.
Shader "Peureu/Grass"
{
    Properties
    {
        _RootColor ("뿌리 색", Color) = (0.62, 0.70, 0.50, 1)
        _Brightness ("밝기", Range(0.3, 2)) = 1
        _SwayAmount ("흔들림 크기 (m)", Range(0, 1)) = 0.28
        _WaveScale ("바람결 간격 (작을수록 넓은 물결)", Range(0.02, 1)) = 0.16
        _WaveSpeed ("바람결 빠르기", Range(0, 6)) = 2.2
        _FlutterSpeed ("잔떨림 빠르기", Range(0, 20)) = 7
        _SunTint ("햇빛 받는 끝 색", Color) = (1, 0.93, 0.78, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            float4 _RootColor, _SunTint;
            float _Brightness, _SwayAmount, _WaveScale, _WaveSpeed, _FlutterSpeed;
            float _PeureuWind;
            float4 _PeureuWindDir;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float h : TEXCOORD0;
                UNITY_FOG_COORDS(1)
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float h = saturate(v.uv.y);
                float k = h * h;

                float2 wd = _PeureuWindDir.xz;
                float wl = length(wd);
                wd = wl > 0.001 ? wd / wl : float2(1, 0);
                float2 side = float2(-wd.y, wd.x);
                float wind = saturate(_PeureuWind * 0.75 + 0.15);

                // 들판 위로 지나가는 바람결 (바람 방향으로 흘러감)
                float phase = dot(wp.xz, wd) * _WaveScale - _Time.y * _WaveSpeed;
                float gust = sin(phase) * 0.5 + 0.5;
                gust = gust * gust;
                // 포기마다 다른 잔떨림
                float flutter = sin(_Time.y * _FlutterSpeed + wp.x * 3.1 + wp.z * 2.3) * 0.25;

                float push = (0.25 + gust * 0.9) * wind;
                wp.xz += wd * (push * _SwayAmount * k) + side * (flutter * _SwayAmount * k * wind);
                wp.y -= push * _SwayAmount * 0.35 * k;

                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1));
                o.h = h;
                o.color = v.color;
                o.color.a = gust;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 c = lerp(_RootColor.rgb, i.color.rgb, smoothstep(0.0, 1.0, i.h));
                // 바람결이 지나갈 때 풀끝이 살짝 밝게 반짝 (햇빛 받은 뒷면)
                c = lerp(c, c * _SunTint.rgb * 1.15, i.color.a * i.h * 0.6);
                c *= _Brightness;
                fixed4 col = fixed4(c, 1);
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    Fallback Off
}
