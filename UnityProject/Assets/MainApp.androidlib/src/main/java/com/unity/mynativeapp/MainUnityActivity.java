package com.unity.mynativeapp;

import android.content.Intent;
import android.os.Bundle;

import com.unity3d.player.UnityPlayerActivity;

public class MainUnityActivity extends UnityPlayerActivity {

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        // Setup activity layout
        SharedClass.addControlsToUnityFrame(this);
        Intent intent = getIntent();
        handleIntent(intent);
    }


    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        handleIntent(intent);
        setIntent(intent);
    }

    void handleIntent(Intent intent) {
        if (intent == null || intent.getExtras() == null) return;

        if (intent.getExtras().containsKey("doQuit")) {
            if (mUnityPlayer != null) {
                finish();
            }
        }
    }

    @Override
    public void onUnityPlayerUnloaded() {
        SharedClass.showMainActivity("");
    }

}
