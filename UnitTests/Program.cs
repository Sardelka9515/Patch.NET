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
            
            // var mount=new SingleFileMount(new FileProvider(@"C:\test.vhdx",true,@"C:\Patch1"),"base.vhdx", new ConsoleLogger("[Mirror] "));
            var mount=new Mirror(new ConsoleLogger("[Mirror] "),@"C:\");
            using var dokan = new Dokan(new ConsoleLogger("[Dokan] "));
            var safeDokanBuilder = new DokanInstanceBuilder(dokan)
                .ConfigureOptions(options =>
                {
                    options.Options = DokanOptions.MountManager|DokanOptions.DebugMode;
                    options.MountPoint = @"C:\mount";
                }).ConfigureLogger(()=>new ConsoleLogger("[Dokan Logger] "));
            var dokanBuilder = new DokanInstanceBuilder(dokan);
            var dokanInstance = dokanBuilder.Build(mount);
            Console.ReadLine();

            // RecordTest.Run();
            // FileTest.Run();
        }
    }
}