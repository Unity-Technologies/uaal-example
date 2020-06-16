using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEngine.Assertions;

using UnityEditor.iOS.Xcode;
 
/*
	Test Notes:
		- test on windows, test on mac
		- build from console with batch/headless
		- no intefaces, one interface, many interfaces having none,one,many, same methods, overloaded methods
		- test all supported data types on min,max values, utf8 for the strings
		- test on Andtoird, iOS, tVOS platwork, its working
		- test on other platforms (egz. standalone) not broken
		- test works without Android, iOS, tvOS module support

	Folder Structure:
	/Assets/
		Plugins/
			UaaL/
				- UaaLPlugin.cs interface attributes

				Editor/
					- Generators

			iOS/UaaL/
				- iOS generated files
					iOSObjcHeaderFilePath
					iOSObjcSourceFilePath

			Android/UaaL/
				- Android generated files
		- generated CS proxy interface implementations | default interface instance generator
*/


namespace UaaL {
public partial class UaaLPluginGenerator
{	
	// TODO pavell: support for simple types https://docs.microsoft.com/en-us/dotnet/csharp/tour-of-csharp/types-and-variables
	static private Dictionary<Type,string> mapTypesToObjc  = new Dictionary<Type, string>() { 
		{typeof(void), "void"}, 
		{typeof(string),"NSString*"},
		{typeof(int), "int" }
	};
	static private Dictionary<Type,string> mapTypesToC  = new Dictionary<Type, string>() { 
		{typeof(void), "void"}, 
		{typeof(string), "const char*"},
		{typeof(int), "int"}
	};
	static private Dictionary<Type,string> mapTypesToCS  = new Dictionary<Type, string>() { 
		{typeof(void), "void"}, 
		{typeof(string), "string"}, 
		{typeof(int), "int" }
	};
	private delegate string TypeCasting(string expr);
	static private Dictionary<Type,TypeCasting> mapCastCTypesToObjc = new Dictionary<Type,TypeCasting>() {
		{typeof(void), expr => {return expr;} }, 
		{typeof(string), expr=> { return String.Format("[NSString stringWithUTF8String:{0}]",expr);} },
		{typeof(int), expr => { return expr;} }
	};
	static private Dictionary<Type,TypeCasting> mapCastObjcTypesToC = new Dictionary<Type,TypeCasting>() {
		{typeof(void), expr => {return expr;} }, 
		{typeof(string), expr => { return String.Format("convertNSStringToCString({0})",expr);} },
		{typeof(int), expr => { return expr;} }
	};

	static void assertIsValidParameter(ParameterInfo param) {
		// TODO pavell: all params should be simple type, and not out, no default value including result
		Assert.IsTrue( mapTypesToObjc.ContainsKey(param.ParameterType) );
	}

	static string geniOSProtocolMethod(MethodInfo method) {
		var resultParam = method.ReturnParameter;
		assertIsValidParameter(resultParam);

        // Display name and type of the concerned member.
        //Debug.Log( String.Format("'{0}' is a {1} is abstract: {2} and returns type: {3}", method.Name, method.MemberType, method.IsAbstract, resultParam.ParameterType));


        var methodParams = method.GetParameters();	           
        var result = "";
        foreach(var param in methodParams) {
       		// validate       		
       		//Debug.Log( String.Format("{0} of type {1}", param.Name, param.ParameterType));
       		if (result != "") result += param.Name;
       		result += String.Format(":({0}){1} ", mapTypesToObjc[param.ParameterType], param.Name);
        }

        return String.Format("-({0}) {1}{2}", mapTypesToObjc[resultParam.ParameterType], method.Name, result);
	}

	static string geniOSProtocolForInterface(Type interfaceType) {
		//Debug.Log( String.Format("\nFound UaaLiOSHostInterface interface: {0} is interface: {1}  is abstract: {2} is class: {3} is public:{4} is sealed:{5}", interfaceType.Name, interfaceType.IsInterface, interfaceType.IsAbstract, interfaceType.IsClass, interfaceType.IsPublic, interfaceType.IsSealed));
        	
        	
    	MemberInfo[] interfaceMethods;
    	interfaceMethods = interfaceType.GetMethods();

        var protoMethods = new List<string>();        
        for (int i =0 ; i < interfaceMethods.Length ; i++)
        {
           MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);
           var protoMethod = geniOSProtocolMethod(method);
           protoMethods.Add(protoMethod + ";");
        }

