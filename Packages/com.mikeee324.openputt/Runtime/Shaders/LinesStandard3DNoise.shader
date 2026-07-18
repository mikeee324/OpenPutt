// Made with Amplify Shader Editor v1.9.9.9
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OpenPutt/GolfCourse/LinesStandard3DNoise"
{
	Properties
	{
		_HeightOffset( "HeightOffset", Range( -0.3, 0.3 ) ) = 0
		_LineBlend( "LineBlend", Range( 0, 1 ) ) = 0.2
		_LineHeightCM( "_LineHeightCM", Range( 0.5, 30 ) ) = 3
		_SmallNoiseAmaount( "SmallNoiseAmaount", Range( 0, 500 ) ) = 200
		_BigNoiseAmaount( "BigNoiseAmaount", Range( 0, 3 ) ) = 2
		_BigNoiseStrength( "BigNoiseStrength", Range( 0, 1 ) ) = 0.33
		_AllNoiseStrength( "AllNoiseStrength", Range( 0, 1 ) ) = 0.5
		_smooothstepmin( "smooothstepmin", Range( 0, 1 ) ) = 0.1
		_smooothstepmax( "smooothstepmax", Range( 0, 1 ) ) = 0.95
		[Toggle( _LOCKNOISETOOBJECT_ON )] _LockNoiseToObject( "LockNoiseToObject", Float ) = 0
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
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "IsEmissive" = "true"  }
		Cull Back
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 3.5
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#pragma shader_feature_local _LOCKNOISETOOBJECT_ON
		#pragma shader_feature_local _ROUGHNESSTOGGLE_ON
		#define ASE_VERSION 19909
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
			float4 ase_positionOS4f;
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
		uniform float _smooothstepmin;
		uniform float _smooothstepmax;
		uniform float _AllNoiseStrength;
		uniform float _SmallNoiseAmaount;
		uniform float _BigNoiseStrength;
		uniform float _BigNoiseAmaount;
		uniform float _EmissionIntensity;
		uniform sampler2D _EmissionMap;
		uniform float3 _EmissionColor;
		uniform sampler2D _Metallic;
		uniform float _MetallicValue;
		uniform sampler2D _Smoothness;
		uniform float _SmoothnessValue;


		float3 mod3D289( float3 x ) { return x - floor( x / 289.0 ) * 289.0; }

		float4 mod3D289( float4 x ) { return x - floor( x / 289.0 ) * 289.0; }

		float4 permute( float4 x ) { return mod3D289( ( x * 34.0 + 1.0 ) * x ); }

		float4 taylorInvSqrt( float4 r ) { return 1.79284291400159 - r * 0.85373472095314; }

		float snoise( float3 v )
		{
			const float2 C = float2( 1.0 / 6.0, 1.0 / 3.0 );
			float3 i = floor( v + dot( v, C.yyy ) );
			float3 x0 = v - i + dot( i, C.xxx );
			float3 g = step( x0.yzx, x0.xyz );
			float3 l = 1.0 - g;
			float3 i1 = min( g.xyz, l.zxy );
			float3 i2 = max( g.xyz, l.zxy );
			float3 x1 = x0 - i1 + C.xxx;
			float3 x2 = x0 - i2 + C.yyy;
			float3 x3 = x0 - 0.5;
			i = mod3D289( i);
			float4 p = permute( permute( permute( i.z + float4( 0.0, i1.z, i2.z, 1.0 ) ) + i.y + float4( 0.0, i1.y, i2.y, 1.0 ) ) + i.x + float4( 0.0, i1.x, i2.x, 1.0 ) );
			float4 j = p - 49.0 * floor( p / 49.0 );  // mod(p,7*7)
			float4 x_ = floor( j / 7.0 );
			float4 y_ = floor( j - 7.0 * x_ );  // mod(j,N)
			float4 x = ( x_ * 2.0 + 0.5 ) / 7.0 - 1.0;
			float4 y = ( y_ * 2.0 + 0.5 ) / 7.0 - 1.0;
			float4 h = 1.0 - abs( x ) - abs( y );
			float4 b0 = float4( x.xy, y.xy );
			float4 b1 = float4( x.zw, y.zw );
			float4 s0 = floor( b0 ) * 2.0 + 1.0;
			float4 s1 = floor( b1 ) * 2.0 + 1.0;
			float4 sh = -step( h, 0.0 );
			float4 a0 = b0.xzyw + s0.xzyw * sh.xxyy;
			float4 a1 = b1.xzyw + s1.xzyw * sh.zzww;
			float3 g0 = float3( a0.xy, h.x );
			float3 g1 = float3( a0.zw, h.y );
			float3 g2 = float3( a1.xy, h.z );
			float3 g3 = float3( a1.zw, h.w );
			float4 norm = taylorInvSqrt( float4( dot( g0, g0 ), dot( g1, g1 ), dot( g2, g2 ), dot( g3, g3 ) ) );
			g0 *= norm.x;
			g1 *= norm.y;
			g2 *= norm.z;
			g3 *= norm.w;
			float4 m = max( 0.6 - float4( dot( x0, x0 ), dot( x1, x1 ), dot( x2, x2 ), dot( x3, x3 ) ), 0.0 );
			m = m* m;
			m = m* m;
			float4 px = float4( dot( x0, g0 ), dot( x1, g1 ), dot( x2, g2 ), dot( x3, g3 ) );
			return 42.0 * dot( m, px);
		}


		void vertexDataFunc( inout appdata_full v, out Input o )
		{
			UNITY_INITIALIZE_OUTPUT( Input, o );
			float4 ase_positionOS4f = v.vertex;
			o.ase_positionOS4f = ase_positionOS4f;
		}

		void surf( Input i , inout SurfaceOutputStandard o )
		{
			float2 uv_TexCoord6 = i.uv_texcoord * _TextureTiling + _TextureOffset;
			o.Normal = UnpackNormal( tex2D( _Normal, uv_TexCoord6 ) );
			float3 temp_output_180_0 = ( _Color.rgb * tex2D( _Albedo, uv_TexCoord6 ).rgb );
			float3 ase_normalWS = WorldNormalVector( i, float3( 0, 0, 1 ) );
			float temp_output_18_0_g8 = ( ( _LineBlend * 0.5 ) * max( sqrt( max( ( 1.0 - ( ase_normalWS.y * ase_normalWS.y ) ), 1E-05 ) ), 0.001 ) );
			float3 ase_positionWS = i.worldPos;
			float temp_output_10_0_g7 = ( ( ase_positionWS.y + _HeightOffset ) * ( 50.0 / _LineHeightCM ) );
			float temp_output_16_0_g7 = fwidth( temp_output_10_0_g7 );
			float smoothstepResult21_g7 = smoothstep( ( ( 0.5 - temp_output_18_0_g8 ) - temp_output_16_0_g7 ) , ( ( 0.5 + temp_output_18_0_g8 ) + temp_output_16_0_g7 ) , abs( (frac( temp_output_10_0_g7 )*2.0 + -1.0) ));
			float lerpResult37_g7 = lerp( smoothstepResult21_g7 , 0.5 , saturate( ( temp_output_16_0_g7 * 2.0 ) ));
			float _LinesAlpha355 = lerpResult37_g7;
			float3 lerpResult79 = lerp( temp_output_180_0 , ( temp_output_180_0 * ( 1.0 - _LinesDarkenAmount ) ) , _LinesAlpha355);
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			#ifdef _LOCKNOISETOOBJECT_ON
				float3 staticSwitch9_g9 = ase_positionOS;
			#else
				float3 staticSwitch9_g9 = ase_positionWS;
			#endif
			float3 temp_output_13_0_g9 = ( _SmallNoiseAmaount * staticSwitch9_g9 );
			float simplePerlin3D15_g9 = snoise( temp_output_13_0_g9 );
			simplePerlin3D15_g9 = simplePerlin3D15_g9*0.5 + 0.5;
			float simplePerlin3D7_g9 = snoise( ( staticSwitch9_g9 * _BigNoiseAmaount ) );
			simplePerlin3D7_g9 = simplePerlin3D7_g9*0.5 + 0.5;
			float smoothstepResult29_g9 = smoothstep( _smooothstepmin , _smooothstepmax , ( 1.0 - ( _AllNoiseStrength * ( simplePerlin3D15_g9 * ( 1.0 - ( _BigNoiseStrength * simplePerlin3D7_g9 ) ) ) ) ));
			float3 break21_g9 = fwidth( temp_output_13_0_g9 );
			float lerpResult40_g9 = lerp( smoothstepResult29_g9 , 0.95 , saturate( ( max( max( break21_g9.x, break21_g9.y ), break21_g9.z ) * 0.5 ) ));
			float Noisemap327 = lerpResult40_g9;
			float3 AlbedoOut147 = ( lerpResult79 * Noisemap327 );
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
		#pragma surface surf Standard keepalpha fullforwardshadows vertex:vertexDataFunc 

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
				float4 customPack2 : TEXCOORD2;
				float4 tSpace0 : TEXCOORD3;
				float4 tSpace1 : TEXCOORD4;
				float4 tSpace2 : TEXCOORD5;
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
				vertexDataFunc( v, customInputData );
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
				o.customPack2.xyzw = customInputData.ase_positionOS4f;
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
				surfIN.ase_positionOS4f = IN.customPack2.xyzw;
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
Version=19909
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;150;3984,32;Inherit;False;Property;_TextureTiling;Texture Tiling;18;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;178;3984,160;Inherit;False;Property;_TextureOffset;TextureOffset;22;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;6;4192,16;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;149;4624,-880;Inherit;False;1949.683;616.6039;;9;182;181;147;79;146;78;47;189;215;AlbedoDarkness;1,1,1,1;0;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;1;4736,-576;Inherit;True;Property;_Albedo;Albedo;13;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;179;4800,-784;Inherit;False;Property;_Color;Color;23;0;Create;True;0;0;0;False;0;False;1,1,1,1;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;5232,-512;Inherit;False;Property;_LinesDarkenAmount;_LinesDarkenAmount;14;0;Create;True;0;0;0;True;0;False;0.25;0.05423951;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;444;3168,-768;Inherit;False;OpenPuttLines;0;;7;5b7dadceb1064e74c9a5f3f739983153;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;180;5120,-624;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;181;5504,-704;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;189;5552,-592;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;355;3456,-768;Inherit;True;_LinesAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;445;3168,-512;Inherit;False;OpenPuttNoise;5;;9;68bb02bd3a8237d478884f6d11659113;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;138;4576,704;Inherit;True;Property;_Smoothness;Smoothness;16;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;172;4880,800;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;78;5760,-720;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;182;5488,-784;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;146;5776,-496;Inherit;True;355;_LinesAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;327;3456,-512;Inherit;True;Noisemap;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;164;5008,896;Inherit;False;Property;_SmoothnessValue;SmoothnessValue;19;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;174;5040,720;Inherit;False;Property;_RoughnessToggle;RoughnessToggle;21;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;79;6016,-800;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;215;6064,-480;Inherit;True;327;Noisemap;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;140;4576,416;Inherit;True;Property;_Metallic;Metallic;17;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;167;4576,608;Inherit;False;Property;_MetallicValue;MetallicValue;20;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;165;5312,800;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;183;4576,16;Inherit;True;Property;_EmissionMap;_EmissionMap;24;0;Create;True;0;0;0;False;0;False;183;None;None;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;185;4640,208;Inherit;False;Property;_EmissionColor;EmissionColor;25;1;[HDR];Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,0;True;False;0;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;216;6288,-672;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;177;5472,672;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;166;5024,416;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;184;4928,128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;186;4912,-16;Inherit;False;Property;_EmissionIntensity;EmissionIntensity;26;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;147;6352,-528;Inherit;True;AlbedoOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;148;5024,-240;Inherit;False;147;AlbedoOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;175;5248,208;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;176;5472,144;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;134;4576,-208;Inherit;True;Property;_Normal;Normal;15;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;188;5184,-32;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;5568,-128;Float;False;True;-1;3;GolfCourseLinesMasterGUI;0;0;Standard;OpenPutt/GolfCourse/LinesStandard3DNoise;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;6;0;150;0
WireConnection;6;1;178;0
WireConnection;1;1;6;0
WireConnection;180;0;179;5
WireConnection;180;1;1;5
WireConnection;181;0;180;0
WireConnection;189;0;47;0
WireConnection;355;0;444;0
WireConnection;138;1;6;0
WireConnection;172;0;138;5
WireConnection;78;0;181;0
WireConnection;78;1;189;0
WireConnection;182;0;180;0
WireConnection;327;0;445;0
WireConnection;174;1;138;5
WireConnection;174;0;172;0
WireConnection;79;0;182;0
WireConnection;79;1;78;0
WireConnection;79;2;146;0
WireConnection;140;1;6;0
WireConnection;165;0;174;0
WireConnection;165;1;164;0
WireConnection;183;1;6;0
WireConnection;216;0;79;0
WireConnection;216;1;215;0
WireConnection;177;0;165;0
WireConnection;166;0;140;5
WireConnection;166;1;167;0
WireConnection;184;0;183;5
WireConnection;184;1;185;0
WireConnection;147;0;216;0
WireConnection;175;0;166;0
WireConnection;176;0;177;0
WireConnection;134;1;6;0
WireConnection;188;0;186;0
WireConnection;188;1;184;0
WireConnection;0;0;148;0
WireConnection;0;1;134;0
WireConnection;0;2;188;0
WireConnection;0;3;175;0
WireConnection;0;4;176;0
ASEEND*/
//CHKSM=513D40CAD9D36644D9FD51479FC27BB3E41C2905