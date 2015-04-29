using Nullfocus.Dynvoke;
using System;
using System.Collections.Generic;

namespace sample
{
	[DynvokeObject]
	public class Person
	{
		public string First { get; set; }

		public string Last { get; set; }

		public int Age { get; set; }
	}

	[DynvokeClass (AltName = "endpoint")]
	public static class MyJSONEndpoint
	{
		[DynvokeMethod (AltName = "mytest")]
		public static object test ()
		{
			return "hello there, you called test()!";
		}

		public static Person getPerson ()
		{
			return new Person () {
				First = "Bob",
				Last = "Smith",
				Age = 23
			};
		}

		public static void setPerson (Person p)
		{
			Console.WriteLine ("received person: " + p.First + ", " + p.Last + " (" + p.Age + ")");
		}

		public static Dictionary<string, string> environment ()
		{
			Dictionary<string, string> envvars = new Dictionary<string, string> ();

			foreach (var property in typeof(Environment).GetProperties()) {
				string name = property.Name;
				object value = property.GetValue (null);
				envvars.Add (name, (value == null ? "" : value.ToString ()));
			}

			return envvars;
		}

		[DynvokeMethod (AltName = "test")]
		public static string anothercall ()
		{
			//when attempting to call test it actually calls this because of the AltName!
			return "you called anothercall()!";
		}

		public static int add (int x, int y)
		{
			//try calling this one with the generated.js call:  api.endpoint.add(x, y, successFunc, failureFunc)
			return x + y;
		}

		private static void CannotCallThis ()
		{
			//can't call this since it's protected!

			//make helpers protected/private!
		}
	}
}
