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
- (void) payloadNotification:(NSString*)payloadType:(NSString*)payloadContent;
@end

@protocol UnityOutgoingWorkflowDelegate
@required
- (void) requestNativeWorkflow:(NSString*)identifier:(NSString*)path:(NSString*)payload;
- (void) cancelNativeWorkflow:(NSString*)identifier;
@end

@protocol UnityIncomingWorkflowDelegate
@required
- (void) completedUnityWorkflow:(NSString*)identifier:(NSNumber*)success:(NSString*)result;
@end


__attribute__ ((visibility("default")))
@interface FrameworkLibAPI : NSObject
// call it any time after UnityFrameworkLoad to set object implementing NativeCallsProtocol methods
+(void) registerAPIforNativeCalls:(id<NativeCallsProtocol>) aApi;

@end


