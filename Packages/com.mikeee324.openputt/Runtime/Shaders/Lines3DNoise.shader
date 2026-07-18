// Upgrade NOTE: upgraded instancing buffer 'OpenPuttGolfCourseLines3DNoise' to new syntax.

// Made with Amplify Shader Editor v1.9.9.9
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OpenPutt/GolfCourse/Lines3DNoise"
{
	Properties
	{
		_SmallNoiseAmaount( "SmallNoiseAmaount", Range( 0, 500 ) ) = 200
		_BigNoiseAmaount( "BigNoiseAmaount", Range( 0, 3 ) ) = 2
		_BigNoiseStrength( "BigNoiseStrength", Range( 0, 1 ) ) = 0.33
		_AllNoiseStrength( "AllNoiseStrength", Range( 0, 1 ) ) = 0.5
		_smooothstepmin( "smooothstepmin", Range( 0, 1 ) ) = 0.1
		_smooothstepmax( "smooothstepmax", Range( 0, 1 ) ) = 0.95
		[Toggle( _LOCKNOISETOOBJECT_ON )] _LockNoiseToObject( "LockNoiseToObject", Float ) = 0
		_HeightOffset( "HeightOffset", Range( -0.3, 0.3 ) ) = 0
		_LineBlend( "LineBlend", Range( 0, 1 ) ) = 0.2
		_LineHeightCM( "_LineHeightCM", Range( 0.5, 30 ) ) = 3
		_Color0( "Color 0", Color ) = ( 0.2, 0.6509804, 0.1019608, 1 )
		_LinesDarkenAmount( "LinesDarkenAmount", Range( 0, 1 ) ) = 0.17
		_Metallic( "Metallic", Range( 0, 1 ) ) = 0
		_Smoothness( "Smoothness", Range( 0, 1 ) ) = 0
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags{ "RenderType" = "Opaque"  "Queue" = "Geometry+0" "DisableBatching" = "True" }
		Cull Back
		CGINCLUDE
		#include "UnityPBSLighting.cginc"
		#include "Lighting.cginc"
		#pragma target 4.6
		#pragma multi_compile_instancing
		#pragma shader_feature_local _LOCKNOISETOOBJECT_ON
		#define ASE_VERSION 19909
		struct Input
		{
			float3 worldNormal;
			float3 worldPos;
			float4 ase_positionOS4f;
		};

		uniform float _LineHeightCM;
		uniform float _LineBlend;
		uniform float _HeightOffset;
		uniform float _smooothstepmin;
		uniform float _smooothstepmax;
		uniform float _AllNoiseStrength;
		uniform float _SmallNoiseAmaount;
		uniform float _BigNoiseStrength;
		uniform float _BigNoiseAmaount;
		uniform float4 _Color0;
		uniform float _Metallic;
		uniform float _Smoothness;

		UNITY_INSTANCING_BUFFER_START(OpenPuttGolfCourseLines3DNoise)
			UNITY_DEFINE_INSTANCED_PROP(float, _LinesDarkenAmount)
#define _LinesDarkenAmount_arr OpenPuttGolfCourseLines3DNoise
		UNITY_INSTANCING_BUFFER_END(OpenPuttGolfCourseLines3DNoise)


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
			float3 ase_normalWS = i.worldNormal;
			float temp_output_18_0_g7 = ( ( _LineBlend * 0.5 ) * max( sqrt( max( ( 1.0 - ( ase_normalWS.y * ase_normalWS.y ) ), 1E-05 ) ), 0.001 ) );
			float3 ase_positionWS = i.worldPos;
			float temp_output_10_0_g6 = ( ( ase_positionWS.y + _HeightOffset ) * ( 50.0 / _LineHeightCM ) );
			float temp_output_16_0_g6 = fwidth( temp_output_10_0_g6 );
			float smoothstepResult21_g6 = smoothstep( ( ( 0.5 - temp_output_18_0_g7 ) - temp_output_16_0_g6 ) , ( ( 0.5 + temp_output_18_0_g7 ) + temp_output_16_0_g6 ) , abs( (frac( temp_output_10_0_g6 )*2.0 + -1.0) ));
			float lerpResult37_g6 = lerp( smoothstepResult21_g6 , 0.5 , saturate( ( temp_output_16_0_g6 * 2.0 ) ));
			float _LinesAlpha48 = lerpResult37_g6;
			float _LinesDarkenAmount_Instance = UNITY_ACCESS_INSTANCED_PROP(_LinesDarkenAmount_arr, _LinesDarkenAmount);
			float clampResult60 = clamp( ( 1.0 - _LinesAlpha48 ) , ( 1.0 - _LinesDarkenAmount_Instance ) , 1.0 );
			float3 ase_positionOS = i.ase_positionOS4f.xyz;
			#ifdef _LOCKNOISETOOBJECT_ON
				float3 staticSwitch9_g8 = ase_positionOS;
			#else
				float3 staticSwitch9_g8 = ase_positionWS;
			#endif
			float3 temp_output_13_0_g8 = ( _SmallNoiseAmaount * staticSwitch9_g8 );
			float simplePerlin3D15_g8 = snoise( temp_output_13_0_g8 );
			simplePerlin3D15_g8 = simplePerlin3D15_g8*0.5 + 0.5;
			float simplePerlin3D7_g8 = snoise( ( staticSwitch9_g8 * _BigNoiseAmaount ) );
			simplePerlin3D7_g8 = simplePerlin3D7_g8*0.5 + 0.5;
			float smoothstepResult29_g8 = smoothstep( _smooothstepmin , _smooothstepmax , ( 1.0 - ( _AllNoiseStrength * ( simplePerlin3D15_g8 * ( 1.0 - ( _BigNoiseStrength * simplePerlin3D7_g8 ) ) ) ) ));
			float3 break21_g8 = fwidth( temp_output_13_0_g8 );
			float lerpResult40_g8 = lerp( smoothstepResult29_g8 , 0.95 , saturate( ( max( max( break21_g8.x, break21_g8.y ), break21_g8.z ) * 0.5 ) ));
			float Noisemap50 = lerpResult40_g8;
			float3 FinalMix62 = ( clampResult60 * ( Noisemap50 * _Color0.rgb ) );
			o.Albedo = FinalMix62;
			o.Metallic = _Metallic;
			o.Smoothness = _Smoothness;
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
			#pragma target 4.6
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
				float4 customPack1 : TEXCOORD1;
				float3 worldPos : TEXCOORD2;
				float3 worldNormal : TEXCOORD3;
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
				o.worldNormal = worldNormal;
				o.customPack1.xyzw = customInputData.ase_positionOS4f;
				o.worldPos = worldPos;
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
				surfIN.ase_positionOS4f = IN.customPack1.xyzw;
				float3 worldPos = IN.worldPos;
				half3 worldViewDir = normalize( UnityWorldSpaceViewDir( worldPos ) );
				surfIN.worldPos = worldPos;
				surfIN.worldNormal = IN.worldNormal;
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
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;243;-544,-768;Inherit;False;OpenPuttLines;8;;6;5b7dadceb1064e74c9a5f3f739983153;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;78;-1008,-1680;Inherit;False;1268;795;Comment;11;56;7;52;54;61;59;60;8;53;62;238;Albedomixer;1,1,1,1;0;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;48;-256,-768;Inherit;True;_LinesAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FunctionNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;244;-544,-512;Inherit;False;OpenPuttNoise;0;;8;68bb02bd3a8237d478884f6d11659113;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;50;-256,-512;Inherit;True;Noisemap;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;56;-960,-1440;Inherit;False;InstancedProperty;_LinesDarkenAmount;LinesDarkenAmount;14;0;Create;True;0;0;0;False;0;False;0.17;0.153;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;54;-896,-1632;Inherit;True;48;_LinesAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;7;-864,-1120;Inherit;False;Property;_Color0;Color 0;13;0;Create;True;0;0;0;False;0;False;0.2,0.6509804,0.1019608,1;0,1,0.03738308,1;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;52;-832,-1248;Inherit;False;50;Noisemap;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;61;-656,-1440;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;59;-656,-1360;Inherit;False;Constant;_Float1;Float 0;11;0;Create;True;0;0;0;False;0;False;1;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;238;-656,-1568;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ClampOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;60;-480,-1472;Inherit;False;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;8;-592,-1248;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;53;-256,-1440;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;62;16,-1440;Inherit;True;FinalMix;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;63;256,-752;Inherit;True;62;FinalMix;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;112;672,-656;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;101;688,-672;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;110;848,-848;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;102;880,-1040;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;107;752,-1072;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;105;688,-976;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;104;688,-976;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;108;624,-1072;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;103;496,-1040;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;109;528,-848;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;106;688,-672;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;66;192,-480;Inherit;False;Property;_Smoothness;Smoothness;16;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;65;192,-560;Inherit;False;Property;_Metallic;Metallic;15;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;111;704,-656;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;896,-624;Float;False;True;-1;6;GolfCourseLinesMasterGUI;0;0;Standard;OpenPutt/GolfCourse/Lines3DNoise;False;False;False;False;False;False;False;False;False;False;False;False;False;True;False;False;False;False;False;False;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;48;0;243;0
WireConnection;50;0;244;0
WireConnection;61;0;56;0
WireConnection;238;0;54;0
WireConnection;60;0;238;0
WireConnection;60;1;61;0
WireConnection;60;2;59;0
WireConnection;8;0;52;0
WireConnection;8;1;7;5
WireConnection;53;0;60;0
WireConnection;53;1;8;0
WireConnection;62;0;53;0
WireConnection;112;0;63;0
WireConnection;101;0;112;0
WireConnection;110;0;101;0
WireConnection;102;0;110;0
WireConnection;107;0;102;0
WireConnection;105;0;107;0
WireConnection;104;0;105;0
WireConnection;108;0;104;0
WireConnection;103;0;108;0
WireConnection;109;0;103;0
WireConnection;106;0;109;0
WireConnection;111;0;106;0
WireConnection;0;0;111;0
WireConnection;0;3;65;0
WireConnection;0;4;66;0
ASEEND*/
//CHKSM=40D47F84FD453EB1BCFE674AC8FED8B7AAEE0F8E