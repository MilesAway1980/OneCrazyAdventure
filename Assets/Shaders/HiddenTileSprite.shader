// A lit 2D sprite that is only shown where a Flashlight's beam falls on it. It is the Universal Render Pipeline's own
// "Sprite-Lit-Default" shader, with a mask over the picture. The mask is worked out for every pixel from the beams that
// HiddenTileSection sends in as global values (_HiddenBeamA, B and C), the same way the section works out how bright a
// tile is, so a beam can cut a tile through at any angle.
//
// Each sprite says how much of each beam it can see (_BeamVisible, so a wall in the way can hide it) and how much of it
// stays shown whatever the beams do (_Floor). Those are set on each sprite by HiddenTileSection.
Shader "Hidden Tiles/Sprite Lit"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        // How much of each beam this sprite can see, from 0 to 1, and how much of it is shown whatever the beams do.
        _BeamVisible("Beam Visible", Vector) = (0, 0, 0, 0)
        _Floor("Always Shown", Range(0, 1)) = 0

        // Legacy properties. They're here so that materials using this shader can gracefully fallback to the legacy sprite shader.
        [HideInInspector] _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color        : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color        : COLOR;
                float2 hiddenWorld : TEXCOORD4;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            // NOTE: Do not ifdef the properties here as SRP batcher can not handle different layouts.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _BeamVisible;
                float _Floor;
            CBUFFER_END

            // The beams, set for every sprite at once by HiddenTileSection. For each: A is where it is and the way it points,
            // B is how far from the middle of it, in radians, it is at full strength and where it has faded out completely,
            // how far it reaches and how strong it is, and C is the fraction of its reach over which it fades away.
            float4 _HiddenBeamA[4];
            float4 _HiddenBeamB[4];
            float4 _HiddenBeamC[4];
            float _HiddenBeamCount;

            float HiddenSmooth(float t)
            {
                t = saturate(t);
                return t * t * (3.0 - 2.0 * t);
            }

            // How much a beam shows at a point in the world: its strength, less the further the point is toward the end of its
            // reach and the further it is to the side of where it points. Nothing is shown outside either.
            float HiddenBeamAmount(float2 point2D, float4 a, float4 b, float4 c)
            {
                float reach = b.z;
                float strength = b.w;

                if (reach <= 0.0 || strength <= 0.0)
                {
                    return 0.0;
                }

                float2 offset = point2D - a.xy;
                float dist = length(offset);

                if (dist > reach)
                {
                    return 0.0;
                }

                float fadeStart = reach * (1.0 - c.x);
                float distanceFactor = (dist <= fadeStart || reach <= fadeStart)
                    ? 1.0
                    : 1.0 - HiddenSmooth((dist - fadeStart) / (reach - fadeStart));

                float angleFactor = 1.0;

                if (dist > 0.0001)
                {
                    float angle = acos(clamp(dot(a.zw, offset / dist), -1.0, 1.0));

                    if (angle >= b.y)
                    {
                        return 0.0;
                    }

                    if (angle > b.x)
                    {
                        angleFactor = 1.0 - HiddenSmooth((angle - b.x) / (b.y - b.x));
                    }
                }

                return strength * distanceFactor * angleFactor;
            }

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                o.hiddenWorld = TransformObjectToWorld(input.positionOS).xy;

                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                float shown = _Floor;
                int count = (int)_HiddenBeamCount;

                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    if (i < count)
                    {
                        shown = max(shown, HiddenBeamAmount(input.hiddenWorld, _HiddenBeamA[i], _HiddenBeamB[i], _HiddenBeamC[i]) * _BeamVisible[i]);
                    }
                }

                half4 color = input.color;
                color.a *= saturate(shown);

                return CommonLitFragment(input, color);
            }
            ENDHLSL
        }
    }
}
