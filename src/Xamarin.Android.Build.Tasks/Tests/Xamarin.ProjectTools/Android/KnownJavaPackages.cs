using System;

namespace Xamarin.ProjectTools
{
	public static class KnownJavaPackages
	{
		public static AndroidItem.EmbeddedJar SvgAndroid = new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
			WebContent = "https://storage.googleapis.com/google-code-archive-downloads/v2/code.google.com/svg-android/svg-android.jar"
		};

		public static AndroidItem.EmbeddedJar JavaBindingIssue = new AndroidItem.EmbeddedJar ("Jars\\svg-android.jar") {
			WebContentFileNameFromAzure = "javaBindingIssue.jar"
		};
	}
}
