// Made with Amplify Shader Editor v1.9.9.12
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OpenPutt/DitheredTrail"
{
	Properties
	{
		_MainTex( "Texture", 2D ) = "white" {}
		_Color( "Color", Color ) = ( 1, 1, 1, 1 )
		_DitherFade( "Dither Fade", Range( 0, 1 ) ) = 0.5
		_DitherStrength( "Dither Strength", Range( 1, 100 ) ) = 20

	}

	SubShader
	{
		

		

		Tags { "RenderType"="Opaque" }

	LOD 0

		ZWrite On
		Cull Off
		AlphaToMask On
		ColorMask RGBA
		Blend One Zero, One Zero
		BlendOp Add, Add

		

		Blend One Zero, One Zero
		BlendOp Add, Add
		

		CGINCLUDE
			#pragma target 3.5
			// ensure rendering platforms toggle list is visible

			float4 ComputeClipSpacePosition( float2 screenPosNorm, float deviceDepth )
			{
				float4 positionCS = float4( screenPosNorm * 2.0 - 1.0, deviceDepth, 1.0 );
			#if UNITY_UV_STARTS_AT_TOP
				positionCS.y = -positionCS.y;
			#endif
				return positionCS;
			}
		ENDCG

		
		Pass
		{
			
			Name "Unlit"
			Tags { "LightMode"="ForwardBase" }

			Cull Back
			ZWrite On
			ZTest LEqual
			Offset 0 , 0
			ColorMask RGBA
			Blend One Zero, One Zero
			BlendOp Add, Add

			

			CGPROGRAM
				#define ASE_OPAQUE_KEEP_ALPHA
				#define ASE_VERSION 19912

				#pragma vertex vert
				#pragma fragment frag
				#pragma multi_compile_instancing
				#include "UnityCG.cginc"

				#include "UnityShaderVariables.cginc"
				#define ASE_NEEDS_TEXTURE_COORDINATES0
				#define ASE_NEEDS_FRAG_SCREEN_POSITION_NORMALIZED
				#define ASE_NEEDS_FRAG_TEXTURE_COORDINATES0


				#if defined(ASE_WRITE_DEPTH_CONSERVATIVE) && (SHADER_TARGET >= 45)
					#define ASE_SV_DEPTH SV_DepthLessEqual
					#define ASE_SV_POSITION_QUALIFIERS linear noperspective centroid
				#else
					#define ASE_SV_DEPTH SV_Depth
					#define ASE_SV_POSITION_QUALIFIERS
				#endif

				struct appdata
				{
					float4 vertex : POSITION;
					float3 normal : NORMAL;
					float4 tangent : TANGENT;
					float4 ase_texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
				};

				struct v2f
				{
					ASE_SV_POSITION_QUALIFIERS float4 pos : SV_POSITION;
					float4 ase_texcoord : TEXCOORD0;
					UNITY_VERTEX_INPUT_INSTANCE_ID
					UNITY_VERTEX_OUTPUT_STEREO
				};

				uniform sampler2D _MainTex;
				uniform float4 _MainTex_ST;
				uniform float4 _Color;
				uniform float _DitherStrength;
				uniform float _DitherFade;


				inline float Dither8x8Bayer( uint x, uint y )
				{
					const float dither[ 64 ] = {
					     1, 49, 13, 61,  4, 52, 16, 64,
					    33, 17, 45, 29, 36, 20, 48, 32,
					     9, 57,  5, 53, 12, 60,  8, 56,
					    41, 25, 37, 21, 44, 28, 40, 24,
					     3, 51, 15, 63,  2, 50, 14, 62,
					    35, 19, 47, 31, 34, 18, 46, 30,
					    11, 59,  7, 55, 10, 58,  6, 54,
					    43, 27, 39, 23, 42, 26, 38, 22};
					uint r = y * 8 + x;
					return dither[ min( r, 63 ) ] / 64; // same # of instructions as pre-dividing due to compiler magic
				}
				

				v2f vert( appdata v  )
				{
					UNITY_SETUP_INSTANCE_ID(v);
					v2f o;
					UNITY_INITIALIZE_OUTPUT(v2f,o);
					UNITY_TRANSFER_INSTANCE_ID(v,o);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

					o.ase_texcoord.xy = v.ase_texcoord.xy;
					
					//setting value to unused interpolator channels and avoid initialization warnings
					o.ase_texcoord.zw = 0;

					#ifdef ASE_ABSOLUTE_VERTEX_POS
						float3 defaultVertexValue = v.vertex.xyz;
					#else
						float3 defaultVertexValue = float3(0, 0, 0);
					#endif
					float3 vertexValue = defaultVertexValue;
					#ifdef ASE_ABSOLUTE_VERTEX_POS
						v.vertex.xyz = vertexValue;
					#else
						v.vertex.xyz += vertexValue;
					#endif
					v.vertex.w = 1;
					v.normal = v.normal;
					v.tangent = v.tangent;

					o.pos = UnityObjectToClipPos( v.vertex );

					#if defined( ASE_SHADOWS )
						UNITY_TRANSFER_SHADOW( o, v.texcoord );
					#endif
					return o;
				}

				half4 frag( v2f IN 
							#if defined( ASE_WRITE_DEPTH )
								, out float outputDepth : SV_Depth
							#endif
				) : SV_Target
				{
					UNITY_SETUP_INSTANCE_ID( IN );
					UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX( IN );

					float4 ScreenPosNorm = float4( IN.pos.xy * ( _ScreenParams.zw - 1.0 ), IN.pos.zw );
					float4 ClipPos = ComputeClipSpacePosition( ScreenPosNorm.xy, IN.pos.z ) * IN.pos.w;
					float4 ScreenPos = ComputeScreenPos( ClipPos );

					float2 uv_MainTex = IN.ase_texcoord.xy * _MainTex_ST.xy + _MainTex_ST.zw;
					float4 tex2DNode3 = tex2D( _MainTex, uv_MainTex );
					float4 ditherScreenPos17 = ( ScreenPosNorm * ( max( _DitherStrength, 1.0 ) * 0.25 ) );
					float2 ditherScreenPosPixel17 = ditherScreenPos17.xy * _ScreenParams.xy;
					float dither17 = Dither8x8Bayer( fmod( ditherScreenPosPixel17.x, 8 ), fmod( ditherScreenPosPixel17.y, 8 ) );
					float2 texCoord6 = IN.ase_texcoord.xy * float2( 1,1 ) + float2( 0,0 );
					clip( dither17 - saturate( ( ( texCoord6.x - ( 1.0 - _DitherFade ) ) / _DitherFade ) ));
					

					float3 Color = ( tex2DNode3 * _Color ).rgb;
					float Alpha = ( tex2DNode3.a * _Color.a );
					half AlphaClipThreshold = 0.5;
					half AlphaClipThresholdShadow = 0.5;

					#if defined( ASE_WRITE_DEPTH )
						outputDepth = IN.pos.z;
					#endif

					#ifdef _ALPHATEST_ON
						clip( Alpha - AlphaClipThreshold );
					#endif

				#if defined( ASE_SURFACE_TRANSPARENT ) || defined( ASE_OPAQUE_KEEP_ALPHA )
					return half4( Color, Alpha );
				#else
					return half4( Color, 1.0 );
				#endif
				}
			ENDCG
		}

	
	}
	CustomEditor "AmplifyShaderEditor.MaterialInspector"
	
	Fallback Off
}
/*ASEBEGIN
Version=19912
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":7,"pos":[-2160,536],"params":["Float","False","Property","_DitherFade","Dither Fade","2","0","Create","True","0","0","0","False","0","False","Object","-1","","0.5","0.5","0","1","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":8,"pos":[-2160,720],"params":["Float","False","Property","_DitherStrength","Dither Strength","3","0","Create","True","0","0","0","False","0","False","Object","-1","","20","20","1","100","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":6,"pos":[-1840,416],"params":["Inherit","False","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":9,"pos":[-1840,608],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMaxOpNode, AmplifyShaderEditor","id":13,"pos":[-1840,704],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","1","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleSubtractOpNode, AmplifyShaderEditor","id":10,"pos":[-1520,456],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":14,"pos":[-1520,576],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0.25","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ScreenPosInputsNode, AmplifyShaderEditor","id":15,"pos":[-1520,696],"params":["Float","False","0","False","0","5","FLOAT4","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":3,"pos":[-1248,-24],"params":["Inherit","True","Property","_MainTex","Texture","0","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.ColorNode, AmplifyShaderEditor","id":4,"pos":[-1248,240],"params":["Float","False","Property","_Color","Color","1","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1,1,1","0,0,0,0","False","True","0","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor","id":11,"pos":[-1248,456],"params":["Inherit","False","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":16,"pos":[-1248,576],"params":["Inherit","False","2","2","0","FLOAT4","0,0,0,0","False","1","FLOAT","0","False","1","FLOAT4","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":5,"pos":[-864,112],"params":["Inherit","False","2","2","0","COLOR","0,0,0,0","False","1","COLOR","0,0,0,0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.SaturateNode, AmplifyShaderEditor","id":12,"pos":[-864,232],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.DitherNode, AmplifyShaderEditor","id":17,"pos":[-864,328],"params":["Inherit","False","1","True","4","0","FLOAT","0","False","1","FLOAT4","0,0,0,0","False","2","SAMPLER2D","","False","3","SAMPLERSTATE","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.ClipNode, AmplifyShaderEditor","id":18,"pos":[-592,104],"params":["Inherit","False","3","0","COLOR","0,0,0,0","False","1","FLOAT","0","False","2","FLOAT","0","False","1","COLOR","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":19,"pos":[-592,-16],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":0,"pos":[-2160,952],"params":["Float","False","False","-1","3","AmplifyShaderEditor.MaterialInspector","0","1","New Amplify Shader","0770190933193b94aaa3065e307002fa","True","ExtraPrePass","0","0","ExtraPrePass","6","False","True","1","1","False","","0","False","","1","1","False","","0","False","","True","1","False","","1","False","","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","False","False","False","True","1","RenderType=Opaque=RenderType","True","3","True","12","all","0","False","True","1","1","False","","0","False","","0","1","False","","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","1","LightMode=ForwardBase","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":2,"pos":[-2160,1048],"params":["Float","False","False","-1","3","AmplifyShaderEditor.MaterialInspector","0","1","New Amplify Shader","0770190933193b94aaa3065e307002fa","True","ShadowCaster","0","2","ShadowCaster","0","False","True","1","1","False","","0","False","","1","1","False","","0","False","","True","1","False","","1","False","","False","False","False","False","False","False","False","False","False","True","0","False","","False","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","False","False","False","True","1","RenderType=Opaque=RenderType","True","3","True","12","all","0","False","False","False","False","False","False","False","False","False","False","False","False","True","0","False","","False","False","False","False","False","False","False","False","False","False","False","False","False","True","1","False","","True","3","False","","False","False","True","1","LightMode=ShadowCaster","False","False","0","","0","0","Standard","0","False","0"]}
{"type":"AmplifyShaderEditor.TemplateMultiPassMasterNode, AmplifyShaderEditor","id":1,"pos":[-320,0],"params":["Float","False","True","-1","3","AmplifyShaderEditor.MaterialInspector","0","7","OpenPutt/DitheredTrail","0770190933193b94aaa3065e307002fa","True","Unlit","0","1","Unlit","8","False","True","1","1","False","","0","False","","1","1","False","","0","False","","True","1","False","","1","False","","False","False","False","False","False","False","False","False","False","True","1","False","","True","True","2","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","False","False","False","True","1","RenderType=Opaque=RenderType","True","3","True","12","all","0","False","True","1","1","False","","0","False","","1","1","False","","0","False","","True","1","False","","1","False","","False","False","False","False","False","False","False","False","False","False","True","True","0","False","","False","True","True","True","True","True","0","False","","False","False","False","False","False","False","False","True","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","True","1","False","","True","3","False","","True","True","0","False","","0","False","","False","True","1","LightMode=ForwardBase","False","False","0","","0","0","Standard","10","Surface","0","0","  Keep Alpha","1","0","  Blend","0","0","Alpha Clipping","0","0","  Use Shadow Threshold","0","0","Cast Shadows","0","0","Write Depth","0","0","  Conservative","0","0","Extra Pre Pass","0","0","Vertex Position","1","0","0","3","False","True","False","False","","False","0"]}
{"wire":[9,0,7,0]}
{"wire":[13,0,8,0]}
{"wire":[10,0,6,1]}
{"wire":[10,1,9,0]}
{"wire":[14,0,13,0]}
{"wire":[11,0,10,0]}
{"wire":[11,1,7,0]}
{"wire":[16,0,15,0]}
{"wire":[16,1,14,0]}
{"wire":[5,0,3,0]}
{"wire":[5,1,4,0]}
{"wire":[12,0,11,0]}
{"wire":[17,1,16,0]}
{"wire":[18,0,5,0]}
{"wire":[18,1,17,0]}
{"wire":[18,2,12,0]}
{"wire":[19,0,3,4]}
{"wire":[19,1,4,4]}
{"wire":[1,0,18,0]}
{"wire":[1,7,19,0]}
ASEEND*/
//CHKSM=3A8F831D77E0D9BD9123F78AF288C941A919C304