using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework.XamlTypes;

namespace Xamarin.Android.Build.Tests
{
	static class Win32
	{
		/// <summary>
		/// See: https://docs.microsoft.com/windows/win32/api/winbase/nf-winbase-createjobobjecta
		/// </summary>
		[DllImport ("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern IntPtr CreateJobObject ([In] ref SECURITY_ATTRIBUTES lpJobAttributes, string lpName);

		/// <summary>
		/// See: https://docs.microsoft.com/windows/win32/api/jobapi2/nf-jobapi2-assignprocesstojobobject
		/// </summary>
		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool AssignProcessToJobObject (IntPtr hJob, IntPtr hProcess);

		/// <summary>
		/// See: https://docs.microsoft.com/windows/win32/api/jobapi2/nf-jobapi2-setinformationjobobject
		/// </summary>
		[DllImport ("kernel32.dll", SetLastError = true)]
		static extern bool SetInformationJobObject (IntPtr hJob, int JobObjectInfoClass, ref JOBOBJECT_BASIC_LIMIT_INFORMATION lpJobObjectInfo, int cbJobObjectInfoLength);

		/// <summary>
		/// The lpJobObjectInfo parameter is a pointer to a JOBOBJECT_BASIC_LIMIT_INFORMATION structure.
		/// </summary>
		const int JobObjectBasicLimitInformation = 2;

		/// <summary>
		/// Causes all processes associated with the job to use the same minimum and maximum working set sizes. The MinimumWorkingSetSize and MaximumWorkingSetSize members contain additional information.
		/// If the job is nested, the effective working set size is the smallest working set size in the job chain.
		/// </summary>
		const uint JOB_OBJECT_LIMIT_WORKINGSET = 1;

		public static void SetLimitWorkingSetSize (Process process, uint minimumWorkingSetSize, uint maximumWorkingSetSize)
		{
			var securityAttributes = new SECURITY_ATTRIBUTES ();
			securityAttributes.bInheritHandle = true;
			securityAttributes.nLength = Marshal.SizeOf (securityAttributes);

			var job = CreateJobObject (ref securityAttributes, process.ProcessName);
			if (job == IntPtr.Zero)
				throw new Win32Exception ();
			if (!AssignProcessToJobObject (job, process.Handle))
				throw new Win32Exception ();

			var limitInfo = new JOBOBJECT_BASIC_LIMIT_INFORMATION {
				LimitFlags = JOB_OBJECT_LIMIT_WORKINGSET,
				MinimumWorkingSetSize = minimumWorkingSetSize,
				MaximumWorkingSetSize = maximumWorkingSetSize,
			};
			if (!SetInformationJobObject (job, JobObjectBasicLimitInformation, ref limitInfo, Marshal.SizeOf (limitInfo)))
				throw new Win32Exception ();
		}

		/// <summary>
		/// See: https://docs.microsoft.com/previous-versions/windows/desktop/legacy/aa379560(v=vs.85)
		/// </summary>
		[StructLayout (LayoutKind.Sequential)]
		struct SECURITY_ATTRIBUTES
		{
			public int nLength;
			public IntPtr lpSecurityDescriptor;
			public bool bInheritHandle;
		}

		/// <summary>
		/// See: https://docs.microsoft.com/windows/win32/api/winnt/ns-winnt-jobobject_basic_limit_information
		/// </summary>
		[StructLayout (LayoutKind.Sequential)]
		struct JOBOBJECT_BASIC_LIMIT_INFORMATION
		{
			public long PerProcessUserTimeLimit;
			public long PerJobUserTimeLimit;
			public uint LimitFlags;
			public uint MinimumWorkingSetSize;
			public uint MaximumWorkingSetSize;
			public uint ActiveProcessLimit;
			public IntPtr Affinity;
			public uint PriorityClass;
			public uint SchedulingClass;
		}
	}
}
