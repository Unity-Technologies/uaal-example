//
//  AppDelegate.h
//  NativeiOSApp
//
//  Created by Jonathan Thorpe on 01/06/2023.
//  Copyright © 2023 unity. All rights reserved.
//

#ifndef AppDelegate_h
#define AppDelegate_h

#import <UIKit/UIKit.h>
#include <UnityFramework/UnityFramework.h>
#include <UnityFramework/NativeCallProxy.h>

@interface AppDelegate : UIResponder<UIApplicationDelegate,
    UnityFrameworkListener, NativeCallsProtocol, UnityNotificationsDelegate>

@property (strong, nonatomic) UIWindow *window;

- (void)sendMessageToGOWithName:(NSString*)goName functionName:(NSString*)name message:(NSString*)msg;

@end

#endif /* AppDelegate_h */
