package com.unity.mynativeapp;

import android.content.Intent;
import android.content.pm.ActivityInfo;
import android.content.pm.PackageManager;
import android.graphics.Color;
import android.os.Bundle;

import androidx.appcompat.app.AppCompatActivity;
import androidx.appcompat.widget.Toolbar;

import android.view.View;
import android.widget.Button;
import android.widget.Toast;

public class MainActivity extends AppCompatActivity {
    private enum ActivityType {
        PLAYER_ACTIVITY, PLAYER_GAME_ACTIVITY, BOTH
    }

    boolean isUnityLoaded = false;
    private ActivityType mActivityType = ActivityType.BOTH;
    private boolean isGameActivity = false;

    private Button mShowUnityButton;
    private Button mShowUnityGameButton;


    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);

        isUnityLoaded = false;
        setContentView(R.layout.activity_main);
        Toolbar toolbar = findViewById(R.id.toolbar);
        setSupportActionBar(toolbar);
        adjustButtons();

        handleIntent(getIntent());
    }

    @Override
    protected void onNewIntent(Intent intent) {
        super.onNewIntent(intent);
        handleIntent(intent);
        setIntent(intent);
    }

    void handleIntent(Intent intent) {
        if (intent == null || intent.getExtras() == null)
            return;

        if (intent.getExtras().containsKey("setColor")) {
            View v = findViewById(R.id.finish_button);
            switch (intent.getExtras().getString("setColor")) {
                case "yellow":
                    v.setBackgroundColor(Color.YELLOW);
                    break;
                case "red":
                    v.setBackgroundColor(Color.RED);
                    break;
                case "blue":
                    v.setBackgroundColor(Color.BLUE);
                    break;
                default:
                    v.setBackgroundColor(0xFFd6d7d7);
                    break;
            }
        }
    }

    public void onClickShowUnity(View v) {
        isUnityLoaded = true;
        isGameActivity = !(v.getId() == R.id.show_unity_button);
        disableShowUnityButtons();

        int id = v.getId();
        if (id == R.id.show_unity_button) {
            startUnityWithClass(getMainUnityActivityClass());
        } else if (id == R.id.show_unity_game_button) {
            startUnityWithClass(getMainUnityGameActivityClass());
        }
    }

    private void startUnityWithClass(Class klass) {
        Intent intent = new Intent(this, klass);
        intent.setFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
        startActivityForResult(intent, 1);
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        if (requestCode == 1) {
            isUnityLoaded = false;
            enableShowUnityButtons();
            showToast("Unity finished.");
        }
    }

    public void unloadUnity(Boolean doShowToast) {
        if (isUnityLoaded) {
            Intent intent;
            if (isGameActivity)
                intent = new Intent(this, getMainUnityGameActivityClass());
            else
                intent = new Intent(this, getMainUnityActivityClass());
            intent.setFlags(Intent.FLAG_ACTIVITY_REORDER_TO_FRONT);
            intent.putExtra("doQuit", true);
            startActivity(intent);
            isUnityLoaded = false;
        } else if (doShowToast) {
            showToast("Show Unity First");
        }
    }

    public void onClickFinish(View v) {
        unloadUnity(true);
    }

    private void showToast(String message) {
        CharSequence text = message;
        int duration = Toast.LENGTH_SHORT;
        Toast toast = Toast.makeText(getApplicationContext(), text, duration);
        toast.show();
    }

    @Override
    public void onBackPressed() {
        finishAffinity();
    }

    private Class findClassUsingReflection(String className) {
        try {
            return Class.forName(className);
        } catch (final ClassNotFoundException e) {
            e.printStackTrace();
        }
        return null;
    }

    private Class getMainUnityActivityClass() {
        return findClassUsingReflection("com.unity.mynativeapp.MainUnityActivity");
    }

    private Class getMainUnityGameActivityClass() {
        return findClassUsingReflection("com.unity.mynativeapp.MainUnityGameActivity");
    }

    private void adjustButtons() {
        mShowUnityButton = findViewById(R.id.show_unity_button);
        mShowUnityGameButton = findViewById(R.id.show_unity_game_button);

        if (getMainUnityActivityClass() != null) {
            mShowUnityButton.setVisibility(View.VISIBLE);
            mActivityType = ActivityType.PLAYER_ACTIVITY;
        }

        if (getMainUnityGameActivityClass() != null) {
            mShowUnityGameButton.setVisibility(View.VISIBLE);
            mActivityType = ActivityType.PLAYER_GAME_ACTIVITY;
        }

        if (mShowUnityButton.getVisibility() == View.VISIBLE && mShowUnityGameButton.getVisibility() == View.VISIBLE) {
            mActivityType = ActivityType.BOTH;
        }
    }

    private void disableShowUnityButtons() {
        if (mActivityType != ActivityType.BOTH)
            return;

        mShowUnityButton.setEnabled(!isGameActivity);
        mShowUnityGameButton.setEnabled(isGameActivity);
    }

    private void enableShowUnityButtons() {
        if (mActivityType != ActivityType.BOTH)
            return;

        mShowUnityButton.setEnabled(true);
        mShowUnityGameButton.setEnabled(true);
    }
}
