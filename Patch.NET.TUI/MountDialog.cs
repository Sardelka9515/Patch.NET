
//------------------------------------------------------------------------------

//  <auto-generated>
//      This code was generated by:
//        TerminalGuiDesigner v1.0.18.0
//      You can make changes to this file and they will not be overwritten when saving.
//  </auto-generated>
// -----------------------------------------------------------------------------
namespace PatchDotNet.TUI
{
    using Terminal.Gui;


    public partial class MountDialog : Dialog
    {
        Button ok = new("OK");
        Button cancel = new("Cancel");
        public MountEventArgs Result;
        public MountDialog(Guid guid, string _mountPoint, string _filename, bool _readOnly) : base("Mount", 0, 0)
        {
            InitializeComponent();
            mountPoint.Text = _mountPoint;
            filename.Text = _filename;
            readOnly.Checked = _readOnly;
            id.Text = guid.ToString();
            cancel.Clicked += () =>
                Application.RequestStop();
            ok.Clicked += () =>
            {
                Result = new MountEventArgs()
                {
                    MountPoint = (string)mountPoint.Text,
                    Filename = (string)filename.Text,
                    ReadOnly = readOnly.Checked,
                };
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
