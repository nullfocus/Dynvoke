using Nullfocus.Dynvoke;
using System.Threading;

namespace sample
{
    class Program
    {
        static void Main(string[] args)
        {
            using (HttpDynvokeServer server = new HttpDynvokeServer("localhost", 6543, "api"))
            {
                server.FindTargets();
                server.Start();

				while (true) {
					Thread.Sleep (1000);
				}

                server.Stop();
            }
        }
    }
}
