Shader "Custom/CustomVoxelShader"
{
    Properties
    {
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
        _Strength ("Strenght", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "VertexUnpack.hlsl"

			sampler2D _MainTex;
			float4 _Color;
			float _Strength;
            
            struct appdata
            {
	            float4 position : POSITION;
            };

            struct v2f
            {
	            float4 vertex : SV_POSITION;
	            float3 normal : POSITION1;
	            float2 uv: TEXCOORD0;
	            float4 color : COLOR;
            };

            v2f vert(in appdata v)
            {
                v2f OUT;
                float a;
                float b;
                bool c;
                UnpackData_float(v.position, OUT.vertex, OUT.normal, OUT.uv, OUT.color, a, b, c);
                OUT.vertex = TransformObjectToHClip(OUT.vertex);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float4 vertColor = IN.color * _Strength;
				float4 col = tex2D(_MainTex, IN.uv) * vertColor;
               
                Light mainLight = GetMainLight();
                 half nl = max(0.3, dot(IN.normal, mainLight.direction));
                half4 diff = nl * _Color;
                return col * diff;

            }
            
            v2f UnpackData(in appdata v)
            {
	            v2f f;
	            int data = asint(v.position.x);
	            f.vertex = float4(data & 0xff, (data >> 8) & 0xff,(data >> 16) & 0xff, 1.0);
	            f.normal = float3(MyNormals[floor(asint(v.position.z) & 0xff)]);
	            f.uv = float2(MyUVs[floor((asint(v.position.z) >> 8) & 0xff)]);
	            f.color = float4(float(v.position.y), float(v.position.y), float(v.position.y), 1.0);
	            return f;
            };
            ENDHLSL
        }
    }
}