using Microsoft.Build.Framework;
using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tasks
{
	class TaskItemComparer : IEqualityComparer<ITaskItem>
	{
		public static readonly TaskItemComparer DefaultComparer = new TaskItemComparer ();

		public bool Equals (ITaskItem a, ITaskItem b) =>
			string.Compare (a.ItemSpec, b.ItemSpec, StringComparison.OrdinalIgnoreCase) == 0;

		public int GetHashCode (ITaskItem value) => value.ItemSpec.GetHashCode ();
	}
}
