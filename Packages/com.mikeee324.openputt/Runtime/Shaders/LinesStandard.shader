// Made with Amplify Shader Editor v1.9.9.8
// Available at the Unity Asset Store - http://u3d.as/y3X 
Shader "OpenPutt/GolfCourse/LinesStandard"
{
	Properties
	{
		_Albedo( "Albedo", 2D ) = "white" {}
		_DarkenAmount( "_DarkenAmount", Range( 0, 1 ) ) = 0.25
		_Normal( "Normal", 2D ) = "bump" {}
		_Smoothness( "Smoothness", 2D ) = "white" {}
		_Metallic( "Metallic", 2D ) = "white" {}
		_TextureTiling( "Texture Tiling", Vector ) = ( 1, 1, 0, 0 )
		_BlendMax( "_BlendMax", Range( 0.5, 1 ) ) = 0.6
		_LineHeightCM( "_LineHeightCM", Range( 0.5, 30 ) ) = 3
		_BlendMin( "_BlendMin", Range( 0, 0.5 ) ) = 0.4
		_SmoothnessValue( "SmoothnessValue", Range( 0, 1 ) ) = 0
		_MetallicValue( "MetallicValue", Range( 0, 1 ) ) = 0
		_HeightOffset( "HeightOffset", Range( -0.1, 0.1 ) ) = 0
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
		CGPROGRAM
		#pragma target 3.5
		#pragma shader_feature _SPECULARHIGHLIGHTS_OFF
		#pragma shader_feature _GLOSSYREFLECTIONS_OFF
		#pragma shader_feature_local _ROUGHNESSTOGGLE_ON
		#define ASE_VERSION 19908
		#pragma surface surf Standard keepalpha addshadow fullforwardshadows 
		struct Input
		{
			float2 uv_texcoord;
			float3 worldPos;
		};

		uniform sampler2D _Normal;
		uniform float2 _TextureTiling;
		uniform float2 _TextureOffset;
		uniform float4 _Color;
		uniform sampler2D _Albedo;
		uniform float _DarkenAmount;
		uniform float _BlendMin;
		uniform float _BlendMax;
		uniform float _HeightOffset;
		uniform float _LineHeightCM;
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
			float3 ase_positionWS = i.worldPos;
			float smoothstepResult161 = smoothstep( _BlendMin , _BlendMax , abs( (frac( ( ( ase_positionWS.y + _HeightOffset ) * ( 100.0 / _LineHeightCM ) ) )*2.0 + -1.0) ));
			float _LinesAlpha144 = smoothstepResult161;
			float3 lerpResult79 = lerp( temp_output_180_0 , ( temp_output_180_0 * ( 1.0 - _DarkenAmount ) ) , _LinesAlpha144);
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
	}
	Fallback Off
	CustomEditor "LinesUnityShaderGUI"
}
/*ASEBEGIN
Version=19908
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;151;1968,-576;Inherit;False;Constant;_100;100;30;0;Create;True;0;0;0;False;0;False;100;0;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;152;1888,-480;Inherit;False;Property;_LineHeightCM;_LineHeightCM;7;0;Create;True;0;0;0;True;0;False;3;10;0.5;30;0;1;FLOAT;0
Node;AmplifyShaderEditor.WorldPosInputsNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;154;2000,-880;Inherit;False;0;4;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;169;1904,-736;Inherit;False;Property;_HeightOffset;HeightOffset;11;0;Create;True;0;0;0;False;0;False;0;0;-0.1;0.1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleDivideOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;153;2176,-560;Inherit;False;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleAddOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;170;2208,-800;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;155;2416,-672;Inherit;True;2;2;0;FLOAT;0;False;1;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.FractNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;156;2640,-672;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.ScaleAndOffsetNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;157;2832,-672;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;2;False;2;FLOAT;-1;False;1;FLOAT;0
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;150;3984,32;Inherit;False;Property;_TextureTiling;Texture Tiling;5;0;Create;True;0;0;0;False;0;False;1,1;1,1;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.Vector2Node, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;178;3984,160;Inherit;False;Property;_TextureOffset;TextureOffset;13;0;Create;True;0;0;0;False;0;False;0,0;0,0;0;3;FLOAT2;0;FLOAT;1;FLOAT;2
Node;AmplifyShaderEditor.AbsOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;158;3088,-672;Inherit;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;159;3232,-384;Inherit;False;Property;_BlendMax;_BlendMax;6;0;Create;True;0;0;0;True;0;False;0.6;1;0.5;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;160;3232,-448;Inherit;False;Property;_BlendMin;_BlendMin;8;0;Create;True;0;0;0;True;0;False;0.4;0;0;0.5;0;1;FLOAT;0
Node;AmplifyShaderEditor.TextureCoordinatesNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;6;4192,16;Inherit;True;0;-1;2;3;2;SAMPLER2D;;False;0;FLOAT2;1,1;False;1;FLOAT2;0,0;False;5;FLOAT2;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4
Node;AmplifyShaderEditor.CommentaryNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;149;4624,-880;Inherit;False;1949.683;616.6039;;8;182;181;147;79;146;78;47;189;AlbedoDarkness;1,1,1,1;0;0
Node;AmplifyShaderEditor.SmoothstepOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;161;3552,-544;Inherit;True;3;0;FLOAT;0;False;1;FLOAT;0;False;2;FLOAT;1;False;1;FLOAT;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;1;4736,-576;Inherit;True;Property;_Albedo;Albedo;0;0;Create;True;0;0;0;False;0;False;-1;6dd6638e8d91f324383b214a129f143d;6dd6638e8d91f324383b214a129f143d;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;179;4800,-784;Inherit;False;Property;_Color;Color;14;0;Create;True;0;0;0;False;0;False;1,1,1,1;0,0,0,0;True;True;0;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;47;5232,-512;Inherit;False;Property;_DarkenAmount;_DarkenAmount;1;0;Create;True;0;0;0;True;0;False;0.25;0.05423951;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;144;3840,-544;Inherit;False;_LinesAlpha;-1;True;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;180;5120,-624;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;181;5504,-704;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;138;4576,704;Inherit;True;Property;_Smoothness;Smoothness;3;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;172;4880,800;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.OneMinusNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;189;5552,-592;Inherit;False;1;0;FLOAT;0;False;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;78;5760,-720;Inherit;True;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;146;5776,-496;Inherit;False;144;_LinesAlpha;1;0;OBJECT;;False;1;FLOAT;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;182;5488,-784;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;164;5008,896;Inherit;False;Property;_SmoothnessValue;SmoothnessValue;9;0;Create;True;0;0;0;False;0;False;0;0;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.StaticSwitch, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;174;5040,720;Inherit;False;Property;_RoughnessToggle;RoughnessToggle;12;0;Create;True;0;0;0;False;0;False;0;0;0;True;;Toggle;2;Key0;Key1;Create;True;True;All;9;1;FLOAT3;0,0,0;False;0;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT3;0,0,0;False;4;FLOAT3;0,0,0;False;5;FLOAT3;0,0,0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.LerpOp, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;79;6016,-800;Inherit;True;3;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;140;4576,416;Inherit;True;Property;_Metallic;Metallic;4;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;white;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;167;4576,608;Inherit;False;Property;_MetallicValue;MetallicValue;10;0;Create;True;0;0;0;False;0;False;0;1;0;1;0;1;FLOAT;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;165;5312,800;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;183;4576,16;Inherit;True;Property;_EmissionMap;_EmissionMap;15;0;Create;True;0;0;0;False;0;False;183;None;None;True;0;False;black;Auto;False;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;COLOR;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.ColorNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;185;4640,208;Inherit;False;Property;_EmissionColor;EmissionColor;16;1;[HDR];Create;True;0;0;0;False;0;False;1,1,1,1;1,1,1,0;True;False;0;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.RegisterLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;147;6320,-800;Inherit;False;AlbedoOut;-1;True;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;177;5472,672;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;166;5024,416;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT;0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;184;4928,128;Inherit;False;2;2;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.RangedFloatNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;186;4912,-16;Inherit;False;Property;_EmissionIntensity;EmissionIntensity;17;0;Create;True;0;0;0;False;0;False;1;1;0;0;0;1;FLOAT;0
Node;AmplifyShaderEditor.StickyNoteNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;163;1840,-624;Inherit;False;466.0763;237.1978;This one makes it show how thick every line is in cm;Lines;1,1,1,1;;0;0
Node;AmplifyShaderEditor.StickyNoteNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;168;1711.957,-958.3924;Inherit;False;2375.474;782.5734;This makes the lines alpha;Lines;1,1,1,1;;0;0
Node;AmplifyShaderEditor.GetLocalVarNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;148;5024,-240;Inherit;False;147;AlbedoOut;1;0;OBJECT;;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;175;5248,208;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.WireNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;176;5472,144;Inherit;False;1;0;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.SamplerNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;134;4576,-208;Inherit;True;Property;_Normal;Normal;2;0;Create;True;0;0;0;False;0;False;-1;None;None;True;0;False;bump;Auto;True;Object;-1;Auto;Texture2D;False;8;0;SAMPLER2D;;False;1;FLOAT2;0,0;False;2;FLOAT;0;False;3;FLOAT2;0,0;False;4;FLOAT2;0,0;False;5;FLOAT;1;False;6;FLOAT;0;False;7;SAMPLERSTATE;;False;6;FLOAT3;0;FLOAT;1;FLOAT;2;FLOAT;3;FLOAT;4;FLOAT3;5
Node;AmplifyShaderEditor.SimpleMultiplyOpNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;188;5184,-32;Inherit;False;2;2;0;FLOAT;0;False;1;FLOAT3;0,0,0;False;1;FLOAT3;0
Node;AmplifyShaderEditor.StandardSurfaceOutputNode, AmplifyShaderEditor, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null;0;5568,-128;Float;False;True;-1;3;LinesUnityShaderGUI;0;0;Standard;OpenPutt/GolfCourse/LinesStandard;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;False;True;True;False;Back;0;False;;0;False;;False;0;False;;0;False;;False;0;0;False;;0;Opaque;0.5;True;True;0;False;Opaque;;Geometry;All;12;all;True;True;True;True;0;False;;False;0;False;;255;False;;255;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;0;False;;False;2;15;10;25;False;0.5;True;0;0;False;;0;False;;0;0;False;;0;False;;0;False;;0;False;;0;False;0;0,0,0,0;VertexOffset;True;False;Cylindrical;False;True;Relative;0;;-1;-1;-1;-1;0;False;0;0;False;;-1;0;False;;0;0;0;False;0.1;False;;0;False;;False;17;0;FLOAT3;0,0,0;False;1;FLOAT3;0,0,0;False;2;FLOAT3;0,0,0;False;3;FLOAT;0;False;4;FLOAT;0;False;5;FLOAT;0;False;6;FLOAT3;0,0,0;False;7;FLOAT3;0,0,0;False;8;FLOAT;0;False;9;FLOAT;0;False;10;FLOAT;0;False;13;FLOAT3;0,0,0;False;11;FLOAT3;0,0,0;False;12;FLOAT3;0,0,0;False;16;FLOAT4;0,0,0,0;False;14;FLOAT4;0,0,0,0;False;15;FLOAT3;0,0,0;False;0
WireConnection;153;0;151;0
WireConnection;153;1;152;0
WireConnection;170;0;154;2
WireConnection;170;1;169;0
WireConnection;155;0;170;0
WireConnection;155;1;153;0
WireConnection;156;0;155;0
WireConnection;157;0;156;0
WireConnection;158;0;157;0
WireConnection;6;0;150;0
WireConnection;6;1;178;0
WireConnection;161;0;158;0
WireConnection;161;1;160;0
WireConnection;161;2;159;0
WireConnection;1;1;6;0
WireConnection;144;0;161;0
WireConnection;180;0;179;5
WireConnection;180;1;1;5
WireConnection;181;0;180;0
WireConnection;138;1;6;0
WireConnection;172;0;138;5
WireConnection;189;0;47;0
WireConnection;78;0;181;0
WireConnection;78;1;189;0
WireConnection;182;0;180;0
WireConnection;174;1;138;5
WireConnection;174;0;172;0
WireConnection;79;0;182;0
WireConnection;79;1;78;0
WireConnection;79;2;146;0
WireConnection;140;1;6;0
WireConnection;165;0;174;0
WireConnection;165;1;164;0
WireConnection;183;1;6;0
WireConnection;147;0;79;0
WireConnection;177;0;165;0
WireConnection;166;0;140;5
WireConnection;166;1;167;0
WireConnection;184;0;183;5
WireConnection;184;1;185;0
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
//CHKSM=5A123A234BC6315665A7F53125516F2E359A4303