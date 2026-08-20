// Made with Amplify Shader Editor v1.9.9.12
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OpenPutt/GolfCourse/LinesStandard"
{
	Properties
	{
		_HeightOffset( "HeightOffset", Range( -0.3, 0.3 ) ) = 0
		_LineBlend( "LineBlend", Range( 0, 1 ) ) = 0.2
		_LineHeightCM( "_LineHeightCM", Range( 0.5, 30 ) ) = 3
		[Toggle( _LOCKLINESTOPIVOT_ON )] _LockLinesToPivot( "LockLinesToPivot", Float ) = 0
		_Albedo( "Albedo", 2D ) = "white" {}
		_LinesDarkenAmount( "_LinesDarkenAmount", Range( 0, 1 ) ) = 0.25
		_Normal( "Normal", 2D ) = "bump" {}
		_Smoothness( "Smoothness", 2D ) = "white" {}
		_Metallic( "Metallic", 2D ) = "white" {}
		_TextureTiling( "Texture Tiling", Vector ) = ( 1, 1, 0, 0 )
		_SmoothnessValue( "SmoothnessValue", Range( 0, 1 ) ) = 0
		_MetallicValue( "MetallicValue", Range( 0, 1 ) ) = 0
		[Toggle( _ROUGHNESSTOGGLE_ON )] _RoughnessToggle( "RoughnessToggle", Float ) = 0
		_TextureOffset( "TextureOffset", Vector ) = ( 0, 0, 0, 0 )
		_Color( "Color", Color ) = ( 1, 1, 1, 1 )
		_EmissionMap( "_EmissionMap", 2D ) = "black" {}
		[HDR] _EmissionColor( "EmissionColor", Color ) = ( 1, 1, 1 )
		_EmissionIntensity( "EmissionIntensity", Float ) = 1
		[HideInInspector] _texcoord( "", 2D ) = "white" {}
		[HideInInspector] __dirty( "", Int ) = 1
		[Header(Forward Rendering Options)]
		[ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
		[ToggleOff] _GlossyReflections("Reflections", Float) = 1.0
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "DisableBatching" = "True" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.5
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#pragma shader_feature_local _LOCKLINESTOPIVOT_ON
		#pragma shader_feature_local _ROUGHNESSTOGGLE_ON
		#define ASE_VERSION 19912
		#ifdef UNITY_PASS_SHADOWCASTER
			#undef INTERNAL_DATA
			#undef WorldReflectionVector
			#undef WorldNormalVector
			#define INTERNAL_DATA half3 internalSurfaceTtoW0; half3 internalSurfaceTtoW1; half3 internalSurfaceTtoW2;
			#define WorldReflectionVector(data,normal) reflect (data.worldRefl, half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal)))
			#define WorldNormalVector(data,normal) half3(dot(data.internalSurfaceTtoW0,normal), dot(data.internalSurfaceTtoW1,normal), dot(data.internalSurfaceTtoW2,normal))
		#endif
		struct Input
		{
			float2 uv_texcoord;
			float3 worldNormal;
			INTERNAL_DATA
			float3 worldPos;
		};

		uniform float _LineHeightCM;
		uniform sampler2D _Normal;
		uniform float2 _TextureTiling;
		uniform float2 _TextureOffset;
		uniform float4 _Color;
		uniform sampler2D _Albedo;
		uniform float _LinesDarkenAmount;
		uniform float _LineBlend;
		uniform float _HeightOffset;
		uniform float _EmissionIntensity;
		uniform sampler2D _EmissionMap;
		uniform float3 _EmissionColor;
		uniform sampler2D _Metallic;
		uniform float _MetallicValue;
		uniform sampler2D _Smoothness;
		uniform float _SmoothnessValue;

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_TexCoord6 = i.uv_texcoord * _TextureTiling + _TextureOffset;
			o.Normal = UnpackNormal( tex2D( _Normal, uv_TexCoord6 ) );
			float3 temp_output_180_0 = ( _Color.rgb * tex2D( _Albedo, uv_TexCoord6 ).rgb );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float temp_output_18_0_g2 = ( ( _LineBlend * 0.5 ) * max( sqrt( max( ( 1.0 - ( ase_normalWS.y * ase_normalWS.y ) ), 1E-05 ) ), 0.001 ) );
			float3 ase_positionWS = i.worldPos;
			float4 transform41_g1 = mul(unity_ObjectToWorld,float4( 0,0,0,1 ));
			#ifdef _LOCKLINESTOPIVOT_ON
				float staticSwitch40_g1 = ( ase_positionWS.y - transform41_g1.y );
			#else
				float staticSwitch40_g1 = ase_positionWS.y;
			#endif
			float temp_output_10_0_g1 = ( ( staticSwitch40_g1 + _HeightOffset ) * ( 50.0 / _LineHeightCM ) );
			float temp_output_16_0_g1 = fwidth( temp_output_10_0_g1 );
			float smoothstepResult21_g1 = smoothstep( ( ( 0.5 - temp_output_18_0_g2 ) - temp_output_16_0_g1 ) , ( ( 0.5 + temp_output_18_0_g2 ) + temp_output_16_0_g1 ) , abs( (frac( temp_output_10_0_g1 )*2.0 + -1.0) ));
			float lerpResult37_g1 = lerp( smoothstepResult21_g1 , 0.5 , saturate( ( temp_output_16_0_g1 * 2.0 ) ));
			float _LinesAlpha240 = lerpResult37_g1;
			float3 lerpResult79 = lerp( temp_output_180_0 , ( temp_output_180_0 * ( 1.0 - _LinesDarkenAmount ) ) , _LinesAlpha240);
			float3 AlbedoOut147 = lerpResult79;
			o.Albedo = AlbedoOut147;
			o.Emission = ( _EmissionIntensity * ( tex2D( _EmissionMap, uv_TexCoord6 ).rgb * _EmissionColor ) );
			o.Metallic = ( tex2D( _Metallic, uv_TexCoord6 ).rgb * _MetallicValue ).x;
			float4 tex2DNode138 = tex2D( _Smoothness, uv_TexCoord6 );
			#ifdef _ROUGHNESSTOGGLE_ON
				float3 staticSwitch174 = ( 1.0 - tex2DNode138.rgb );
			#else
				float3 staticSwitch174 = tex2DNode138.rgb;
			#endif
			o.Smoothness = ( staticSwitch174 * _SmoothnessValue ).x;
			o.Alpha = 1;
		}

		ENDCG
		CGPROGRAM
		#pragma surface surf Standard keepalpha fullforwardshadows 

		ENDCG
		Pass
		{
			Name "ShadowCaster"
			Tags{ "LightMode" = "ShadowCaster" }
			ZWrite On
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma target 3.5
			#pragma multi_compile_shadowcaster
			#pragma multi_compile UNITY_PASS_SHADOWCASTER
			#pragma skip_variants FOG_LINEAR FOG_EXP FOG_EXP2
			#include "HLSLSupport.cginc"
			#if ( SHADER_API_D3D11 || SHADER_API_GLCORE || SHADER_API_GLES || SHADER_API_GLES3 || SHADER_API_METAL || SHADER_API_VULKAN )
				#define CAN_SKIP_VPOS
			#endif
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityPBSLighting.cginc"
			struct v2f
			{
				V2F_SHADOW_CASTER;
				float2 customPack1 : TEXCOORD1;
				float4 tSpace0 : TEXCOORD2;
				float4 tSpace1 : TEXCOORD3;
				float4 tSpace2 : TEXCOORD4;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			v2f vert( appdata_full v )
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID( v );
				UNITY_INITIALIZE_OUTPUT( v2f, o );
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO( o );
				UNITY_TRANSFER_INSTANCE_ID( v, o );
				Input customInputData;
				float3 worldPos = mul( unity_ObjectToWorld, v.vertex ).xyz;
				half3 worldNormal = UnityObjectToWorldNormal( v.normal );
				half3 worldTangent = UnityObjectToWorldDir( v.tangent.xyz );
				half tangentSign = v.tangent.w * unity_WorldTransformParams.w;
				half3 worldBinormal = cross( worldNormal, worldTangent ) * tangentSign;
				o.tSpace0 = float4( worldTangent.x, worldBinormal.x, worldNormal.x, worldPos.x );
				o.tSpace1 = float4( worldTangent.y, worldBinormal.y, worldNormal.y, worldPos.y );
				o.tSpace2 = float4( worldTangent.z, worldBinormal.z, worldNormal.z, worldPos.z );
				o.customPack1.xy = customInputData.uv_texcoord;
				o.customPack1.xy = v.texcoord;
				TRANSFER_SHADOW_CASTER_NORMALOFFSET( o )
				return o;
			}
			half4 frag( v2f IN
			#if !defined( CAN_SKIP_VPOS )
			, UNITY_VPOS_TYPE vpos : VPOS
			#endif
			) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID( IN );
				Input surfIN;
				UNITY_INITIALIZE_OUTPUT( Input, surfIN );
				surfIN.uv_texcoord = IN.customPack1.xy;
				float3 worldPos = float3( IN.tSpace0.w, IN.tSpace1.w, IN.tSpace2.w );
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = float3( IN.tSpace0.z, IN.tSpace1.z, IN.tSpace2.z );
				surfIN.internalSurfaceTtoW0 = IN.tSpace0.xyz;
				surfIN.internalSurfaceTtoW1 = IN.tSpace1.xyz;
				surfIN.internalSurfaceTtoW2 = IN.tSpace2.xyz;
				SurfaceOutputStandard o;
				UNITY_INITIALIZE_OUTPUT( SurfaceOutputStandard, o )
				surf( surfIN, o );
				#if defined( CAN_SKIP_VPOS )
				float2 vpos = IN.pos;
				#endif
				SHADOW_CASTER_FRAGMENT( IN )
			}
			ENDCG
		}
	}
	Fallback Off
	CustomEditor "GolfCourseLinesMasterGUI"
}
/*ASEBEGIN
Version=19912
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":150,"pos":[3984,32],"params":["Inherit","False","Property","_TextureTiling","Texture Tiling","11","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1","1,1","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor","id":178,"pos":[3984,160],"params":["Inherit","False","Property","_TextureOffset","TextureOffset","15","0","Create","True","0","0","0","False","0","False","Object","-1","","0,0","0,0","0","3","FLOAT2","0","FLOAT","1","FLOAT","2"]}
{"type":"AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor","id":149,"pos":[4624,-880],"params":["Inherit","False","1949.683","616.6039","","8","182","181","147","79","146","78","47","189","AlbedoDarkness","1,1,1,1","0","0"]}
{"type":"AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor","id":6,"pos":[4192,16],"params":["Inherit","True","0","-1","2","3","2","SAMPLER2D","","False","0","FLOAT2","1,1","False","1","FLOAT2","0,0","False","5","FLOAT2","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":1,"pos":[4736,-576],"params":["Inherit","True","Property","_Albedo","Albedo","6","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.ColorNode, AmplifyShaderEditor","id":179,"pos":[4800,-784],"params":["Inherit","False","Property","_Color","Color","16","0","Create","True","0","0","0","False","0","False","Object","-1","","1,1,1,1","0,0,0,0","True","True","0","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":47,"pos":[5232,-512],"params":["Inherit","False","Property","_LinesDarkenAmount","_LinesDarkenAmount","7","0","Create","True","0","0","0","True","0","False","Object","-1","","0.25","0.05423951","0","1","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor","id":270,"pos":[3936,-384],"params":["Inherit","False","OpenPuttLines","0","","1","5b7dadceb1064e74c9a5f3f739983153","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":180,"pos":[5120,-624],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.WireNode, AmplifyShaderEditor","id":181,"pos":[5504,-704],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":138,"pos":[4576,704],"params":["Inherit","True","Property","_Smoothness","Smoothness","9","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":172,"pos":[4880,800],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor","id":189,"pos":[5552,-592],"params":["Inherit","False","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":240,"pos":[4224,-384],"params":["Inherit","True","_LinesAlpha","-1","True","1","0","FLOAT","0","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":78,"pos":[5760,-720],"params":["Inherit","True","2","2","0","FLOAT3","0,0,0","False","1","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.WireNode, AmplifyShaderEditor","id":182,"pos":[5488,-784],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":164,"pos":[5008,896],"params":["Inherit","False","Property","_SmoothnessValue","SmoothnessValue","12","0","Create","True","0","0","0","False","0","False","Object","-1","","0","0","0","1","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor","id":174,"pos":[5040,720],"params":["Inherit","False","Property","_RoughnessToggle","RoughnessToggle","14","0","Create","True","0","0","0","False","0","False","","0","0","0","True","","Toggle","2","Key0","Key1","Create","True","True","All","9","1","FLOAT3","0,0,0","False","0","FLOAT3","0,0,0","False","2","FLOAT3","0,0,0","False","3","FLOAT3","0,0,0","False","4","FLOAT3","0,0,0","False","5","FLOAT3","0,0,0","False","6","FLOAT3","0,0,0","False","7","FLOAT3","0,0,0","False","8","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":146,"pos":[5776,-496],"params":["Inherit","False","240","_LinesAlpha","1","0","OBJECT","","False","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.LerpOp, AmplifyShaderEditor","id":79,"pos":[6016,-800],"params":["Inherit","True","3","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","2","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":140,"pos":[4576,416],"params":["Inherit","True","Property","_Metallic","Metallic","10","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","white","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":167,"pos":[4576,608],"params":["Inherit","False","Property","_MetallicValue","MetallicValue","13","0","Create","True","0","0","0","False","0","False","Object","-1","","0","1","0","1","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":165,"pos":[5312,800],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":183,"pos":[4576,16],"params":["Inherit","True","Property","_EmissionMap","_EmissionMap","17","0","Create","True","0","0","0","False","0","False","","183","None","None","True","0","False","black","Auto","False","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","COLOR","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.ColorNode, AmplifyShaderEditor","id":185,"pos":[4640,208],"params":["Inherit","False","Property","_EmissionColor","EmissionColor","18","1","[HDR]","Create","True","0","0","0","False","0","False","Object","-1","","1,1,1,1","1,1,1,0","True","False","0","6","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor","id":147,"pos":[6320,-800],"params":["Inherit","False","AlbedoOut","-1","True","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.WireNode, AmplifyShaderEditor","id":177,"pos":[5472,672],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":166,"pos":[5024,416],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT","0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":184,"pos":[4928,128],"params":["Inherit","False","2","2","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor","id":186,"pos":[4912,-16],"params":["Inherit","False","Property","_EmissionIntensity","EmissionIntensity","19","0","Create","True","0","0","0","False","0","False","Object","-1","","1","1","0","0","0","1","FLOAT","0"]}
{"type":"AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor","id":148,"pos":[5024,-240],"params":["Inherit","False","147","AlbedoOut","1","0","OBJECT","","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.WireNode, AmplifyShaderEditor","id":175,"pos":[5248,208],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.WireNode, AmplifyShaderEditor","id":176,"pos":[5472,144],"params":["Inherit","False","1","0","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor","id":134,"pos":[4576,-208],"params":["Inherit","True","Property","_Normal","Normal","8","0","Create","True","0","0","0","False","0","False","","-1","None","None","True","0","False","bump","Auto","True","Object","-1","Auto","Texture2D","False","8","0","SAMPLER2D","","False","1","FLOAT2","0,0","False","2","FLOAT","0","False","3","FLOAT2","0,0","False","4","FLOAT2","0,0","False","5","FLOAT","1","False","6","FLOAT","0","False","7","SAMPLERSTATE","","False","6","FLOAT3","0","FLOAT","1","FLOAT","2","FLOAT","3","FLOAT","4","FLOAT3","5"]}
{"type":"AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor","id":188,"pos":[5184,-32],"params":["Inherit","False","2","2","0","FLOAT","0","False","1","FLOAT3","0,0,0","False","1","FLOAT3","0"]}
{"type":"AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor","id":0,"pos":[5568,-128],"params":["Float","False","True","-1","3","GolfCourseLinesMasterGUI","0","0","Standard","OpenPutt/GolfCourse/LinesStandard","False","False","False","False","False","False","False","False","False","False","False","False","False","True","False","False","False","False","True","True","False","Back","0","False","","0","False","","False","0","False","","0","False","","False","0","0","False","","0","Opaque","0.5","True","True","0","False","Opaque","","Geometry","All","12","all","True","True","True","True","0","False","","False","0","False","","255","False","","255","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","0","False","","False","2","15","10","25","False","0.5","True","0","0","False","","0","False","","0","0","False","","0","False","","0","False","","0","False","","0","False","0","0,0,0,0","VertexOffset","True","False","Cylindrical","False","True","Relative","0","","-1","-1","-1","-1","0","False","0","0","False","","-1","0","False","","0","0","0","False","0.1","False","","0","False","","False","17","0","FLOAT3","0,0,0","False","1","FLOAT3","0,0,0","False","2","FLOAT3","0,0,0","False","3","FLOAT","0","False","4","FLOAT","0","False","5","FLOAT","0","False","6","FLOAT3","0,0,0","False","7","FLOAT3","0,0,0","False","8","FLOAT","0","False","9","FLOAT","0","False","10","FLOAT","0","False","13","FLOAT3","0,0,0","False","11","FLOAT3","0,0,0","False","12","FLOAT3","0,0,0","False","16","FLOAT4","0,0,0,0","False","14","FLOAT4","0,0,0,0","False","15","FLOAT3","0,0,0","False","0"]}
{"wire":[6,0,150,0]}
{"wire":[6,1,178,0]}
{"wire":[1,1,6,0]}
{"wire":[180,0,179,5]}
{"wire":[180,1,1,5]}
{"wire":[181,0,180,0]}
{"wire":[138,1,6,0]}
{"wire":[172,0,138,5]}
{"wire":[189,0,47,0]}
{"wire":[240,0,270,0]}
{"wire":[78,0,181,0]}
{"wire":[78,1,189,0]}
{"wire":[182,0,180,0]}
{"wire":[174,1,138,5]}
{"wire":[174,0,172,0]}
{"wire":[79,0,182,0]}
{"wire":[79,1,78,0]}
{"wire":[79,2,146,0]}
{"wire":[140,1,6,0]}
{"wire":[165,0,174,0]}
{"wire":[165,1,164,0]}
{"wire":[183,1,6,0]}
{"wire":[147,0,79,0]}
{"wire":[177,0,165,0]}
{"wire":[166,0,140,5]}
{"wire":[166,1,167,0]}
{"wire":[184,0,183,5]}
{"wire":[184,1,185,0]}
{"wire":[175,0,166,0]}
{"wire":[176,0,177,0]}
{"wire":[134,1,6,0]}
{"wire":[188,0,186,0]}
{"wire":[188,1,184,0]}
{"wire":[0,0,148,0]}
{"wire":[0,1,134,0]}
{"wire":[0,2,188,0]}
{"wire":[0,3,175,0]}
{"wire":[0,4,176,0]}
ASEEND*/
//CHKSM=1498A3F0623301B5078CDFFA447A410AD093986B