package com.company.product;

import android.os.Bundle;


import com.unity3d.player.UnityPlayerGameActivity;

public abstract class OverrideUnityActivity extends UnityPlayerGameActivity {
    public static OverrideUnityActivity instance = null;

    abstract protected void showMainActivity(String setToColor);

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        instance = this;
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        instance = null;
    }

}
