Shader "Custom/CustomVoxelShader"
{
    Properties
    {
		_MainTex ("Texture", 2D) = "white" {}
		_Color ("Color", Color) = (1,1,1,1)
        _Strength ("Strength", Float) = 0
        _AmbientStrength ("Ambient Strength", Range(0,1)) = 0.3
        _SpecularPower ("Specular Power", Range(1,64)) = 16
        _SpecularStrength ("Specular Strength", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "VertexUnpack.hlsl"

			TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
			float4 _Color;
			float _Strength;
            float _AmbientStrength;
            float _SpecularPower;
            float _SpecularStrength;
            
            struct appdata
            {
	            float4 position : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
	            float4 vertex : SV_POSITION;
	            float3 normal : POSITION1;
	            float2 uv: TEXCOORD0;
	            float4 color : COLOR;
                float3 worldPos : TEXCOORD1;
                float boundsHeight : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(in appdata v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, OUT);
                
                float a;
                UnpackData_float(v.position, OUT.vertex, OUT.normal, OUT.uv, OUT.color, a);
                
                // Calculate world position for specular lighting
                OUT.worldPos = TransformObjectToWorld(OUT.vertex.xyz);
                OUT.normal = TransformObjectToWorldNormal(OUT.normal);
                
                // Extract mesh bounds height from object to world matrix scale
                // Unity provides unity_ObjectToWorld by default
                float3 objectScale = float3(
                    length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)),
                    length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21)),
                    length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22))
                );
                
                // Y scale (height) of the object
                OUT.boundsHeight = objectScale.y;
                
                OUT.vertex = TransformObjectToHClip(OUT.vertex);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                
                // Normalize vertex color by automatically calculated bounds height
                float4 normalizedColor = IN.color;
                if (IN.boundsHeight > 0)
                {
                    normalizedColor = IN.color / IN.boundsHeight;
                }
                
                float4 vertColor = normalizedColor * _Strength;
				float4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * vertColor;
               
                // Improved lighting calculation
                Light mainLight = GetMainLight();
                float3 normalizedNormal = normalize(IN.normal);
                
                // Diffuse lighting with ambient component
                float diffuseFactor = max(_AmbientStrength, dot(normalizedNormal, mainLight.direction));
                
                // Specular lighting (Blinn-Phong)
                float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
                float3 halfVector = normalize(mainLight.direction + viewDir);
                float specularFactor = pow(max(0, dot(normalizedNormal, halfVector)), _SpecularPower) * _SpecularStrength;
                
                // Final lighting calculation
                float3 lighting = (diffuseFactor * _Color.rgb + specularFactor) * mainLight.color;
                
                return float4(albedo.rgb * lighting, albedo.a);
            }
            ENDHLSL
        }
    }
}