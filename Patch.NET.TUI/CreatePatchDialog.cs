
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.0.18.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace PatchDotNet.TUI {
    using System.Threading.Channels;
    using Terminal.Gui;
    
    
    public partial class CreatePatchDialog {

        Button ok = new("OK");
        Button cancel = new("Cancel");
        public bool Canceled=false;
        public CreatePatchDialog(Guid parent) {
            InitializeComponent();
            var dt=DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            ParentGuid.Text = parent.ToString();
            NewName.Text = dt;
            FilePath.Text = Path.Combine(Program.Store.BaseDirectory,dt+".patch");

            cancel.Clicked += () =>
            {
                Canceled = true;
                Application.RequestStop();
            };
            ok.Clicked += () => {
                Application.RequestStop();
            };
            AddButton(ok);
            AddButton(cancel);
            X = Pos.Center();
            Y = Pos.Center();

            Width = Dim.Percent(85);
            Height = Dim.Percent(85);
        }
    }
}