using Android.Runtime;
using Android.Util;
using Java.Interop;

/// <summary>
/// NOTE: This class is not required, but used for testing Android.App.Application subclasses.
/// </summary>
[Register ("my/MainApplication")] // Required for typemap in NativeAotTypeManager
[Application]
public class MainApplication : Application
{
    public MainApplication (ref JniObjectReference reference, JniObjectReferenceOptions options)
        : base (reference.Handle, JniHandleOwnership.DoNotRegister)
    {
    }

    public override void OnCreate ()
    {
        Log.Debug ("NativeAOT", "Application.OnCreate()");

        base.OnCreate ();
    }
}
