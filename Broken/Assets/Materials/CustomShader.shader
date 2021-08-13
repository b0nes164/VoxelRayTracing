Shader "Unlit/CustomShader"
{
            SubShader
            {
                Pass
                {
                    CGPROGRAM
                    #pragma vertex vert
                    #pragma fragment frag
                    #include "UnityCG.cginc"

                    struct v2f
                    {
                        float3 uv : TEXCOORD0;
                        float4 vertex : SV_POSITION;
                    };

                    struct appdata
                    {
                        float3 uv : TEXCOORD0;
                        float4 vertex : POSITION;
                    };

                    StructuredBuffer<uint3> _LocalPositionBuffer;
                    StructuredBuffer<uint> _RenderProperties;

                    extern int e_xOffset;
                    extern int e_yOffset;
                    extern int e_zOffset;

                    v2f vert(appdata i, uint instanceID: SV_InstanceID) {
                        v2f o;

                        float4 pos = mul(
                            
                            float4x4
                            (   1, 0, 0, _LocalPositionBuffer[_RenderProperties[instanceID] & 0x7FFF].x + e_xOffset,
                                0, 1, 0, _LocalPositionBuffer[_RenderProperties[instanceID] & 0x7FFF].y + e_yOffset,
                                0, 0, 1, _LocalPositionBuffer[_RenderProperties[instanceID] & 0x7FFF].z + e_zOffset,
                                0, 0, 0, 1
                            ),
                            i.vertex);

                        o.vertex = UnityObjectToClipPos(pos);
                        o.uv.xy = i.uv.xy;
                        o.uv.z = _RenderProperties[instanceID] >> 16;
                        return o;
                    }

                    UNITY_DECLARE_TEX2DARRAY(_MyArr);

                    fixed4 frag(v2f i) : SV_Target
                    {
                        return UNITY_SAMPLE_TEX2DARRAY(_MyArr, i.uv);
                    }
                    ENDCG
                }
            }
}
