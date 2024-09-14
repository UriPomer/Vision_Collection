Shader"Custom/Cutoff"
{
    Properties
    {
        _RenderTexture ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD0;
            };

            sampler2D _RenderTexture;

            v2f vert (appdata_base v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Perform screen-space clipping
                if (i.screenPos.x < 0 || i.screenPos.x > i.screenPos.w || 
                    i.screenPos.y < 0 || i.screenPos.y > i.screenPos.w)
                {
                    discard;  // This will discard the fragment (clip it)
                }

                // Sample the texture
                fixed4 col = tex2D(_RenderTexture, i.screenPos.xy / i.screenPos.w);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
