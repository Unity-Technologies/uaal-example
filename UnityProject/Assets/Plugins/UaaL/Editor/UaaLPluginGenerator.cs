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
	// TODO pavell: support delegate type
	struct TypeiOSDetails {

	}

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
	static private Dictionary<Type,TypeCasting> mapCastObjcTypesToCcopy = new Dictionary<Type,TypeCasting>() {
		{typeof(void), expr => {return expr;} }, 
		{typeof(string), expr => { return String.Format("convertNSStringToCString({0})",expr);} },
		{typeof(int), expr => { return expr;} }
	};
	static private Dictionary<Type,TypeCasting> mapCastObjcTypesToC = new Dictionary<Type,TypeCasting>() {
		{typeof(void), expr => {return expr;} }, 
		{typeof(string), expr => { return String.Format("[{0} UTF8String]",expr);} },
		{typeof(int), expr => { return expr;} }
	};

	static void assertIsValidParameter(ParameterInfo param) {
		// TODO pavell: all params should be simple type, and not out, no default value including result
		Assert.IsTrue( mapTypesToObjc.ContainsKey(param.ParameterType) );
	}

	static string geniOSProtocolMethodDef(MethodInfo method) {
		var resultParam = method.ReturnParameter;
		assertIsValidParameter(resultParam);

        var methodParams = method.GetParameters();	           
        var result = "";
        foreach(var param in methodParams) {
       		// validate       		
       		if (result != "") result += param.Name;
       		result += String.Format(":({0}){1} ", mapTypesToObjc[param.ParameterType], param.Name);
        }

        return String.Format("-({0}) {1}{2}", mapTypesToObjc[resultParam.ParameterType], method.Name, result);
	}

	
	/*
	generates objc public header exposted from framework
	host must implement protocos and register instances by calling related register{INTERFACE_NAME} method 
	*/
	public const string iOSObjcPluginsHeaderFilePath = "Plugins/iOS/UaaL/UaaLBirdge.gen.h";
	const string iOSObjcHeaderFilePath = "Assets/" + iOSObjcPluginsHeaderFilePath;
	const string iOSObjcSourceFilePath = "Assets/Plugins/iOS/UaaL/UaaLBirdge.gen.mm";

	static string formatiOSObjcRegisterMethod(string typeName) { return String.Format("-(void) register{0}:(id<{0}>) proto", typeName); }
	static string formatiOSObjcGetInterfaceMethod(string typeName) { return String.Format("-(id<{0}>) get{0}", typeName); }

	static string formatiOSObjcToCProxyCall(Type interfaceType, MethodInfo method) {
		var methodParams = method.GetParameters();	           
        var cParams = new List<string>();        
        for(int i=0; i < methodParams.Count(); i++) {
        	var param = methodParams[i];
       		cParams.Add( String.Format("{0}", mapCastObjcTypesToC[param.ParameterType](param.Name)) );
        }		
        var returnType = method.ReturnParameter.ParameterType;	

        var call = string.Format( "g{0}{1}({2})", interfaceType.Name, method.Name, string.Join(", ", cParams) );        
        return string.Format( "{0}{1}", returnType == typeof(void) ? "" : "return ", mapCastCTypesToObjc[returnType](call));
	}

	static string formatiOSCToObjcMethod(Type interfaceType, MethodInfo method) {
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
			mapCastObjcTypesToCcopy[returnType](string.Format( "[g{0} {1}]", interfaceType.Name, string.Join(" ",objCcallExpr) ))			
			);
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

	static string formatCMethodType(MethodInfo method, string typeName) {
		var methodParams = method.GetParameters();
        var call = new List<string>();
        foreach(var param in methodParams) {
       		call.Add( String.Format("{0}", mapTypesToC[param.ParameterType]) );

        }
       	string returnS = mapTypesToC[method.ReturnParameter.ParameterType];
        return string.Format( "{0} (*{1})({2})", returnS, typeName, string.Join(",",call) );
	}

	static string formatCSMethodCall(MethodInfo method, string namePrefix="") {
		var methodParams = method.GetParameters();	           
        var call = new List<string>();        
        foreach(var param in methodParams) {
       		call.Add( String.Format("{0}", param.Name) );
		}

		return String.Format("{0}{1}({2})", namePrefix, method.Name, string.Join(",", call));
	}
	

	class iOSGenerator {
		string csSourceProxyClasses;
		List<string> csSourceGetInstance = new List<string>();
		List<string> csSourceSetInstance = new List<string>();

		List<string> objCHeaderProtocols = new List<string>();
		List<string> objCHeaderUFWMethods= new List<string>();

		List<string> objCSourceProxyClassInterfaces= new List<string>();
		List<string> objCSourceProxyClassImplements= new List<string>();
		List<string> objCSourceUFWMethods= new List<string>();
		List<string> objCSourceCMethods  = new List<string>();

		void genHostInterface() {
			var hostInterfaces = UnityEditor.TypeCache.GetTypesWithAttribute<UaaLiOSHostInterface>();		
			var registerMethods = new List<string>();		

	        foreach (var interfaceType in hostInterfaces)
	        {
	        	var csStaticiOSMethods = new List<string>();
	        	var csStaticMethods = new List<string>();

	        	MemberInfo[] interfaceMethods;
		    	interfaceMethods = interfaceType.GetMethods();

		    	string className = "UaaLPlugin_" + interfaceType.Name;
		    	csSourceGetInstance.Add( string.Format("if( typeof(T) == typeof({0})) return (new {1}()) as T;", interfaceType.Name, className) );

				objCHeaderProtocols.Add( formatiOSProtocolForInterface(interfaceType) );
				objCHeaderUFWMethods.Add(formatiOSObjcRegisterMethod(interfaceType.Name ) + ";");

				objCSourceUFWMethods.Add( String.Format("id<{0}> g{0} = NULL;\n{1} {{ g{0} = proto; }}", interfaceType.Name, formatiOSObjcRegisterMethod(interfaceType.Name ) ) );
		    	objCSourceCMethods.Add(String.Format("// {0}",interfaceType.Name));
	    	
		        for (int i =0 ; i < interfaceMethods.Length ; i++)
		        {
		        	MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);

		        	objCSourceCMethods.Add(formatiOSCToObjcMethod(interfaceType,method));

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

	        csSourceProxyClasses += string.Format(	
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
		}

		void formatCSUaalInterfaceClassMethods(Type interfaceType, MethodInfo method, List<string> csClass, List<string> registerMethods, List<string> cMethods, List<string> objcCalssMethodDefs, List<string> objcClassMethodImps) {
			{
				var mDelegate = formatCSMethodDef(method, "delegate" + "_");
		        var mDef = formatCSMethodDef(method);
		        var mCall = formatCSMethodCall(method);
		        var returnType = method.ReturnParameter.ParameterType;

		        string delegateName = string.Format("delegate_{0}", method.Name);

		        csClass.Add( string.Format("public delegate {0};",mDelegate));
		        csClass.Add( string.Format("[AOT.MonoPInvokeCallback (typeof({0}))]", delegateName));
		        csClass.Add( string.Format("public static {0} {{ {1}instance.{2}; }}", mDef, returnType == typeof(void) ? "" : "return ", mCall));

		        string setCallbackName = string.Format("setCallback_{0}_{1}",interfaceType.Name, method.Name);
		        csClass.Add( "[DllImport(\"__Internal\")]");
				csClass.Add( string.Format("public static extern void {0}({1} callback);", setCallbackName, delegateName));
				csClass.Add("");

				var cTypeName = "Type" + interfaceType.Name + method.Name;
				var mCTypeDef = formatCMethodType(method, cTypeName);
				var gVarName = "g" + interfaceType.Name + method.Name;
				cMethods.Add( string.Format("typedef {0};",mCTypeDef ));
				cMethods.Add( string.Format("{0} {1} = nullptr;", cTypeName, gVarName));
				cMethods.Add( string.Format("void {0} ({1} callback) {{ {2}= callback; }}", setCallbackName, cTypeName, gVarName ));

				registerMethods.Add( string.Format("{0}({1});",setCallbackName,method.Name) );
			}


			{
				var mDef = geniOSProtocolMethodDef(method);
			
				objcCalssMethodDefs.Add(mDef + ";");
				objcClassMethodImps.Add( string.Format("{0} {{ {1}; }}", mDef, formatiOSObjcToCProxyCall(interfaceType, method)));
			}
		}

		string formatCSUaaLInterfaceClass(Type interfaceType, List<string> classMethods, List<string> registerMethods) {
			return string.Format(
@"public class UaaLPlugin_{0} {{
	static {0} instance = null;

	#if UNITY_EDITOR
	#elif UNITY_IOS || UNITY_TVOS	
	{1}
	#endif

	static bool setCallbacks = true;
	public static void setInstance({0} aInstance) {{
		if(setCallbacks) {{
			#if UNITY_EDITOR
			#elif UNITY_IOS || UNITY_TVOS	
			{2}
			#endif
			setCallbacks = false;
		}}
		instance = aInstance;
	}}

}}",interfaceType.Name, string.Join("\n\t", classMethods), string.Join("\n\t\t", registerMethods));
		}

		string formatObjcProxyClassImplement(Type interfaceType, List<string> methods) {
return string.Format (
@"@implementation UaaLPlugin_{0}
{1}
@end
", interfaceType.Name, string.Join("\n", methods));
		}

		string formatObjcProxyClassInterface(Type interfaceType, List<string> methods) {
return string.Format (
@"@interface UaaLPlugin_{0} : NSObject
{1}
@end
", interfaceType.Name, string.Join("\n",methods));
		}

		void genUaaLInterface() {
			var uaalInterfaces = UnityEditor.TypeCache.GetTypesWithAttribute<UaaLiOSUaaLInterface>();		

	        foreach (var interfaceType in uaalInterfaces)
	        {
	        	objCHeaderProtocols.Add( formatiOSProtocolForInterface(interfaceType) );
				objCHeaderUFWMethods.Add(formatiOSObjcGetInterfaceMethod(interfaceType.Name ) + ";");

				objCSourceUFWMethods.Add( String.Format("{1}{{ return (id<{0}>)[[UaaLPlugin_{0} alloc] init];}}", interfaceType.Name, formatiOSObjcGetInterfaceMethod(interfaceType.Name ) ) );

	        	var csClassMethods = new List<string>();
	        	var registerMethods = new List<string>();
	        	var cMethods = new List<string>();
	        	var objcCalssMethodDefs = new List<string>();
	        	var objcClassMethodImps = new List<string>();

				MemberInfo[] interfaceMethods;
		    	interfaceMethods = interfaceType.GetMethods();
		    	for (int i =0 ; i < interfaceMethods.Length ; i++)
		        {
			    	MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);
			        formatCSUaalInterfaceClassMethods(interfaceType,method,csClassMethods,registerMethods, cMethods, objcCalssMethodDefs, objcClassMethodImps);
				}

				objCSourceCMethods.Add(string.Format("// {0}", interfaceType.Name));	
				objCSourceCMethods.AddRange(cMethods);

	        	csSourceSetInstance.Add(string.Format("if( typeof(T) ==typeof({0}) ) {{ UaaLPlugin_{0}.setInstance(instance as {0}); return; }}", interfaceType.Name) );
	        	csSourceProxyClasses += formatCSUaaLInterfaceClass(interfaceType, csClassMethods, registerMethods);
	        	objCSourceProxyClassInterfaces.Add( formatObjcProxyClassInterface(interfaceType,objcCalssMethodDefs));
	        	objCSourceProxyClassImplements.Add( formatObjcProxyClassImplement(interfaceType,objcClassMethodImps));
	        }
		} 

	/*
	generates objc source to receive calls from C# and route them to ObjC 
		C# -> C -> ObjC
		implements UnityFramework(UaaL) register{INTERFACE_NAME} methods
	*/
	public string getiOSObjcSource() {		
        return String.Format(
@"// Automatically generated by UaaLPluginGenerator
#import <{0}>
{3}

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

{4}
",	Path.GetFileName(iOSObjcHeaderFilePath), 
	string.Join("\n", objCSourceUFWMethods), 
	"\r" + string.Join("\n\t", objCSourceCMethods), 
	string.Join("\n", objCSourceProxyClassInterfaces),
	string.Join("\n", objCSourceProxyClassImplements)
	);
		}  

		public void gen() {
			genHostInterface();
			genUaaLInterface();
		}

		string formatiOSProtocolForInterface(Type interfaceType) {
	    	MemberInfo[] interfaceMethods;
	    	interfaceMethods = interfaceType.GetMethods();

	        var protoMethods = new List<string>();        
	        for (int i =0 ; i < interfaceMethods.Length ; i++)
	        {
	           MethodInfo method = interfaceType.GetMethod(interfaceMethods[i].Name);
	           var protoMethod = geniOSProtocolMethodDef(method);
	           protoMethods.Add(protoMethod + ";");
	        }

		    return String.Format(
@"@protocol {0}
@required
{1}
@end
",	interfaceType.Name, string.Join("\n", protoMethods) );
		}  
	
		

		public string getiOSObjcHeader() {
	        return String.Format(
@"// Automatically generated by UaaLPluginGenerator
#import <UnityFramework/UnityFramework.h>
{0}

@interface UnityFramework (UaaL)
{1}
@end
",	string.Join("\n", objCHeaderProtocols), string.Join("\n", objCHeaderUFWMethods));

		}

		public string getCSSource() {
       return string.Format(
@"// Automatically generated by UaaLPluginGenerator
// If interface name was modifyed/removed you will need to fix this manually
// if you having compile issues COMMENT NEXT LINE

#define USE_UAAL_GENCODE

using UnityEngine;
using System.Runtime.InteropServices;

#if USE_UAAL_GENCODE
{0}
#endif

public partial class UaaLPlugin : MonoBehaviour
{{
	public static T getInstance<T>() where T: class {{
		#if USE_UAAL_GENCODE
		{1}
		#endif
		return default(T);
	}}

	public static void setInterface<T>(T instance) where T: class {{
		#if USE_UAAL_GENCODE
		{2}
		#endif
	}}
}}
", csSourceProxyClasses, string.Join("\n\t\t",csSourceGetInstance), string.Join("\n\t\t",csSourceSetInstance) );			
		}
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
		// SC interface implementation
		var gen = new iOSGenerator();
		gen.gen();
		var scSource = gen.getCSSource();
		WriteFile( "Assets/UaaLPlugin.gen.cs", scSource );

		var objcHeader = gen.getiOSObjcHeader();
		WriteFile( iOSObjcHeaderFilePath, objcHeader );

		var mmSource = gen.getiOSObjcSource();
		WriteFile( iOSObjcSourceFilePath, mmSource );


		Debug.Log("UaaLPlugin Done generating UaaLPlugin files.");
	}
}

}
