using PatchDotNet;
using Newtonsoft.Json;
namespace FileSync
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var fs = File.Create("callbackstreamTest");
            var s=new NonSeekableStream(fs);
            
        }
    }
}