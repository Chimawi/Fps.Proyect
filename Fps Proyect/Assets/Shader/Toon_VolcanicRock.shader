Shader "Custom/Toon_VolcanicRock"
{
    // Fully procedural (no texture needed): dark toon-shaded rock with animated
    // glowing lava cracks, an ember rim, and the same Looney Tunes ink outline
    // used by Toon_LooneyTunes.shader.
    Properties
    {
        [Header(Rock Base)]
        _RockColor("Rock Color (Lit)", Color) = (0.1,0.085,0.08,1)
        _RockColorDark("Rock Color (Shadow)", Color) = (0.02,0.017,0.017,1)
        [IntRange] _ToonSteps("Toon Steps", Range(2,6)) = 3

        [Header(Lava Cracks)]
        _LavaColor("Lava Color", Color) = (0.9,0.22,0.03,1)
        _LavaHotColor("Lava Hot Color", Color) = (1,0.85,0.35,1)
        _CrackScale("Crack Scale", Range(1,30)) = 8
        _CrackThreshold("Crack Density", Range(0,1)) = 0.55
        _CrackSharpness("Crack Edge Softness", Range(0.01,0.5)) = 0.08
        _FlowSpeed("Lava Flow Speed", Range(0,5)) = 0.4
        _EmissionIntensity("Emission Intensity", Range(0,10)) = 2.5

        [Header(Ember Rim)]
        _RimColor("Ember Rim Color", Color) = (1,0.5,0.1,1)
        _RimAmount("Rim Threshold", Range(0,1)) = 0.65
        _RimIntensity("Rim Intensity", Range(0,3)) = 1.0

        [Header(Specular Highlight)]
        _SpecularColor("Specular Color", Color) = (1,0.9,0.7,1)
        _SpecularSize("Specular Size", Range(0,1)) = 0.06
        _SpecularSmoothness("Specular Softness", Range(0.001,0.5)) = 0.02

        [Header(Outline)]
        _OutlineColor("Outline Color", Color) = (0.02,0.02,0.02,1)
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
                half4 _RockColor;
                half4 _RockColorDark;
                half _ToonSteps;
                half4 _LavaColor;
                half4 _LavaHotColor;
                half _CrackScale;
                half _CrackThreshold;
                half _CrackSharpness;
                half _FlowSpeed;
                half _EmissionIntensity;
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

        // Flat-banded rock shading + procedural animated lava cracks + ember rim.
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

            CBUFFER_START(UnityPerMaterial)
                half4 _RockColor;
                half4 _RockColorDark;
                half _ToonSteps;
                half4 _LavaColor;
                half4 _LavaHotColor;
                half _CrackScale;
                half _CrackThreshold;
                half _CrackSharpness;
                half _FlowSpeed;
                half _EmissionIntensity;
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
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
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

            float Hash13(float3 p3)
            {
                p3 = frac(p3 * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Trilinear value noise, no textures required.
            float ValueNoise3D(float3 p)
            {
                float3 i = floor(p);
                float3 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);

                float n000 = Hash13(i + float3(0, 0, 0));
                float n100 = Hash13(i + float3(1, 0, 0));
                float n010 = Hash13(i + float3(0, 1, 0));
                float n110 = Hash13(i + float3(1, 1, 0));
                float n001 = Hash13(i + float3(0, 0, 1));
                float n101 = Hash13(i + float3(1, 0, 1));
                float n011 = Hash13(i + float3(0, 1, 1));
                float n111 = Hash13(i + float3(1, 1, 1));

                float nx00 = lerp(n000, n100, f.x);
                float nx10 = lerp(n010, n110, f.x);
                float nx01 = lerp(n001, n101, f.x);
                float nx11 = lerp(n011, n111, f.x);

                float nxy0 = lerp(nx00, nx10, f.y);
                float nxy1 = lerp(nx01, nx11, f.y);

                return lerp(nxy0, nxy1, f.z);
            }

            float CrackNoise(float3 p)
            {
                float n = ValueNoise3D(p) * 0.6 + ValueNoise3D(p * 2.13 + 7.3) * 0.4;
                return n;
            }

            Varyings LitVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = vertexInput.positionCS;
                OUT.positionWS = vertexInput.positionWS;
                OUT.normalWS = normalInput.normalWS;
                OUT.shadowCoord = GetShadowCoord(vertexInput);
                return OUT;
            }

            half4 LitFrag(Varyings IN) : SV_Target
            {
                float3 normalWS = normalize(IN.normalWS);
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));

                Light mainLight = GetMainLight(IN.shadowCoord);
                float NdotL = dot(normalWS, mainLight.direction);

                // --- Flat toon rock shading ---
                half lightMask = ToonBand(NdotL * 0.5h + 0.5h, _ToonSteps) * mainLight.shadowAttenuation;
                half3 rockLit = _RockColor.rgb * mainLight.color;
                half3 rockShadow = _RockColorDark.rgb;
                half3 rockShaded = lerp(rockShadow, rockLit, lightMask);

                // --- Procedural glowing lava cracks (flowing over time, no texture) ---
                float3 flowOffset = float3(0, -_Time.y * _FlowSpeed, _Time.y * _FlowSpeed * 0.5);
                float n = CrackNoise(IN.positionWS * _CrackScale + flowOffset);
                half crack = 1.0h - smoothstep(0.0h, _CrackSharpness, abs(n - _CrackThreshold));

                half pulse = 0.85h + 0.15h * sin(_Time.y * 3.0h + n * 6.2832h);
                half3 lavaGlow = lerp(_LavaColor.rgb, _LavaHotColor.rgb, saturate(crack * pulse)) * _EmissionIntensity * pulse;

                half3 shadedResult = lerp(rockShaded, lavaGlow, crack);

                // --- Ember heat rim on the silhouette ---
                half rimDot = 1.0h - saturate(dot(normalWS, viewDirWS));
                half rim = smoothstep(_RimAmount - 0.05h, _RimAmount + 0.05h, rimDot);
                half3 rimLight = rim * _RimIntensity * _RimColor.rgb;

                // --- Small hard toon specular (wet obsidian glint) ---
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                half NdotH = saturate(dot(normalWS, halfDir));
                half spec = smoothstep(1.0h - _SpecularSize, 1.0h - _SpecularSize + _SpecularSmoothness, NdotH) * mainLight.shadowAttenuation * saturate(NdotL);
                half3 specular = spec * _SpecularColor.rgb * (1.0h - crack);

                half3 finalColor = shadedResult + rimLight + specular;
                return half4(finalColor, 1.0h);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
