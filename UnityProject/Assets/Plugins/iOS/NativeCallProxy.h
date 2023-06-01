// [!] important set UnityFramework in Target Membership for this file
// [!]           and set Public header visibility

#import <Foundation/Foundation.h>

// NativeCallsProtocol defines protocol with methods you want to be called from managed
@protocol NativeCallsProtocol
@required
- (void) showHostMainWindow:(NSString*)color;
// other methods
@end

@protocol UnityNotificationsDelegate
@required
- (void) payloadNotification:(NSString*)path:(NSString*)payload;
@end

__attribute__ ((visibility("default")))
@interface FrameworkLibAPI : NSObject
// call it any time after UnityFrameworkLoad to set object implementing NativeCallsProtocol methods
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi;
+(void) registerAPIforUnityNotifications:(id<UnityNotificationsDelegate>) aApi;
@end


