Shader "Beach/Hyperbolic Ocean" {
 Properties {
  _MainTex("Texture",2D)="white"{}
  _AOTex("Ambient",2D)="white"{}
  _Color("Shallow water",Color)=(0.04,0.53,0.65,1)
  _DeepColor("Deep water",Color)=(0.015,0.12,0.27,1)
  _Ambient("Ambient",Float)=1
  _BoundaryAO("Boundary",Float)=1
  _Noise("Noise",Color)=(0,0,0,0)
 }
 SubShader {
  Tags {"Queue"="Geometry" "HyperRenderType"="Opaque" "LightMode"="ForwardBase"}
  Pass {
   Cull Off
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_instancing
   #define HYPERBOLIC 1
   #define BEACH_OCEAN 1
   #include "../HyperEngine/Shaders/HyperCore.cginc"
   ENDCG
  }
 }
}
