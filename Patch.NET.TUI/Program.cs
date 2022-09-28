using Terminal.Gui;
using PatchDotNet;
using Terminal.Gui.Trees;
using System.Runtime.InteropServices;
using DokanNet;
using PatchDotNet.Win32;
using System.Diagnostics;

namespace PatchDotNet.TUI
{
    internal class Program
    {
        public static FileStore Store;
        static Toplevel Top;
        public static FileProvider Provider;
        static Window SnapshotManager;
        static string MountedPath;
        static DokanInstance Win32Mount;
        static Dokan Dokan;
        static TreeView PatchView = new TreeView()
        {
            Width = 50,
            Height = Dim.Fill(),
            Visible = false,
        };
        static PropertiesWindow Properties;
        static Settings Settings = Util.ReadJson<Settings>("Settings.json");
        static void Main(string[] args)
        {
            Application.Init();
            Colors.Base.Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black);
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

                    if (Provider == null)
                    {
                        Mount();
                        mountItem.Title = "Unmount";
                    }
                    else
                    {
                        Unmount();
                        mountItem.Title = "Mount";
                        Provider = null;
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
                    new MenuItem ("Close", "",()=>Close()),
                    new MenuItem ("Recover...", "",()=>Recover()),
                    new MenuItem ("Quit", "", () => {Close(); Top.Running = false; })
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
                Visible = false
            };
            Properties.OnSaved += (s, e) => UpdateNode(e);
            PatchView.SelectionChanged += (s, e) => Properties.Source = (TreeNode)e.NewValue;
            SnapshotManager.Add(Properties);
            SnapshotManager.Add();
            SnapshotManager.Add(PatchView);
            Top.Add(menu, SnapshotManager);
            Application.Run();
        }
        static void Recover()
        {
            Try(() =>
            {
                if (Store == null)
                {
                    throw new Exception("Please open a FileStore first");
                }
                var open = new OpenDialog("Recover orphan patch", "Open patch", null);
                open.AllowsMultipleSelection = false;
                Application.Run(open);
                if (open.Canceled)
                {
                    return;
                }
                Store.RecoverOrphanPatch((string)open.FilePath);
                RebuildTree(PatchView.SelectedObject.Tag as PatchNode);
            });
        }
        static void Close()
        {

            Store = null;
            Properties.Source = null;
            Settings.LastSelected = (PatchView.SelectedObject?.Tag as PatchNode)?.ID.ToString();
            Save();
            PatchView.Visible = false;
        }
        static void UpdateNode(ITreeNode n)
        {

            var pn = n.Tag as PatchNode;
            pn.Update();
            n.Text = pn.Name;
            Properties.Update();
        }
        public static void Unmount()
        {
            /*
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && (MountedPath.EndsWith(".vhd") || MountedPath.EndsWith(".vhdx")))
            {
                VdiskUtil.DetachVhd(MountedPath);
            }
            */
            if (Provider.Streams.Length>0 && MessageBox.Query("Warning", "Some programs may still be using the file, do you want to forcibly unmount?", "OK", "Cancel") != 0) { return; }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {

                Win32Mount?.Dispose();
                Win32Mount = null;
                Dokan?.Dispose();
                Dokan = null;
            }
            else
            {
                Provider.Dispose();
                Provider = null;
                throw new PlatformNotSupportedException("This platform does not currently support virtual file mount");
            }
            Provider.Dispose();
            Provider = null;
        }

