Shader "Custom/VerticalFogUnlit"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        _FogColor ("Fog Color", Color) = (0.5, 0.7, 0.3, 1)
        _FogHeight ("Fog Height", Float) = 0
        _FogDensity ("Fog Density", Range(0.01, 10)) = 1
        _FogSmoothness ("Fog Smoothness", Range(0.01, 5)) = 1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _FogColor;
            float _FogHeight;
            float _FogDensity;
            float _FogSmoothness;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                
                // Calculate fog factor based on world Y position
                float heightAboveFog = i.worldPos.y - _FogHeight;
                float fogFactor = saturate(heightAboveFog * _FogDensity / _FogSmoothness);
                
                // Blend between fog color and texture color
                col.rgb = lerp(_FogColor.rgb, col.rgb, fogFactor);
                
                // Fade alpha based on fog (objects deeper in fog become more transparent)
                col.a *= fogFactor;
                
                return col;
            }
            ENDCG
        }
    }
    
    FallBack "Unlit/Transparent"
}