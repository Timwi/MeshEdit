using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RT.Util;

[assembly: AssemblyTitle("MeshEdit")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("MeshEdit")]
[assembly: AssemblyCopyright("Copyright © Timwi 2016")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]
[assembly: ComVisible(false)]
[assembly: Guid("e9ab1223-ae74-41ed-a93d-fb206a00cfd8")]
[assembly: AssemblyVersion("1.0.9999.9999")]
[assembly: AssemblyFileVersion("1.0.9999.9999")]

namespace MeshEdit
{
    static class Program
    {
        public static Settings Settings;

        [STAThread]
        static void Main()
        {
            SettingsUtil.LoadSettings(out Settings);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool doOpenFile = false;
            if (Settings.Filename == null || !File.Exists(Settings.Filename))
            {
                using (var dlg = new OpenFileDialog { DefaultExt = "obj", Filter = "OBJ files (*.obj)|*.obj|All files (*.*)|*.*" })
                {
                    if (Settings.LastDir != null)
                        dlg.InitialDirectory = Settings.LastDir;
                    var result = dlg.ShowDialog();
                    if (result == DialogResult.Cancel)
                        return;
                    Settings.Filename = dlg.FileName;
                    Settings.LastDir = Path.GetDirectoryName(dlg.FileName);
                    doOpenFile = true;
                }
            }

            Application.Run(new Mainform(doOpenFile));
            Settings.Save(onFailure: SettingsOnFailure.ShowRetryWithCancel);
        }
    }
}
