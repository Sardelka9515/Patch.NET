using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
        readonly TextField Name;
        readonly Label ID;
        readonly Label Parent;
        readonly Label Defragmented;
        readonly Label Size;
        readonly Button Save;
        readonly Button Mount;
        public PropertiesWindow() : base("Properties")
        {
            lName = new Label("Name:") { Width = 20 };
            lID = new Label("ID:") { Width = 20, Y = Pos.Bottom(lName) };
            lParent = new Label("Parent:") { Width = 20, Y = Pos.Bottom(lID) };
            lDefragmented = new Label("Defragmented:") { Width = 20, Y = Pos.Bottom(lParent) };
            lSize = new Label("Size:") { Width = 20, Y = Pos.Bottom(lDefragmented) };


            Add(lName);
            Add(lID);
            Add(lParent);
            Add(lDefragmented);
            Add(lSize);

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
            Add(Name);
            Add(ID);
            Add(Parent);
            Add(Defragmented);
            Add(Size);

            Save = new("Save")
            {
                Y = Pos.Bottom(this) - 5
            };
            Mount = new("Mount")
            {
                Y = Pos.Y(Save),
                X = Pos.Right(Save)
            };
            Save.Clicked += () =>
            {
                if (_source == null)
                {
                    MessageBox.Query("Error", "There's no snapshot selected", "OK");
                    return;
                }
                var p = new Patch((_source.Tag as PatchNode).Path, true);
                p.Name = (string)Name.Text;
                p.Dispose();
                MessageBox.Query("Sucess", "Changes have been saved", "OK");
                OnSaved?.Invoke(this, _source);
            };
            Add(Save);
            // Add(Mount);
        }
        private TreeNode _source;
        public TreeNode Source
        {
            get => _source;
            set
            {
                var pn = value.Tag as PatchNode;
                if (pn == null)
                {
                    Name.Text = "";
                    ID.Text = "";
                    Parent.Text = "";
                    Defragmented.Text = "";
                    Size.Text = "";
                    return;
                }
                else if (value == _source || pn.Parent == null) { return; }
                var p = new Patch(pn.Path, false);
                Name.Text = pn.Name;
                ID.Text = pn.ID.ToString();
                Parent.Text = pn.Parent.ToString();
                Defragmented.Text = p.LastDefragmented.ToString();
                Size.Text = Util.FormatSize(p.Reader.BaseStream.Length);
                p.Dispose();
                _source = value;
            }
        }
    }
}
