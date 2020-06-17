using System;
 
namespace UaaL {
	
	[AttributeUsage(AttributeTargets.Interface)]
	public class UaaLiOSHostInterface : Attribute {
		public UaaLiOSHostInterface() {}
	}

	[AttributeUsage(AttributeTargets.Interface)]
	public class UaaLiOSUaaLInterface : Attribute {
		public UaaLiOSUaaLInterface() {}	
	}

}
