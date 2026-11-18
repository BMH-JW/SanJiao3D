// 暖金色「玻璃球 + 内部云 + 中心光」情绪球材质
// 不依赖 URP / HDRP 任何 Package，Built-in 与团结引擎都能直接编译
// 注意：地面反光、周围尘点、外侧飞行光弧属于场景搭建，不在本 shader 内
Shader "Custom/EmotionOrbWarm"
{
    Properties
    {
        // 最上面的颜色：同时决定整体色调和边缘光晕的颜色（HDR，亮度 > 1 才会泛光）
        [HDR]_Color ("Color (色调 / 光晕)", Color) = (1.0, 0.86, 0.64, 1)
        _GlowIntensity ("Glow Intensity 光晕强度", Range(0, 6)) = 1.8

        [HDR]_LightColor ("Light Color 光核颜色", Color) = (2.4, 2.0, 1.5, 1)
        _LightIntensity ("Light Intensity 光核强度", Range(0, 8)) = 1.6
        _LightSize ("Light Size 光核大小", Range(0.05, 1.0)) = 0.35
        _LightOffset ("Light Offset 光核偏移", Vector) = (-0.10, 0.05, 0, 0)

        [HDR]_CloudColor ("Cloud Color 云主体", Color) = (1.0, 0.80, 0.50, 1)
        [HDR]_CloudDark ("Cloud Shadow 云暗部", Color) = (0.62, 0.42, 0.22, 1)
        _CloudScale ("Cloud Scale 云的密度", Range(0.5, 8)) = 2.6
        _CloudContrast ("Cloud Contrast 云的对比", Range(0.3, 4)) = 1.6
        _CloudAmount ("Cloud Amount 云的多少", Range(0, 1)) = 0.85
        _CloudSpeed ("Cloud Speed 流速", Range(0, 2)) = 0.25

        _RimPower ("Rim Power 光环锐度", Range(0.5, 12)) = 3.0
        _RimEcho ("Rim Echo 外圈回声", Range(0, 2)) = 0.55
        _SpecPos ("Highlight Pos 高光位置", Vector) = (0.40, 0.40, 0, 0)
        _SpecSize ("Highlight Size 高光尺寸", Vector) = (0.16, 0.40, 0, 0)
        _SpecAngle ("Highlight Angle 高光角度", Range(-180, 180)) = 35
        _SpecIntensity ("Highlight Intensity 高光强度", Range(0, 4)) = 0.9

        _AlphaBase ("Alpha 不透明度", Range(0, 1)) = 0.96
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }

        Pass
        {
            Name "Orb"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Fog { Mode Off }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos      : SV_POSITION;
                float3 posOS    : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float3 worldNrm : TEXCOORD2;
            };

            half4  _Color;
            float  _GlowIntensity;
            half4  _LightColor;
            float  _LightIntensity;
            float  _LightSize;
            float4 _LightOffset;
            half4  _CloudColor;
            half4  _CloudDark;
            float  _CloudScale;
            float  _CloudContrast;
            float  _CloudAmount;
            float  _CloudSpeed;
            float  _RimPower;
            float  _RimEcho;
            float4 _SpecPos;
            float4 _SpecSize;
            float  _SpecAngle;
            float  _SpecIntensity;
            float  _AlphaBase;

            float hash31(float3 p)
            {
                p = frac(p * 0.3183099 + float3(0.1, 0.2, 0.3));
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float vnoise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = hash31(i + float3(0, 0, 0));
                float n100 = hash31(i + float3(1, 0, 0));
                float n010 = hash31(i + float3(0, 1, 0));
                float n110 = hash31(i + float3(1, 1, 0));
                float n001 = hash31(i + float3(0, 0, 1));
                float n101 = hash31(i + float3(1, 0, 1));
                float n011 = hash31(i + float3(0, 1, 1));
                float n111 = hash31(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float fbm(float3 p)
            {
                float sum = 0.0;
                float amp = 0.5;
                [unroll]
                for (int k = 0; k < 4; k++)
                {
                    sum += amp * vnoise(p);
                    p = p * 2.03 + 7.31;
                    amp *= 0.5;
                }
                return sum;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos      = UnityObjectToClipPos(v.vertex);
                o.posOS    = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNrm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3 N    = normalize(i.worldNrm);
                float3 V    = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);
                float  ndv  = saturate(dot(N, V));
                float  fres = 1.0 - ndv;

                // 视空间法线的 xy = 该点在球面投影上的位置（单位是球半径）
                float3 nVS  = mul((float3x3)UNITY_MATRIX_V, N);
                float2 disc = nVS.xy;

                // 1. 内部云层
                float  t = _Time.y * _CloudSpeed;
                float3 p = i.posOS * _CloudScale;

                float n1 = fbm(p + float3(t * 0.20, t * 0.35, t * 0.10));
                float n2 = fbm(p * 1.9 - float3(t * 0.15, -t * 0.20, t * 0.25));

                float cloud = saturate((n1 * 0.70 + n2 * 0.45 - 0.34) * 2.4);
                cloud = pow(cloud, _CloudContrast);

                float lower = 1.0 - smoothstep(-0.35, 0.85, i.posOS.y);
                cloud *= lerp(1.0, lower, 0.65) * _CloudAmount;

                // 2. 内部底色
                float3 glassTint = _Color.rgb * 1.05;
                float3 cloudCol  = lerp(_CloudDark.rgb, _CloudColor.rgb, cloud);
                float3 col = lerp(glassTint, cloudCol, saturate(cloud * 1.5));
                col *= lerp(1.0, 0.72, smoothstep(0.05, 0.75, fres));

                // 3. 中心光核
                float3 ldir  = normalize(float3(_LightOffset.x, _LightOffset.y, 1.0));
                float  dcore = 1.0 - saturate(dot(nVS, ldir));
                float  core  = saturate(1.0 - dcore / max(_LightSize, 1e-3));

                float glowCore = core * core * 1.2 + core * 0.35;
                col += _LightColor.rgb * _LightIntensity * glowCore * (1.0 - fres * 0.6);

                // 4. 玻璃边缘光环
                float rim  = pow(fres, _RimPower);
                float echo = pow(fres, _RimPower * 0.35);
                col += _Color.rgb * _GlowIntensity * (rim + echo * _RimEcho);

                // 5. 玻璃高光条
                float  ang = radians(_SpecAngle);
                float2 sp  = disc - _SpecPos.xy;
                float2 rsp = float2(sp.x * cos(ang) - sp.y * sin(ang),
                                    sp.x * sin(ang) + sp.y * cos(ang));
                float2 q   = rsp / max(_SpecSize.xy, float2(1e-4, 1e-4));
                float  spec = pow(saturate(1.0 - dot(q, q)), 2.2);
                col += _LightColor.rgb * spec * _SpecIntensity;

                // 6. 输出
                float alpha = saturate(_AlphaBase + fres * 1.6);
                return half4(col, alpha);
            }
            ENDCG
        }
    }
    Fallback Off
}
