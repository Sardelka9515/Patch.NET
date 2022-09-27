using Terminal.Gui;
using PatchDotNet;
using Terminal.Gui.Trees;
using System.Runtime.InteropServices;

namespace PatchDotNet.TUI
{
    internal class Program
    {
        static FileStore Store;
        static Toplevel Top;
        static FileProvider provider;
        static Window SnapshotManager;
        static Win32.SingleFileMount Win32Mount;
        static TreeView PatchView = new TreeView()
        {
            Width = 50,
            Height = Dim.Fill(),
            Visible = false,
        };
        static PropertiesWindow Properties;
        static void Main(string[] args)
        {
            Application.Init();
            var mountItem = new MenuItem("Mount", "", null);
            mountItem.Action = () =>
            {
                if (PatchView.SelectedObject == null)
                {
                    MessageBox.Query("Error", "There's no snapshot selected", "OK");
                    return;
                }
                try
                {

                    if (provider == null)
                    {
                        Mount();
                        mountItem.Title = "Unmount";
                    }
                    else
                    {
                        Unmount();
                        mountItem.Title = "Mount";
                        provider = null;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Query("Error", ex.Message, "OK");
                }
            };
            Top = Application.Top;// Creates a menubar, the item "New" has a help menu.
            SnapshotManager = new Window("No FileStore slected")
            {
                X = 0,
                Y = 1, // Leave one row for the toplevel menu

                // By using Dim.Fill(), it will automatically resize without manual intervention
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };
            var menu = new MenuBar(new MenuBarItem[] {
                new MenuBarItem ("File", new MenuItem [] {
                    new MenuItem ("Open", "",Open),
                    new MenuItem ("New", "", null),
                    new MenuItem ("Close", "",()=>{ Store=null;Properties.Source=null;RebuildTree(); }),
                    new MenuItem ("Quit", "", () => { Top.Running = false; })
                }),
                new MenuBarItem ("Edit", new MenuItem [] {
                    mountItem
                })
            });
            Properties = new()
            {
                X = Pos.Right(PatchView),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Visible = true
            };
            Properties.OnSaved += (s, e) =>
            {
                var pn = e.Tag as PatchNode;
                pn.Update();
                e.Text = pn.Name;
            };
            PatchView.SelectionChanged += (s, e) => Properties.Source = (TreeNode)e.NewValue;

            SnapshotManager.Add(Properties);
            SnapshotManager.Add(PatchView);
            Top.Add(menu, SnapshotManager);
            Application.Run();
        }

        private static void Unmount()
        {
            provider.Dispose();
            provider = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Win32Mount = null;
            }
            else
            {
                throw new PlatformNotSupportedException("This platform does not currently support virtual file mount");
            }
        }

        static void Mount()
        {
            var mountDialog;
            provider = Store.GetProvider(PatchView.SelectedObject.Tag as PatchNode,);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

            }
            else
            {
                throw new PlatformNotSupportedException("This platform does not currently support virtual file mount");
            }
        }
        static void Open()
        {
            var open = new OpenDialog("Open FileStore", "Open a FileStore json", new() { ".json" });
            open.AllowsMultipleSelection = false;
            Application.Run(open);
            if (open.Canceled)
            {
                return;
            }
            Store = new(FileStoreInfo.FromJson((string)open.FilePath));
            RebuildTree();
        }
        static void RebuildTree()
        {
            PatchView.ClearObjects();
            if (Store == null) { return; }
            var rootNode = new TreeNode("Base");
            rootNode.Tag = Store.Root;
            PatchView.AddObject(rootNode);
            Add(rootNode);
            void Add(TreeNode node)
            {
                foreach (var p in (node.Tag as PatchNode).Children)
                {
                    var newNode = new TreeNode(p.Name)
                    {
                        Tag = p
                    };
                    Add(newNode);
                    node.Children.Add(newNode);
                }
            }
            PatchView.Visible = true;
            SnapshotManager.Title = Store.Name;
        }
    }
}