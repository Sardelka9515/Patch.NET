using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PatchDotNet;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace PatchDotNet.TUI
{
    internal class PropertiesWindow : Window
    {
        public event EventHandler<TreeNode> OnSaved;
        readonly Label lName;
        readonly Label lID;
        readonly Label lParent;
        readonly Label lDefragmented;
        readonly Label lSize;
        readonly Label lPath;
        readonly Label lRecordsCount;
        readonly TextField Name;
        readonly Label ID;
        readonly Label Parent;
        readonly Label Defragmented;
        readonly Label RecordsCount;
        readonly Label Size;
        readonly Label Path;
        readonly Button Save;
        readonly Button Mount;
        readonly Button Fork;
        readonly Button Delete;
        readonly Button Merge;
        public PropertiesWindow() : base("Properties")
        {
            lName = new Label("Name:") { Width = 20 };
            lID = new Label("ID:") { Width = 20, Y = Pos.Bottom(lName) };
            lParent = new Label("Parent:") { Width = 20, Y = Pos.Bottom(lID) };
            lDefragmented = new Label("Defragmented:") { Width = 20, Y = Pos.Bottom(lParent) };
            lSize = new Label("Size:") { Width = 20, Y = Pos.Bottom(lDefragmented) };
            lPath = new Label("Path:") { Width = 20, Y = Pos.Bottom(lSize) };
            lRecordsCount = new Label("Records:") { Width = 20, Y = Pos.Bottom(lPath) };


            Add(lName);
            Add(lID);
            Add(lParent);
            Add(lDefragmented);
            Add(lSize);
            Add(lPath);
            // Add(lRecordsCount);

            Name = new TextField()
            {
                X = Pos.Right(lName),
                Y = Pos.Y(lName),
                Width = Dim.Fill(10),
            };
            ID = new()
            {
                X = Pos.Right(lID),
                Y = Pos.Y(lID),
                Width = Dim.Fill(10),
            };
            Parent = new()
            {
                X = Pos.Right(lParent),
                Y = Pos.Y(lParent),
                Width = Dim.Fill(10),
            };
            Defragmented = new()
            {
                X = Pos.Right(lDefragmented),
                Y = Pos.Y(lDefragmented),
                Width = Dim.Fill(10),
            };
            Size = new()
            {
                X = Pos.Right(lSize),
                Y = Pos.Y(lSize),
                Width = Dim.Fill(10),
            }; 
            Path = new()
            {
                X = Pos.Right(lPath),
                Y = Pos.Y(lPath),
                Width = Dim.Fill(10),
            };
            RecordsCount = new()
            {
                X = Pos.Right(lRecordsCount),
                Y = Pos.Y(lRecordsCount),
                Width = Dim.Fill(10),
            };
            Add(Name);
            Add(ID);
            Add(Parent);
            Add(Defragmented);
            Add(Size);
            Add(Path);
            // Add(RecordsCount);

            Save = new("Save")
            {
                Y = Pos.Bottom(this) - 5
            };
            Mount = new("Mount")
            {
                Y = Pos.Y(Save),
                X = Pos.Right(Save)
            };
            Fork = new("Fork")
            {
                Y = Pos.Y(Mount),
                X = Pos.Right(Mount)
            };
            Delete = new("Delete")
            {
                Y = Pos.Y(Mount),
                X = Pos.Right(Fork)
            };
            Merge = new("Optimize & Merge")
            {
                Y = Pos.Y(Mount),
                X = Pos.Right(Delete)
            };
            Merge.Clicked += (() =>
            {
                Try(() =>
                {
                    var d = new MergeDialog(SourcePatch);
                    Application.Run(d);
                    if (d.Canceled) { return; }
                    if (Program.Provider != null && Program.Provider.Patches.Any(x => x.Path == SourcePatch.Path))
                    {
                        throw new Exception("Cannot merge a patch that's currently mounted");
                    }
                    var level = int.Parse((string)d.MergeLevel.Text);
                    if (Program.Store.Merge(SourcePatch,level
                        , (string)d.MergedPath.Text, (string)d.Name.Text, (c, a, cLen, nLen) =>
                    {
                        if (c <= a && level==1)
                        {
                            MessageBox.Query("Hmm", $"No need to merge {c}, {a}", "OK");
                            return false;
                        }
                        return MessageBox.Query("Merge records",
                            $"Current records: {c}" +
                            $"\nAfter merge: {a}" +
                            $"\nCurrent size: {Util.FormatSize(cLen)}" +
                            $"\nMerged size: {Util.FormatSize(nLen)}", "OK", "Cancel") == 0;
                    }))
                    {
                        MessageBox.Query("Info", "Merged", "OK");
                    }
                    Program.RebuildTree(SourcePatch);
                    Update();
                });
            });

            Delete.Clicked += () =>
            {
                Try(() =>
                {
                    if (Program.Store == null || Source == null || SourcePatch.IsRoot)
                    {
                        throw new Exception("Please select the patch to be deleted");
                    }
                    if (MessageBox.Query("Delete patch", "All children patches and file will be deleted, are you sure?", "OK", "Cancel") == 0)
                    {
                        Program.Store.RemovePatch(SourcePatch, true, true);
                    }
                    Program.RebuildTree(SourcePatch.Parent ?? default);
                });
            };
            Fork.Clicked += () =>
            {
                Try(() =>
                {
                    Update();
                    var prov = Program.Provider;
                    var dialog = new CreatePatchDialog(Source.Tag as PatchNode);
                    Application.Run(dialog);
                    if (dialog.Canceled) { return; }
                    var path = (string)dialog.FilePath.Text;
                    var name = (string)dialog.NewName.Text;
                    PatchNode newNode;
                    if (prov != null && SourcePatch == prov.CurrentGuid)
                    {
                        newNode = Program.Store.CreatePatch(Program.Provider, path, name);
                    }
                    else if (SourcePatch.IsRoot)
                    {
                        newNode = Program.Store.CreatePatch(path, name);
                    }
                    else
                    {
                        newNode = Program.Store.CreatePatch(SourcePatch.ID, path, name);
                    }
                    Program.RebuildTree(newNode);
                });
            };
            Mount.Clicked += () =>
            {
                Try(() =>
                {
                    if (Mount.Text == "Mount")
                    {
                        Program.Mount();
                    }
                    else
                    {
                        Program.Unmount();
                    }
                });
                Update();
            };
            Save.Clicked += () =>
            {
                Try(() =>
                {
                    if (_source == null || (_source.Tag as PatchNode).IsRoot)
                    {
                        throw new Exception("There's no patch selected");
                    }
                    var p = new Patch((_source.Tag as PatchNode).Path, true);
                    p.Name = (string)Name.Text;
                    p.Dispose();
                    MessageBox.Query("Sucess", "Changes have been saved", "OK");
                    OnSaved?.Invoke(this, _source);
                });
            };
            Add(Save);
            Add(Mount);
            Add(Fork);
            Add(Delete);
            Add(Merge);
        }
        private TreeNode _source;
        public void Update()
        {
            if (Source?.Tag is not PatchNode pn)
            {
                Name.Text = "";
                ID.Text = "";
                Parent.Text = "";
                Defragmented.Text = "";
                Size.Text = "";
                Path.Text = "";
                RecordsCount.Text = "";
                return;
            }
            pn.Update();
            Name.Text = pn.Name;
            ID.Text = pn.ID.ToString();
            Parent.Text = pn.Parent?.ID.ToString();
            Defragmented.Text = pn.LastDefragmented.ToString();
            Size.Text = Util.FormatSize(new FileInfo(pn.Path).Length);
            Path.Text = pn.Path;
            if (Program.Provider == null)
            {
                // RecordsCount.Text = pn.GetRecordsCount(CorruptConfirm).ToString();
            }

            if (Program.Provider != null && Program.Provider.CurrentGuid == (Source?.Tag as PatchNode)?.ID)
            {
                Mount.Text = "Unmount";
            }
            else
            {
                Mount.Text = "Mount";
            }
            if (Program.Provider != null && Program.Provider.CurrentGuid == SourcePatch)
            {
                Fork.Text = "Snapshot & Fork";
            }
            else
            {
                Fork.Text = "Fork";
            }

        }
        public static bool CorruptConfirm(long pos,long length)
        {
            return MessageBox.ErrorQuery("Error", $"Corrupted record after position {pos}, do you want to discard the data after it?" +
                $"\n({Util.FormatSize(length - pos)} data will be lost)", "OK", "Cancel") == 0 ;
        }
        public TreeNode Source
        {
            get => _source;
            set
            {
                _source = value;
                if (_source == null) { Visible = false; }
                else { Visible = true; }
                Update();
            }
        }
        public PatchNode SourcePatch => Source?.Tag as PatchNode;

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
}
