using PatchDotNet;
using PatchDotNet.Win32;
using DokanNet;
using DokanNet.Logging;
namespace UnitTests
{
    public class Program
    {
        public static void Main(string[] args)
        {
            FileTest.Run();
            RecordTest.Run();
        }
    }
}