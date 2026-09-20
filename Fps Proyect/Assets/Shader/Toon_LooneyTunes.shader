Shader "Custom/Toon_LooneyTunes"
{
    Properties
    {
        [Header(Base)]
        _BaseMap("Albedo (Grass001)", 2D) = "white" {}
        _BaseColor("Color Tint", Color) = (1,1,1,1)

        [Header(Cel Shading)]
        [IntRange] _ToonSteps("Toon Steps", Range(2,6)) = 3
        _ShadowColor("Shadow Tint", Color) = (0.45,0.5,0.7,1)

        [Header(Rim Light)]
        _RimColor("Rim Color", Color) = (1,1,0.9,1)
        _RimAmount("Rim Threshold", Range(0,1)) = 0.7
        _RimIntensity("Rim Intensity", Range(0,3)) = 1.2

        [Header(Specular Highlight)]
        _SpecularColor("Specular Color", Color) = (1,1,1,1)
        _SpecularSize("Specular Size", Range(0,1)) = 0.08
        _SpecularSmoothness("Specular Softness", Range(0.001,0.5)) = 0.02

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.05,0.05,0.05,1)
        _OutlineWidth("Outline Width", Range(0,0.05)) = 0.006
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        LOD 200

        // Classic cartoon outline: render backfaces pushed out along the normal in solid ink color.
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ToonSteps;
                half4 _RimColor;
                half _RimAmount;
                half _RimIntensity;
                half4 _SpecularColor;
                half _SpecularSize;
                half _SpecularSmoothness;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 positionWS = vertexInput.positionWS + normalWS * _OutlineWidth;
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // Flat-banded (cel shaded) lighting with a hard specular pop and rim light.
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Back

            HLSLPROGRAM
            #pragma vertex LitVert
            #pragma fragment LitFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _ShadowColor;
                half _ToonSteps;
                half4 _RimColor;
                half _RimAmount;
                half _RimIntensity;
                half4 _SpecularColor;
                half _SpecularSize;
                half _SpecularSmoothness;
                half4 _OutlineColor;
                half _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            // Quantizes x (0..1) into `steps` flat bands with a hair of AA on the edge (fwidth) so bands stay crisp, not jagged.
            half ToonBand(half x, half steps)
            {
                steps = max(steps, 1.0h);
                half scaled = saturate(x) * steps;
                half stepped = floor(scaled);
                half nextStepped = min(stepped + 1.0h, steps - 1.0h);
                half f = frac(scaled);
                half w = max(fwidth(scaled) * 0.5h, 1e-4h);
                half t = smoothstep(0.5h - w, 0.5h + w, f);
                return lerp(stepped, nextStepped, t) / max(steps - 1.0h, 1.0h);
            }

            Varyings LitVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.shadowCoord = GetShadowCoord(vertexInput);
                return OUT;
            }

            half4 LitFrag(Varyings IN) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL = dot(normalWS, mainLight.direction);

                half lightMask = ToonBand(NdotL * 0.5h + 0.5h, _ToonSteps) * mainLight.shadowAttenuation;

                half3 litColor = albedo.rgb * mainLight.color;
                half3 shadowedColor = albedo.rgb * _ShadowColor.rgb;
                half3 baseLighting = lerp(shadowedColor, litColor, lightMask);

                // Rim light: exaggerated silhouette pop, classic cartoon "glow" on the lit side only.
                half rimDot = 1.0h - saturate(dot(normalWS, viewDirWS));
                half rim = smoothstep(_RimAmount - 0.05h, _RimAmount + 0.05h, rimDot) * saturate(NdotL + 0.5h);
                half3 rimLight = rim * _RimIntensity * _RimColor.rgb;

                // Hard-edged toon specular, like the single bright dot on a cartoon eye/apple.
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half spec = smoothstep(1.0h - _SpecularSize, 1.0h - _SpecularSize + _SpecularSmoothness, NdotH) * mainLight.shadowAttenuation * saturate(NdotL);
                half3 specular = spec * _SpecularColor.rgb;

                half3 finalColor = baseLighting + rimLight + specular;
                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
