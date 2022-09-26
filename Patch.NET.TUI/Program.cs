using Terminal.Gui;

namespace PatchDotNet.TUI
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Application.Init();
            var open = new OpenDialog("Open FileStore","Open a FileStore json", new() {".json" });
            open.AllowsMultipleSelection = false;
            var top=Application.Top;// Creates a menubar, the item "New" has a help menu.
            var menu = new MenuBar(new MenuBarItem[] {
            new MenuBarItem ("File", new MenuItem [] {
                new MenuItem ("Open", "",()=> Application.Run(open)),
                new MenuItem ("New", "", null),
                new MenuItem ("Close", "",null),
                new MenuItem ("Quit", "", () => { top.Running = false; })
            }),
            new MenuBarItem ("Edit", new MenuItem [] {
                new MenuItem ("Copy", "", null),
                new MenuItem ("Cut", "", null),
                new MenuItem ("Paste", "", null)
            })
        });
            top.Add(menu);
            var win = new Window("Patch.NET.TUI")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            top.Add(win);
            Application.Run();
        }
    }
}