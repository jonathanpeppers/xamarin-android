package mono.android;

import android.util.Log;

public class TypeManager {

	public static void Activate (String typeName, String sig, Object instance, Object[] parameterList)
	{
		Log.d("FOO", "Start of TypeManager.Activate");
		n_activate (typeName, sig, instance, parameterList);
		Log.d("FOO", "End of TypeManager.Activate");
	}

	private static native void n_activate (String typeName, String sig, Object instance, Object[] parameterList);

	static {
		Log.d("FOO", "Start of TypeManager static ctor");
		String methods = 
			"n_activate:(Ljava/lang/String;Ljava/lang/String;Ljava/lang/Object;[Ljava/lang/Object;)V:GetActivateHandler\n" +
			"";
		mono.android.Runtime.register ("Java.Interop.TypeManager+JavaTypeManager, Mono.Android, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", TypeManager.class, methods);
		Log.d("FOO", "End of TypeManager static ctor");
	}
}
