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
	inout float textureIndex,
	inout float faceIndex,
	inout bool useTop)
{
	int data = asint(packedData.x);
	vertex = float4(data & 0xff, (data >> 8) & 0xff,(data >> 16) & 0xff, 1.0);
	normal = float3(MyNormals[(data >> 24) & 0x7]);
	uv = float2(MyUVs[(data >>= 27) & 0x3]);
	color = float4(packedData.y, packedData.y, packedData.y, 1.0);
	data = asint(packedData.z);
	useTop = bool(data & 0xff);
	faceIndex = float((data >> 8) & 0xff);
	textureIndex = float((data >> 16) & 0xff);
};

#endif

