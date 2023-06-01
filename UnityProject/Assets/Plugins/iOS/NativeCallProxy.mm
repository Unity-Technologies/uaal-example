#import <Foundation/Foundation.h>
#import "NativeCallProxy.h"

@implementation FrameworkLibAPI

id<NativeCallsProtocol> api = NULL;
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi
{
    api = aApi;
}

id<UnityNotificationsDelegate> notificationsDelegate = NULL;
+(void) registerAPIforUnityNotifications:(id<UnityNotificationsDelegate>) aApi
{
    notificationsDelegate = aApi;
}

@end

extern "C" {
    void showHostMainWindow(const char* color) { return [api showHostMainWindow:[NSString stringWithUTF8String:color]]; }
    void payloadNotification(const char* path, const char* payload) { return [notificationsDelegate payloadNotification:[NSString stringWithUTF8String:path]:[NSString stringWithUTF8String:payload]];}
}

