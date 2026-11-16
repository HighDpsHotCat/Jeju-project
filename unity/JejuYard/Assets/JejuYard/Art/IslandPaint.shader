Shader "Jeju/Island Paint"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase nolightmap nodirlightmap nodynlightmap novertexlight
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"
            struct PaintedVertex { float4 vertex : POSITION; float3 normal : NORMAL; fixed4 color : COLOR; };
            struct PaintedSurface
            {
                float4 pos : SV_POSITION;
                fixed3 pigment : COLOR0;
                half3 normal : TEXCOORD0;
                SHADOW_COORDS(1)
            };
            PaintedSurface vert(PaintedVertex v)
            {
                PaintedSurface o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.pigment = v.color.rgb;
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag(PaintedSurface i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half sun = smoothstep(-.20h,.93h,dot(n,normalize(_WorldSpaceLightPos0.xyz))) * SHADOW_ATTENUATION(i);
                half sky = saturate(n.y*.5h+.5h);
                fixed3 shade = lerp(fixed3(.56,.58,.64),fixed3(.76,.80,.81),sky);
                return fixed4(i.pigment * lerp(shade,fixed3(1.045,1.035,.98),sun),1);
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    FallBack "Diffuse"
}
