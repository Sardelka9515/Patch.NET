
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.0.18.0
//      Changes to this file may cause incorrect behavior and will be lost if
//      the code is regenerated.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace PatchDotNet.TUI {
    using System;
    using Terminal.Gui;
    
    
    public partial class MergeDialog : Terminal.Gui.Dialog {
        
        private Terminal.Gui.Label label1;

        public Terminal.Gui.TextField MergeLevel;
        
        private Terminal.Gui.Label label12;

        public Terminal.Gui.TextField Name;
        
        private Terminal.Gui.Label label13;
        
        public Terminal.Gui.TextField MergedPath;
        
        private void InitializeComponent() {
            this.MergedPath = new();
            this.label13 = new Terminal.Gui.Label();
            this.Name = new();
            this.label12 = new Terminal.Gui.Label();
            this.MergeLevel = new();
            this.label1 = new Terminal.Gui.Label();
            this.Width = Dim.Fill(0);
            this.Height = Dim.Fill(0);
            this.X = 0;
            this.Y = 0;
            this.Modal = false;
            this.Text = "";
            this.Border.BorderStyle = Terminal.Gui.BorderStyle.Single;
            this.Border.Effect3D = false;
            this.Border.DrawMarginFrame = true;
            this.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Title = "";
            this.label1.Width = 4;
            this.label1.Height = 1;
            this.label1.X = 4;
            this.label1.Y = 1;
            this.label1.Data = "label1";
            this.label1.Text = "Level to merge:";
            this.label1.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.label1);
            this.MergeLevel.Width = 4;
            this.MergeLevel.Height = 1;
            this.MergeLevel.X = 23;
            this.MergeLevel.Y = 1;
            this.MergeLevel.Data = "MergeLevel";
            this.MergeLevel.Text = "1";
            this.MergeLevel.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.MergeLevel);
            this.label12.Width = 4;
            this.label12.Height = 1;
            this.label12.X = 4;
            this.label12.Y = 3;
            this.label12.Data = "label12";
            this.label12.Text = "Name:";
            this.label12.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.label12);
            this.Name.Width = 20;
            this.Name.Height = 1;
            this.Name.X = 23;
            this.Name.Y = 3;
            this.Name.Data = "Name";
            this.Name.Text = "merged-name";
            this.Name.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.Name);
            this.label13.Width = 4;
            this.label13.Height = 1;
            this.label13.X = 4;
            this.label13.Y = 5;
            this.label13.Data = "label13";
            this.label13.Text = "Path:";
            this.label13.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.label13);
            this.MergedPath.Width = 20;
            this.MergedPath.Height = 1;
            this.MergedPath.X = 23;
            this.MergedPath.Y = 5;
            this.MergedPath.Data = "Name2";
            this.MergedPath.Text = "merged-path";
            this.MergedPath.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.MergedPath);
        }
    }
}