	    return String.Format(
@"@protocol {0}
@required
{1}
@end
",	interfaceType.Name, string.Join("\n", protoMethods) );
	}  

	
	/*
	generates objc public header exposted from framework
	host must implement protocos and register instances by calling related register{INTERFACE_NAME} method 
	*/
	public const string iOSObjcPluginsHeaderFilePath = "Plugins/iOS/UaaL/UaaLBirdge.gen.h";
	const string iOSObjcHeaderFilePath = "Assets/" + iOSObjcPluginsHeaderFilePath;
	const string iOSObjcSourceFilePath = "Assets/Plugins/iOS/UaaL/UaaLBirdge.gen.mm";

	static string formatiOSObjcRegisterMethod(string typeName) { return String.Format("-(void) register{0}:(id<{0}>) proto", typeName); }
	static string geniOSObjcHeader() {		
		var hostInterfaces = UnityEditor.TypeCache.GetTypesWithAttribute<UaaLiOSHostInterface>();
		var protos = new List<string>();
		var registerMethods = new List<string>();
        foreach (var interfaceType in hostInterfaces)
        {
        	var proto = geniOSProtocolForInterface(interfaceType);
        	protos.Add(proto);
        	registerMethods.Add(formatiOSObjcRegisterMethod(interfaceType.Name ) + ";");
        }	

        return String.Format(
@"// Automatically generated by UaaLPluginGenerator
#import <UnityFramework/UnityFramework.h>
{0}

@interface UnityFramework (UaaL)
{1}
@end
",	string.Join("\n", protos), string.Join("\n", registerMethods));
	}  


	static string geniOSCRouteMethod(Type interfaceType, MethodInfo method) {
		//void showHostMainWindow(const char* color) { return [api showHostMainWindow:[NSString stringWithUTF8String:color]]; }

        var methodParams = method.GetParameters();	           
        var cParams = new List<string>();
        var objCcallExpr = new List<string>();
        for(int i=0; i < methodParams.Count(); i++) {
        	var param = methodParams[i];
       		cParams.Add( String.Format("{0} {1}", mapTypesToC[param.ParameterType], param.Name) );

       		string name = param.Name;
       		if(i==0) name = method.Name;
       		objCcallExpr.Add( String.Format("{0}:{1}", name, mapCastCTypesToObjc[param.ParameterType](param.Name)));
        }

        if(objCcallExpr.Count() == 0) 
        	objCcallExpr.Add( method.Name); 

		var returnType = method.ReturnParameter.ParameterType;		
		return String.Format("{0} {1}_{2}( {3} ) {{ {4} {5}; }}", 
			mapTypesToC[returnType], 
			interfaceType.Name, 
			method.Name, 
			string.Join(", ",cParams), 
			returnType == typeof(void) ? "" : "return",
			mapCastObjcTypesToC[returnType](string.Format( "[g{0} {1}]", interfaceType.Name, string.Join(" ",objCcallExpr) ))			
			);
	}

	/*
	generates objc source to receive calls from C# and route them to ObjC 
		C# -> C -> ObjC
		implements UnityFramework(UaaL) register{INTERFACE_NAME} methods
	*/
	static string geniOSObjcSource() {		
		var hostInterfaces = UnityEditor.TypeCache.GetTypesWithAttribute<UaaLiOSHostInterface>();
		var cMethods = new List<string>();
		var registerMethods = new List<string>();		
        foreach (var interfaceType in hostInterfaces)
        {
        	MemberInfo[] interfaceMethods;
	    	interfaceMethods = interfaceType.GetMethods();
	    	cMethods.Add(String.Format("// {0}",interfaceType.Name));
	        for (int i =0 ; i < interfaceMethods.Length ; i++)
	        {
	           MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);
	           cMethods.Add(geniOSCRouteMethod(interfaceType,method));
	        } 			
        	registerMethods.Add( String.Format("id<{0}> g{0} = NULL;\n{1} {{ g{0} = proto; }}", interfaceType.Name, formatiOSObjcRegisterMethod(interfaceType.Name ) ) );
        }	
 
        return String.Format(
@"// Automatically generated by UaaLPluginGenerator
#import <{0}>

@implementation UnityFramework (UaaL)
{1}
@end

char* convertNSStringToCString(const NSString* nsString)
{{
    if (nsString == NULL) return NULL;

    const char* nsStringUtf8 = [nsString UTF8String];
    char* cString = (char*)malloc(strlen(nsStringUtf8) + 1);
    strcpy(cString, nsStringUtf8);

    return cString;
}}

extern ""C"" {{
	{2}
}}
",	Path.GetFileName(iOSObjcHeaderFilePath), string.Join("\n", registerMethods), "\r" + string.Join("\n\t", cMethods) );
	}  


	static string formatCSMethodDef(MethodInfo method, string namePrefix="") {
		var methodParams = method.GetParameters();	           
        var call = new List<string>();          
        foreach(var param in methodParams) {
       		call.Add( String.Format("{0} {1}", mapTypesToCS[param.ParameterType], param.Name) );

        }
       	string returnS = mapTypesToCS[method.ReturnParameter.ParameterType];
        return string.Format( "{0} {1}{2}({3})", returnS, namePrefix, method.Name, string.Join(",",call) );
	}

	static string formatCSMethodCall(MethodInfo method, string namePrefix="") {
		var methodParams = method.GetParameters();	           
        var call = new List<string>();        
        foreach(var param in methodParams) {
       		call.Add( String.Format("{0}", param.Name) );
		}

		return String.Format("{0}{1}({2})", namePrefix, method.Name, string.Join(",", call));
	}
	

	static string genCSSource() {
		var hostInterfaces = UnityEditor.TypeCache.GetTypesWithAttribute<UaaLiOSHostInterface>();		
		var registerMethods = new List<string>();		

		string result = "";
		var createInstance = new List<string>();

        foreach (var interfaceType in hostInterfaces)
        {
        	var csStaticiOSMethods = new List<string>();
        	var csStaticMethods = new List<string>();

        	MemberInfo[] interfaceMethods;
	    	interfaceMethods = interfaceType.GetMethods();

	    	string className = "UaaLPlugin_" + interfaceType.Name;
	    	createInstance.Add( string.Format("if( typeof(T) == typeof({0})) return (new {1}()) as T;", interfaceType.Name, className) );

	        for (int i =0 ; i < interfaceMethods.Length ; i++)
	        {
	        	MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);
	        	var m = formatCSMethodDef(method, interfaceType.Name + "_");
	        	csStaticiOSMethods.Add( "[DllImport(\"__Internal\")]" );
	            csStaticiOSMethods.Add( String.Format( "public static extern {0};", m ) );	            


	            var returnType = method.ReturnParameter.ParameterType;

				m = formatCSMethodDef(method); 
				string mC = formatCSMethodCall(method, interfaceType.Name + "_");
	            csStaticMethods.Add( String.Format(
@"public {0} {{
		#if UNITY_EDITOR
		#elif UNITY_IOS || UNITY_TVOS	
			{1}{2};			
		#else 
		#endif
		{3}
	}}", 
		m, 
		returnType == typeof(void) ? "" : "return ", 
		mC,
		returnType == typeof(void) ? "" : string.Format("return default({0});", mapTypesToCS[returnType])
		));
	        } 			

