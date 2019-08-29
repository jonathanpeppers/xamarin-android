// Copyright (C) 2011 Xamarin, Inc. All rights reserved.

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class Javac : JavaCompileToolTask
	{
		public override string TaskPrefix => "JVC";

		[Required]
		public string StubSourceDirectory { get; set; }

		[Required]
		public string ClassesOutputDirectory { get; set; }

		public string ClassesZip { get; set; }

		public string [] Jars { get; set; }

		public override bool RunTask ()
		{
			if (!Compile (StubSourceDirectory, ClassesOutputDirectory, Jars))
				return false;

			Compress (ClassesOutputDirectory, ClassesZip);

			return !Log.HasLoggedErrors;
		}
	}
}