        public static void Mount()
        {
            if (Provider != null)
            {
                throw new InvalidOperationException("Already mounted");
            }
            var node = PatchView.SelectedObject.Tag as PatchNode;
            if (node == Store.Root)
            {
                throw new InvalidOperationException("Cannot mount base file, considering creating a snapshot and mount again");
            }
            var mountDialog = new MountDialog(node, Settings.MountPoint, Settings.Filename, Settings.MountReadonly);
            Application.Run(mountDialog);
            var e = mountDialog.Result;
            if (e == null) { return; }
            Provider = Store.GetProvider(node, !e.ReadOnly);
            MountedPath = Path.Combine(e.MountPoint, e.Filename);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var mount = new SingleFileMount(Provider, e.Filename);
                Directory.CreateDirectory(e.MountPoint);
                if (Directory.EnumerateFileSystemEntries(e.MountPoint).Any())
                {
                    throw new InvalidOperationException("Mount directory must be empty");
                }
                Dokan = new Dokan(null);
                var builder = new DokanInstanceBuilder(Dokan)
                    .ConfigureOptions(options =>
                    {
                        options.Options = DokanOptions.MountManager | (e.ReadOnly ? DokanOptions.WriteProtection : 0);
                        options.MountPoint = Path.GetFullPath(e.MountPoint);
                    });
                Win32Mount = builder.Build(mount);
                // MessageBox.Query("Info", "Filesystem mounted at: " + Path.GetFullPath(e.MountPoint), "Ok");
                /*
                 if ((e.Filename.EndsWith(".vhd") || e.Filename.EndsWith(".vhdx")) && MessageBox.Query("Mount vhd", "Mount vhd file?", "OK", "Cancel") == 0)
                {
                    Try(() => VdiskUtil.AttachVhd(MountedPath, e.ReadOnly));
                }
                 */
            }
            else
            {
                throw new PlatformNotSupportedException("This platform does not currently support virtual file mount");
            }

            Settings.MountPoint = e.MountPoint;
            Settings.Filename = e.Filename;
            Settings.MountReadonly = e.ReadOnly;
            Save();
        }
        static void Save()
        {
            Util.WriteJson(Settings, "Settings.json");
        }
        static void Open()
        {
            var open = new OpenDialog("Open FileStore", "Open a FileStore json", new() { ".pfs" });
            open.AllowsMultipleSelection = false;
            Application.Run(open);
            if (open.Canceled)
            {
                return;
            }
            if (!File.Exists((string)open.FilePath))
            {
                throw new FileNotFoundException("Cannot locate file: " + (string)open.FilePath);
            }
            try
            {
                Store = new(FileStoreInfo.FromJson((string)open.FilePath));
            }
            catch (FileNotFoundException)
            {
                if (MessageBox.ErrorQuery("Oops", "Looks like some files are missing, would you like to remove these entries? \n(You can recover them later using File -> Recover...)", "Yes", "No") == 0)
                {
                    Store = new(FileStoreInfo.FromJson((string)open.FilePath), true, out var deleted);
                    MessageBox.Query("Success", "The following entries have been removed:\n" + String.Join('\n', deleted), "OK");
                }
                else
                {
                    throw;
                }
            }
            RebuildTree(Guid.Parse(Settings.LastSelected));
        }
        public static void RebuildTree(Guid toExpand = default)
        {

            PatchView.ColorGetter = (node) =>
            {
                if ((PatchNode)node.Tag == Provider?.CurrentGuid)
                {
                    return Colors.Dialog;
                }
                return Colors.Base;
            };
            PatchView.ClearObjects();
            if (Store == null) { return; }
            var rootNode = new TreeNode("Base");
            rootNode.Tag = Store.Root;
            PatchView.AddObject(rootNode);
            List<TreeNode> nodes = new();
            Add(rootNode);
            void Add(TreeNode node)
            {
                nodes.Add(node);
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
            var nodeToExpand = nodes.Where(x => x.Tag as PatchNode == toExpand).FirstOrDefault();
            if (nodeToExpand != null)
            {
                var chain = new List<TreeNode>() { nodeToExpand };
                while (chain[0] != rootNode)
                {
                    chain.Insert(0, nodes.Where(x => x.Children.Contains(chain[0])).First());
                }
                chain.ForEach(x => PatchView.Expand(x));
                PatchView.SelectedObject = nodeToExpand;
                PatchView.SetFocus();
            }

            PatchView.Visible = true;
            SnapshotManager.Title = Store.Name;
        }
        static void Try(Action a)
        {
            try
            {
                a.Invoke();
            }
            catch (Exception ex)
            {
#if DEBUG
                MessageBox.ErrorQuery("Error", ex.ToString(), "OK");
#else
                MessageBox.ErrorQuery("Error", ex.Message, "OK");
#endif

            }
        }
    }

    public class MountEventArgs
    {
        public string MountPoint;
        public string Filename;
        public bool ReadOnly;
    }
}