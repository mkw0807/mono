// 
// System.IO.Directory.cs 
//
// Authors:
//   Jim Richardson  (develop@wtfo-guru.com)
//   Miguel de Icaza (miguel@ximian.com)
//   Dan Lewis       (dihlewis@yahoo.co.uk)
//   Eduardo Garcia  (kiwnix@yahoo.es)
//   Ville Palo      (vi64pa@kolumbus.fi)
//
// Copyright (C) 2001 Moonlight Enterprises, All Rights Reserved
// Copyright (C) 2002 Ximian, Inc.
// 
// Created:        Monday, August 13, 2001 
//
//------------------------------------------------------------------------------

//
// Copyright (C) 2004-2005 Novell, Inc (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System.Collections;
using System.Security;
using System.Security.Permissions;
using System.Text;

namespace System.IO
{
	public
#if NET_2_0
	static
#else
	sealed
#endif
	class Directory
	{

#if !NET_2_0
		private Directory () {}
#endif
		
		public static DirectoryInfo CreateDirectory (string path)
		{
			if (path == null)
				throw new ArgumentNullException ("path");
			
			if (path == "")
				throw new ArgumentException ("Path is empty");
			
			if (path.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Path contains invalid chars");

			if (path.Trim ().Length == 0)
				throw new ArgumentException ("Only blank characters in path");

#if NET_2_0
			if (File.Exists(path))
				throw new IOException ("Cannot create " + path + " because a file with the same name already exists.");
#endif
			
			// LAMESPEC: with .net 1.0 version this throw NotSupportedException and msdn says so too
			// but v1.1 throws ArgumentException.
			if (path == ":")
				throw new ArgumentException ("Only ':' In path");
			
			return CreateDirectoriesInternal (path);
		}

		static DirectoryInfo CreateDirectoriesInternal (string path)
		{
			if (SecurityManager.SecurityEnabled) {
				new FileIOPermission (FileIOPermissionAccess.Read | FileIOPermissionAccess.Write, path).Demand ();
			}

			DirectoryInfo info = new DirectoryInfo (path);
			if (info.Parent != null && !info.Parent.Exists)
				 info.Parent.Create ();

			MonoIOError error;
			if (!MonoIO.CreateDirectory (path, out error)) {
				// LAMESPEC: 1.1 and 1.2alpha allow CreateDirectory on a file path.
				// So CreateDirectory ("/tmp/somefile") will succeed if 'somefile' is
				// not a directory. However, 1.0 will throw an exception.
				// We behave like 1.0 here (emulating 1.1-like behavior is just a matter
				// of comparing error to ERROR_FILE_EXISTS, but it's lame to do:
				//    DirectoryInfo di = Directory.CreateDirectory (something);
				// and having di.Exists return false afterwards.
				// I hope we don't break anyone's code, as they should be catching
				// the exception anyway.
				if (error != MonoIOError.ERROR_ALREADY_EXISTS &&
				    error != MonoIOError.ERROR_FILE_EXISTS)
					throw MonoIO.GetException (path, error);
			}

			return info;
		}
		
		public static void Delete (string path)
		{
			if (path == null)
				throw new ArgumentNullException ("path");
			
			if (path == "")
				throw new ArgumentException ("Path is empty");
			
			if (path.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Path contains invalid chars");

			if (path.Trim().Length == 0)
				throw new ArgumentException ("Only blank characters in path");

			if (path == ":")
				throw new NotSupportedException ("Only ':' In path");

			MonoIOError error;
			
			if (!MonoIO.RemoveDirectory (path, out error)) {
				/*
				 * FIXME:
				 * In io-layer/io.c rmdir returns error_file_not_found if directory does not exists.
				 * So maybe this could be handled somewhere else?
				 */
				if (error == MonoIOError.ERROR_FILE_NOT_FOUND) 
					throw new DirectoryNotFoundException ("Directory '" + path + "' doesnt exists.");
				else
					throw MonoIO.GetException (path, error);
			}
		}

		static void RecursiveDelete (string path)
		{
			foreach (string dir in GetDirectories (path))
				RecursiveDelete (dir);

			foreach (string file in GetFiles (path))
				File.Delete (file);

			Directory.Delete (path);
		}
		
		public static void Delete (string path, bool recurse)
		{
			CheckPathExceptions (path);
			
			if (recurse == false){
				Delete (path);
				return;
			}

			RecursiveDelete (path);
		}

		public static bool Exists (string path)
		{
			if (path == null)
				return false;
				
			MonoIOError error;
			bool exists;
			
			exists = MonoIO.ExistsDirectory (path, out error);
			if (error != MonoIOError.ERROR_SUCCESS &&
			    error != MonoIOError.ERROR_PATH_NOT_FOUND &&
			    error != MonoIOError.ERROR_INVALID_HANDLE &&
			    error != MonoIOError.ERROR_ACCESS_DENIED) {

				// INVALID_HANDLE might happen if the file is moved
				// while testing for the existence, a kernel issue
				// according to Larry Ewing.
				
				throw MonoIO.GetException (path, error);
			}

			return(exists);
		}

		public static DateTime GetLastAccessTime (string path)
		{
			return File.GetLastAccessTime (path);
		}
		
		public static DateTime GetLastAccessTimeUtc (string path)
		{
			return GetLastAccessTime (path).ToUniversalTime ();
		}
		      
		public static DateTime GetLastWriteTime (string path)
		{
			return File.GetLastWriteTime (path);
		}
		
		public static DateTime GetLastWriteTimeUtc (string path)
		{
			return GetLastWriteTime (path).ToUniversalTime ();
		}

		public static DateTime GetCreationTime (string path)
		{
			return File.GetCreationTime (path);
		}

		public static DateTime GetCreationTimeUtc (string path)
		{
			return GetCreationTime (path).ToUniversalTime ();
		}

		public static string GetCurrentDirectory ()
		{
			MonoIOError error;
				
			string result = MonoIO.GetCurrentDirectory (out error);
			if (error != MonoIOError.ERROR_SUCCESS)
				throw MonoIO.GetException (error);

			if ((result != null) && (result.Length > 0) && SecurityManager.SecurityEnabled) {
				new FileIOPermission (FileIOPermissionAccess.PathDiscovery, result).Demand ();
			}
			return result;
		}
		
		public static string [] GetDirectories (string path)
		{
			return GetDirectories (path, "*");
		}
		
		public static string [] GetDirectories (string path, string pattern)
		{
			return GetFileSystemEntries (path, pattern, FileAttributes.Directory, FileAttributes.Directory);
		}
		
#if NET_2_0
		public static string [] GetDirectories (string path, string pattern, SearchOption option)
		{
			if (option == SearchOption.TopDirectoryOnly)
				return GetDirectories (path, pattern);
			ArrayList all = new ArrayList ();
			GetDirectoriesRecurse (path, pattern, all);
			return (string []) all.ToArray (typeof (string));
		}
		
		static void GetDirectoriesRecurse (string path, string pattern, ArrayList all)
		{
			all.AddRange (GetDirectories (path, pattern));
			foreach (string dir in GetDirectories (path))
				GetDirectoriesRecurse (dir, pattern, all);
		}
#endif

		public static string GetDirectoryRoot (string path)
		{
			return new String(Path.DirectorySeparatorChar,1);
		}
		
		public static string [] GetFiles (string path)
		{
			return GetFiles (path, "*");
		}
		
		public static string [] GetFiles (string path, string pattern)
		{
			return GetFileSystemEntries (path, pattern, FileAttributes.Directory, 0);
		}

#if NET_2_0
		public static string[] GetFiles (string path, string searchPattern, SearchOption searchOption)
		{
			if (searchOption == SearchOption.TopDirectoryOnly)
				return GetFiles (path, searchPattern);
			ArrayList all = new ArrayList ();
			GetFilesRecurse (path, searchPattern, all);
			return (string []) all.ToArray (typeof (string));
		}
		
		static void GetFilesRecurse (string path, string pattern, ArrayList all)
		{
			all.AddRange (GetFiles (path, pattern));
			foreach (string dir in GetDirectories (path))
				GetFilesRecurse (dir, pattern, all);
		}
#endif

		public static string [] GetFileSystemEntries (string path)
		{
			return GetFileSystemEntries (path, "*");
		}

		public static string [] GetFileSystemEntries (string path, string pattern)
		{
			return GetFileSystemEntries (path, pattern, 0, 0);
		}
		
		public static string[] GetLogicalDrives ()
		{ 
			return Environment.GetLogicalDrives ();
		}

		static bool IsRootDirectory (string path)
		{
			// Unix
		       if (Path.DirectorySeparatorChar == '/' && path == "/")
			       return true;

		       // Windows
		       if (Path.DirectorySeparatorChar == '\\')
			       if (path.Length == 3 && path.EndsWith (":\\"))
				       return true;

		       return false;
		}

		public static DirectoryInfo GetParent (string path)
		{
			if (path == null)
				throw new ArgumentNullException ();
			if (path.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Path contains invalid characters");
			if (path == "")
				throw new ArgumentException ("The Path do not have a valid format");

			// return null if the path is the root directory
			if (IsRootDirectory (path))
				return null;

			string parent_name = Path.GetDirectoryName (path);
			if (parent_name == "")
				parent_name = GetCurrentDirectory();

			return new DirectoryInfo (parent_name);
		}

		public static void Move (string src, string dest)
		{
			if (src == null)
				throw new ArgumentNullException ("src");

			if (dest == null)
				throw new ArgumentNullException ("dest");

			if (src.Trim () == "" || src.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Invalid source directory name: " + src, "src");

			if (dest.Trim () == "" || dest.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Invalid target directory name: " + dest, "dest");

			if (src == dest)
				throw new IOException ("Source directory cannot be same as a target directory.");

			if (Exists (dest))
				throw new IOException (dest + " already exists.");

			if (!Exists (src))
				throw new DirectoryNotFoundException (src + " does not exist");

			MonoIOError error;
			if (!MonoIO.MoveFile (src, dest, out error))
				throw MonoIO.GetException (error);
		}

		public static void SetCreationTime (string path, DateTime creation_time)
		{
			File.SetCreationTime (path, creation_time);
		}

		public static void SetCreationTimeUtc (string path, DateTime creation_time)
		{
			SetCreationTime (path, creation_time.ToLocalTime ());
		}

		[SecurityPermission (SecurityAction.Demand, UnmanagedCode = true)]
		public static void SetCurrentDirectory (string path)
		{
			if (path == null)
				throw new ArgumentNullException ("path");
			if (path.Trim () == String.Empty)
				throw new ArgumentException ("path string must not be an empty string or whitespace string");

			MonoIOError error;
				
			if (!Exists (path))
				throw new DirectoryNotFoundException ("Directory \"" +
									path + "\" not found.");

			MonoIO.SetCurrentDirectory (path, out error);
			if (error != MonoIOError.ERROR_SUCCESS)
				throw MonoIO.GetException (path, error);
		}

		public static void SetLastAccessTime (string path, DateTime last_access_time)
		{
			File.SetLastAccessTime (path, last_access_time);
		}

		public static void SetLastAccessTimeUtc (string path, DateTime last_access_time)
		{
			SetLastAccessTime (path, last_access_time.ToLocalTime ());
		}

		public static void SetLastWriteTime (string path, DateTime last_write_time)
		{
			File.SetLastWriteTime (path, last_write_time);
		}

		public static void SetLastWriteTimeUtc (string path, DateTime last_write_time)
		{
			SetLastWriteTime (path, last_write_time.ToLocalTime ());
		}

		// private
		
		private static void CheckPathExceptions (string path)
		{
			if (path == null)
				throw new System.ArgumentNullException("Path is Null");
			if (path == "")
				throw new System.ArgumentException("Path is Empty");
			if (path.Trim().Length == 0)
				throw new ArgumentException ("Only blank characters in path");
			if (path.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Path contains invalid chars");
		}

		private static string [] GetFileSystemEntries (string path, string pattern, FileAttributes mask, FileAttributes attrs)
		{
			if (path == null || pattern == null)
				throw new ArgumentNullException ();

			if (pattern == String.Empty)
				return new string [] {};
			
			if (path.Trim () == "")
				throw new ArgumentException ("The Path does not have a valid format");

			string wild = Path.Combine (path, pattern);
			string wildpath = Path.GetDirectoryName (wild);
			if (wildpath.IndexOfAny (Path.InvalidPathChars) != -1)
				throw new ArgumentException ("Path contains invalid characters");

			if (wildpath.IndexOfAny (Path.InvalidPathChars) != -1) {
				if (path.IndexOfAny (SearchPattern.InvalidChars) == -1)
					throw new ArgumentException ("Path contains invalid characters", "path");

				throw new ArgumentException ("Pattern contains invalid characters", "pattern");
			}

			MonoIOError error;
			if (!MonoIO.ExistsDirectory (wildpath, out error)) {
				if (error == MonoIOError.ERROR_SUCCESS) {
					MonoIOError file_error;
					if (MonoIO.ExistsFile (wildpath, out file_error)) {
						return new string [] { wildpath };
					}
				}

				if (error != MonoIOError.ERROR_PATH_NOT_FOUND)
					throw MonoIO.GetException (wildpath, error);

				if (wildpath.IndexOfAny (SearchPattern.WildcardChars) == -1)
					throw new DirectoryNotFoundException ("Directory '" + wildpath + "' not found.");

				if (path.IndexOfAny (SearchPattern.WildcardChars) == -1)
					throw new ArgumentException ("Pattern is invalid", "pattern");

				throw new ArgumentException ("Path is invalid", "path");
			}

			string [] result = MonoIO.GetFileSystemEntries (wildpath, pattern, (int) attrs, (int) mask, out error);
			if (error != 0)
				throw MonoIO.GetException (wildpath, error);

			return result;
		}
	}
}

