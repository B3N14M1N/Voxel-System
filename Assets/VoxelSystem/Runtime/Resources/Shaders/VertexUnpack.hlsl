#ifndef VERTEXUNPACK_INCLUDED
#define VERTEXUNPACK_INCLUDED

static const float3 MyNormals[6]  = 
{
	float3(0.0, 0.0, -1.0),
	float3(1.0, 0.0, 0.0),
	float3(0.0, 0.0, 1.0),
	float3(-1.0, 0.0, 0.0),
	float3(0.0, 1.0, 0.0),
	float3(0.0, -1.0, 0.0)
};

static const float2 MyUVs[4] = 
{
	float2(0.0, 0.0),
	float2(1.0, 0.0),
	float2(1.0, 1.0),
	float2(0.0, 1.0)
};

void UnpackData_float(in float4 packedData,
	inout float4 vertex,
	inout float3 normal,
	inout float2 uv,
	inout float4 color,
	inout float textureIndex)
{
	uint data = asuint(packedData.x);
	// Unpack position using our new bit allocation: 11-10-11 bits
	vertex = float4(data & 0x7FF, (data >> 11) & 0x3FF, (data >> 21) & 0x7FF, 1.0);
	
	// Unpack normal and uv indices
	uint normalData = asuint(packedData.y);
	uint normalIdx = normalData & 0x7;
	uint uvIdx = (normalData >> 3) & 0x3;
	
	// Get height for color/shading
	float height = float((normalData >> 5));
	
	normal = float3(MyNormals[normalIdx]);
	uv = float2(MyUVs[uvIdx]);
	color = float4(height, height, height, 1.0);
	textureIndex = packedData.z;
}

#endif

