
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
    
    
    public partial class MountDialog : Terminal.Gui.Dialog {
        
        private Terminal.Gui.Label label1;
        
        private Terminal.Gui.Label id;
        
        private Terminal.Gui.Label label12;
        
        private Terminal.Gui.TextField mountPoint;
        
        private Terminal.Gui.Label id2;
        
        private Terminal.Gui.TextField filename;
        
        private Terminal.Gui.CheckBox readOnly;
        
        private void InitializeComponent() {
            this.readOnly = new Terminal.Gui.CheckBox();
            this.filename = new Terminal.Gui.TextField();
            this.id2 = new Terminal.Gui.Label();
            this.mountPoint = new Terminal.Gui.TextField();
            this.label12 = new Terminal.Gui.Label();
            this.id = new Terminal.Gui.Label();
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
            this.label1.X = 2;
            this.label1.Y = 0;
            this.label1.Data = "label1";
            this.label1.Text = "ID:";
            this.label1.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.label1);
            this.id.Width = 30;
            this.id.Height = 1;
            this.id.X = 20;
            this.id.Y = 0;
            this.id.Data = "id";
            this.id.Text = "GUID-text";
            this.id.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.id);
            this.label12.Width = 4;
            this.label12.Height = 1;
            this.label12.X = 2;
            this.label12.Y = 1;
            this.label12.Data = "label12";
            this.label12.Text = "Mount point:";
            this.label12.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.label12);
            this.mountPoint.Width = 30;
            this.mountPoint.Height = 1;
            this.mountPoint.X = 20;
            this.mountPoint.Y = 1;
            this.mountPoint.Secret = false;
            this.mountPoint.Data = "mountPoint";
            this.mountPoint.Text = "mount";
            this.mountPoint.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.mountPoint);
            this.id2.Width = 4;
            this.id2.Height = 1;
            this.id2.X = 2;
            this.id2.Y = 2;
            this.id2.Data = "id2";
            this.id2.Text = "Filename:";
            this.id2.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.id2);
            this.filename.Width = 30;
            this.filename.Height = 1;
            this.filename.X = 20;
            this.filename.Y = 2;
            this.filename.Secret = false;
            this.filename.Data = "filename";
            this.filename.Text = "file.vhdx";
            this.filename.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.Add(this.filename);
            this.readOnly.Width = 6;
            this.readOnly.Height = 1;
            this.readOnly.X = 2;
            this.readOnly.Y = 4;
            this.readOnly.Data = "readOnly";
            this.readOnly.Text = "Read-only";
            this.readOnly.TextAlignment = Terminal.Gui.TextAlignment.Left;
            this.readOnly.Checked = false;
            this.Add(this.readOnly);
        }
    }
}
