#import <Foundation/Foundation.h>
#import "NativeCallProxy.h"


@implementation FrameworkLibAPI

id<NativeCallsProtocol> api = NULL;
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi
{
    api = aApi;
}

id<UnityNotificationsDelegate> notificationsDelegate = NULL;
+(void) registerAPIforNotificationsDelegate:(id<NativeCallsProtocol>) aApi
{
    notificationsDelegate = aApi;
}

id<UnityOutgoingWorkflowDelegate> outgoingWorkflowDelegate = NULL;
+(void) registerAPIforOutgoingWorkflowDelegate:(id<UnityOutgoingWorkflowDelegate>) aApi
{
    outgoingWorkflowDelegate = aApi;
}

id<UnityIncomingWorkflowDelegate> incomingWorkflowDelegate = NULL;
+(void) registerAPIforIncomingWorkflowDelegate:(id<UnityIncomingWorkflowDelegate>) aApi
{
    incomingWorkflowDelegate = aApi;
}

@end


extern "C" {
    void showHostMainWindow(const char* color) { return [api showHostMainWindow:[NSString stringWithUTF8String:color]]; }
    void payloadNotification(const char* payloadType, const char* payloadContent, const bool quiet) { return [notificationsDelegate payloadNotification:[NSString stringWithUTF8String:payloadType]:[NSString stringWithUTF8String:payloadContent]];}
}

