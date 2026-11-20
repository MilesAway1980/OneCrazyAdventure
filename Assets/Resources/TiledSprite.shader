// Draws a sprite repeated across a shape. The shape's texture coordinates count whole tiles (0 to 1 is one tile, 1 to 2 the next, and so
// on), and each is turned into a place inside the sprite's own part of its texture, so it repeats a sprite that is only a part of a
// larger texture (a sprite sheet, or one packed in an atlas), and doesn't need the texture's Wrap Mode set to Repeat. It's used by
// LiquidArea for the Tile overlay style, and lives in Resources so a build includes it.
Shader "OneCrazyAdventure/Tiled Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        // Where the sprite is in its texture: the left, the bottom, the width and the height, each from 0 to 1.
        _TileRect ("Sprite Rect In Texture", Vector) = (0,0,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "False"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            fixed4 _Color;
            float4 _TileRect;
            sampler2D _MainTex;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // The place in the tile, and so in the sprite. The slopes are the tiled ones, scaled to the sprite, so the texture is
                // sampled smoothly across the joins between tiles and doesn't leave a line there.
                float2 inTile = frac(IN.texcoord);
                float2 uv = _TileRect.xy + inTile * _TileRect.zw;
                float2 slopeX = ddx(IN.texcoord) * _TileRect.zw;
                float2 slopeY = ddy(IN.texcoord) * _TileRect.zw;

                fixed4 colour = tex2Dgrad(_MainTex, uv, slopeX, slopeY) * IN.color;
                return colour;
            }
            ENDCG
        }
    }
}
