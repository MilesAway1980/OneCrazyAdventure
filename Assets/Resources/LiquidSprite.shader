// Draws a sprite for LiquidArea's overlay, either stretched over a shape or repeated across it, and lights it with the scene's 2D lights, so
// that the liquid goes dark where it's dark and is lit by the lights near it.
//
// How much the lights matter is Light Response: 1 is lit as a sprite is, and 0 ignores the lights altogether, as an unlit sprite does, and
// in between is a mix of the two.
//
// For the Tiled setting, the shape's texture coordinates count whole tiles (0 to 1 is one tile, 1 to 2 the next, and so on), and each is turned
// into a place inside the sprite's own part of its texture, so it repeats a sprite that is only a part of a larger texture (a sprite sheet,
// or one packed in an atlas) and doesn't need the texture's Wrap Mode set to Repeat. Without it, the coordinates are the sprite's own, and
// the picture is stretched. It lives in Resources so a build includes it.
//
// The lit pass is made from the one URP's Sprite-Lit-Default has (Packages/com.unity.render-pipelines.universal/Shaders/2D), so it follows
// the 2D renderer's lights and blend styles. It has no normal map pass, so the liquid is lit flat.
Shader "OneCrazyAdventure/Liquid Sprite"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // Where the sprite is in its texture: the left, the bottom, the width and the height, each from 0 to 1. Only used when Tiled.
        _TileRect ("Sprite Rect In Texture", Vector) = (0,0,1,1)
        [Toggle] _Tiled ("Tile The Sprite", Float) = 0
        _LightResponse ("Light Response", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

        // The same in every pass, as the SRP batcher needs it to be.
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float4 _TileRect;
            half _Tiled;
            half _LightResponse;
        CBUFFER_END
        ENDHLSL

        // Lit by the 2D lights.
        Pass
        {
            Name "Lit"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                Varyings o = CommonLitVertex(input);
                o.color = input.color * _Color;
                return o;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                // The slopes are worked out from the coordinates as they are, and scaled to the sprite when tiling, so the texture is sampled
                // smoothly across the joins between tiles and doesn't leave a line there.
                float2 uv = input.uv;
                float2 slopeX = ddx(uv);
                float2 slopeY = ddy(uv);

                if (_Tiled > 0.5)
                {
                    uv = _TileRect.xy + frac(uv) * _TileRect.zw;
                    slopeX *= _TileRect.zw;
                    slopeY *= _TileRect.zw;
                }

                const half4 main = input.color * SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, slopeX, slopeY);

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, 1, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

                const half4 lit = CombinedShapeLightShared(surfaceData, inputData);

                return half4(lerp(main.rgb, lit.rgb, _LightResponse), lit.a);
            }
            ENDHLSL
        }

        // Not lit: for when the lights aren't used, such as a view of the scene with lighting turned off.
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing

            Varyings UnlitVertex(Attributes input)
            {
                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color;
                return o;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                float2 uv = input.uv;
                float2 slopeX = ddx(uv);
                float2 slopeY = ddy(uv);

                if (_Tiled > 0.5)
                {
                    uv = _TileRect.xy + frac(uv) * _TileRect.zw;
                    slopeX *= _TileRect.zw;
                    slopeY *= _TileRect.zw;
                }

                return input.color * SAMPLE_TEXTURE2D_GRAD(_MainTex, sampler_MainTex, uv, slopeX, slopeY);
            }
            ENDHLSL
        }
    }
}