	        result += string.Format(	
@"public class {0} : {1}
{{
#if UNITY_IOS || UNITY_TVOS
	// external calls implemented in .mm file generated by UaaLPluginGenerator.geniOSObjcSource
	{2}
#endif

	// proxy methods to call platform depended code
	{3}

}}
",	className, interfaceType.Name, string.Join("\n\t",csStaticiOSMethods), string.Join("\n\t",csStaticMethods));
        }


       return string.Format(
@"// Automatically generated by UaaLPluginGenerator
// If interface name was modifyed/removed you will need to fix this manually
// if you having compile issues COMMENT NEXT LINE

#define USE_INTERFACES

using UnityEngine;
using System.Runtime.InteropServices;

#if USE_INTERFACES
{0}
#endif

public partial class UaaLPlugin : MonoBehaviour
{{
	public static T getInstance<T>() where T: class {{
		#if USE_INTERFACES
		{1}
		#endif
		return default(T);
	}}
}}
", result, string.Join("\n\t\t",createInstance) );
	}

	static void WriteFile(string projectRelPath, string content) {
		string path = Path.Combine(Application.persistentDataPath, projectRelPath);
		Directory.CreateDirectory(Path.GetDirectoryName(projectRelPath));

		var writer = new StreamWriter(projectRelPath,false);
		writer.Write(content);
		writer.Close();

		AssetDatabase.ImportAsset(projectRelPath);
	}

	[MenuItem("Plugins/UaaL/Generate Code")]
	public static void GenerateCode() {
		var objcHeader = geniOSObjcHeader();
		WriteFile( iOSObjcHeaderFilePath, objcHeader );

		var mmSource = geniOSObjcSource();
		WriteFile( iOSObjcSourceFilePath, mmSource );
		
		// SC interface implementation
		var scSource = genCSSource();
		WriteFile( "Assets/UaaLPlugin.gen.cs", scSource );
	}
}

}
