package com.unity.mynativeapp;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.view.View;
import android.widget.Button;
import android.widget.FrameLayout;

import com.unity3d.player.IUnityPlayerSupport;
import com.unity3d.player.UnityPlayer;

public class SharedClass {

    public static void showMainActivity(String setToColor) {
        showMainActivity(UnityPlayer.currentActivity, setToColor);
    }

    public static void showMainActivity(Activity activity, String setToColor) {
        Intent intent = new Intent((Context) activity, MainActivity.class);
        intent.setFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT | Intent.FLAG_ACTIVITY_SINGLE_TOP);
        intent.putExtra("setColor", setToColor);
        activity.startActivity(intent);
    }

    public static void addControlsToUnityFrame(Activity activity) {
        UnityPlayer unityPlayer = ((IUnityPlayerSupport) UnityPlayer.currentActivity).getUnityPlayerConnection();
        FrameLayout layout = unityPlayer.getFrameLayout();
        Button showMainButton = new Button(activity);
        showMainButton.setText("Show Main");
        showMainButton.setX(10);
        showMainButton.setY(500);
        showMainButton.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                showMainActivity(activity, "");
            }
        });
        layout.addView(showMainButton, 300, 200);

        Button sendMsgButton = new Button(activity);
        sendMsgButton.setText("Send Msg");
        sendMsgButton.setX(320);
        sendMsgButton.setY(500);
        sendMsgButton.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                unityPlayer.UnitySendMessage("Cube", "ChangeColor", "yellow");
            }
        });
        layout.addView(sendMsgButton, 300, 200);

        Button unloadButton = new Button(activity);
        unloadButton.setText("Unload UnityPlayer");
        unloadButton.setX(630);
        unloadButton.setY(500);

        unloadButton.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                unityPlayer.unload();
            }
        });
        layout.addView(unloadButton, 350, 200);

        Button finishButton = new Button(activity);
        finishButton.setText("Activity Finish");
        finishButton.setX(630);
        finishButton.setY(800);

        finishButton.setOnClickListener(new View.OnClickListener() {
            public void onClick(View v) {
                activity.finish();
            }
        });
        layout.addView(finishButton, 300, 200);
    }


}
