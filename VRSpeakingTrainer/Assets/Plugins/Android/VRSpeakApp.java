package com.sabin.vrspeakingtrainer;

import android.app.Application;
import android.util.Log;

/**
 * Custom Application class whose sole purpose is to pre-load the Cardboard
 * native library from the main Java thread.
 *
 * Problem: Unity's IL2CPP loads libGfxPluginCardboard.so via dlopen() from a
 * native (C++) thread.  When a library is loaded that way, JNI_OnLoad is
 * invoked with the *bootstrap* class loader, so FindClass() cannot locate
 * application DEX classes.  The Cardboard SDK's JNI_OnLoad calls
 * FindClass("com/google/cardboard/sdk/QrCodeCaptureActivity") and then passes
 * the resulting NULL jclass to RegisterNatives(), which causes a fatal JNI
 * abort before the first frame renders.
 *
 * Fix: calling System.loadLibrary() here runs JNI_OnLoad on the main thread,
 * where the class loader IS the application class loader.  FindClass() can
 * then resolve QrCodeCaptureActivity, and RegisterNatives() succeeds.
 * Android's linker is idempotent — when Unity's IL2CPP later tries to dlopen
 * the same .so, the library is already mapped and JNI_OnLoad is NOT called
 * again.
 */
public class VRSpeakApp extends Application {
    private static final String TAG = "VRSpeakApp";

    @Override
    public void onCreate() {
        super.onCreate();
        try {
            System.loadLibrary("GfxPluginCardboard");
            Log.d(TAG, "GfxPluginCardboard pre-loaded successfully");
        } catch (UnsatisfiedLinkError e) {
            // Library not present in this build (e.g., Editor build) — skip.
            Log.w(TAG, "GfxPluginCardboard pre-load skipped: " + e.getMessage());
        }
    }
}
