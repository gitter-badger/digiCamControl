using System;
using System.IO;
using System.Xml;
using WixSharp;
using System;
using System.IO;
using Microsoft.Win32;
using System.Diagnostics;
using WixSharp.CommonTasks;
using File = WixSharp.File;
using System.Windows.Forms;
using Microsoft.Deployment.WindowsInstaller;
using System.Security.AccessControl;
using System.Security.Principal;

namespace Setup
{
    internal class Script
    {
        public static void Main(string[] args)
        {

            Feature appFeature = new Feature("Application files", "Main application files", true, false, @"INSTALLDIR");
            Feature obsPlugin = new Feature("Obs Plugin");

            var appDir = new Dir(@"digiCamControl",
                new File(appFeature, "CameraControl.exe",
                    new FileShortcut(appFeature,"digiCamControl", @"%ProgramMenu%\digiCamControl"),
                    new FileShortcut(appFeature, "digiCamControl", @"%Desktop%")),
                new File(appFeature, "CameraControl.PluginManager.exe"),
                new File(appFeature, "CameraControlCmd.exe"),
                new File(appFeature, "CameraControlRemoteCmd.exe"),
                new File(appFeature, "dcraw.exe"),
                new File(appFeature, "ffmpeg.exe"),
                new File(appFeature, "ngrok.exe"),
                new File(appFeature, "MtpTester.exe"),
                //new File(appFeature, "PhotoBooth.exe",new FileShortcut(appFeature, "PhotoBooth", @"%ProgramMenu%\digiCamControl")),
                new DirFiles(appFeature, @"*.dll"),
#if DEBUG
                new DirFiles(appFeature, @"*.pdb"),
#endif
                new File(appFeature, "regwia.bat"),
                new File(appFeature, "logo.ico"),
                new File(appFeature, "logo_big.jpg"),
                new File(appFeature, "baseMtpDevice.xml"),
                new DirFiles(appFeature, @"*.png"),
                new File(appFeature, "DigiCamControl.xbs"),
                new Dir(appFeature, "Data",
                    new Files(appFeature, @"Data\*.*")),
                //new Dir(appFeature, "Plugins",
                //    new Files(appFeature, @"Plugins\*.*", "MahApps.Metro.*", "System.Windows.Interactivity.*",
                //        "WriteableBitmapEx.Wpf.*", "GalaSoft.MvvmLight.*", "*.config")),
                new Dir(appFeature, "Plugins",
                    new Dir(appFeature, "CameraControl.Plugins",
                        new File(appFeature, "Plugins\\CameraControl.Plugins\\CameraControl.Plugins.dll"),
                        new File(appFeature, "Plugins\\CameraControl.Plugins\\dcc.plugin")),
                    new Dir(appFeature, "Plugin.DeviceControlBox",
                        new File(appFeature, "Plugins\\Plugin.DeviceControlBox\\Plugin.DeviceControlBox.dll"),
                        new File(appFeature, "Plugins\\Plugin.DeviceControlBox\\dcc.plugin"))
                    ),
                new Dir(appFeature, "Languages",
                    new DirFiles(appFeature, @"Languages\*.xml")),
                new Dir(appFeature, "Licenses",
                    new DirFiles(appFeature, @"Licenses\*.*")),
                new Dir(appFeature, "Tools",
                    new DirFiles(appFeature, @"Tools\*.*")),
                new Dir(appFeature, "WebServer",
                    new Files(appFeature, @"WebServer\*.*"))
                );
            

            var obsDir = new Dir(@"OBS\plugins",
                new File(obsPlugin, @"ObsPlugin\CLRHostPlugin.dll"),
                new Dir(obsPlugin, "CLRHostPlugin",
                    new DirFiles(obsPlugin, @"ObsPlugin\CLRHostPlugin\*.*")
                    ));

            var baseDir = new Dir(@"%ProgramFiles%\",
                appDir ,
                obsDir
                );


            Project project = new Project("digiCamControl",
                new LaunchCondition("NET40=\"#1\"", "Please install .NET 4.0 first."),
                baseDir,
                new RegValueProperty("NET40", RegistryHive.LocalMachine,
                    @"Software\Microsoft\NET Framework Setup\NDP\v4\Full", "Install", "0"),
                new ManagedAction(@"MyAction", Return.ignore, When.Before, Step.LaunchConditions,
                    Condition.NOT_Installed, Sequence.InstallUISequence),
                new ManagedAction(@"SetRightAction", Return.ignore, When.Before, Step.InstallFinalize,
                    Condition.Always, Sequence.InstallExecuteSequence)
                );
            
            project.UI = WUI.WixUI_FeatureTree;
            project.GUID = new Guid("19d12628-7654-4354-a305-9ab0932af676");

#if DEBUG            
            project.SourceBaseDir =
                Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, @"..\CameraControl\bin\Debug\"));
#else
            project.SourceBaseDir =
                Path.GetFullPath(System.IO.Path.Combine(Environment.CurrentDirectory, @"..\CameraControl\bin\Release\"));
#endif
            project.ResolveWildCards();

            FileVersionInfo ver =
                FileVersionInfo.GetVersionInfo(Path.Combine(project.SourceBaseDir, "CameraControl.exe"));

            project.LicenceFile = @"Licenses\DigiCamControlLicence.rtf";
            
            project.Version = new Version(ver.FileMajorPart, ver.FileMinorPart, ver.FileBuildPart, ver.FilePrivatePart);
            project.MajorUpgradeStrategy = MajorUpgradeStrategy.Default;
            project.MajorUpgradeStrategy.RemoveExistingProductAfter = Step.InstallInitialize;

            project.ControlPanelInfo.Manufacturer = "Duka Istvan";
            project.OutFileName = string.Format("digiCamControlsetup_{0}", ver.FileVersion);
            project.ControlPanelInfo.ProductIcon = "logo.ico";
            
            string branding = Path.Combine(project.SourceBaseDir, "branding.xml");
            if (System.IO.File.Exists(branding))
            {
                var doc = new XmlDocument();
                doc.LoadXml(System.IO.File.ReadAllText(branding));
                string name = doc.DocumentElement.SelectSingleNode("/Branding/ApplicationTitle").InnerText;
                project.ControlPanelInfo.Manufacturer = name;
                project.OutFileName = string.Format(name+"_{0}", ver.FileVersion);
                appDir.AddFile(new File(appFeature, "branding.xml"));
                project.Name = name;
            }

            Compiler.PreserveTempFiles = true;
            Compiler.BuildMsi(project);

        }

    }

    public class CustomActions
    {
        [CustomAction]
        public static ActionResult MyAction(Session session)
        {
            var unInstallFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "digiCamControl", "uninstall.exe");
            if (System.IO.File.Exists(unInstallFile))
            {
                try
                {
                    MessageBox.Show("Older version is installed, first should be UnInstalled !", "Installation" );
                    ProcessStartInfo Pro = new ProcessStartInfo();
                    Pro.Verb = "runas";
                    Pro.UseShellExecute = true;
                    Pro.FileName = unInstallFile;
                    Pro.Arguments = "/S";
                    Pro.WindowStyle = ProcessWindowStyle.Normal;
                    Process process=new Process();
                    process.StartInfo = Pro;
                    process.Start();
                    process.WaitForExit();
                }
                catch (Exception)
                {


                }
            }
            return ActionResult.Success;
        }

        [CustomAction]
        public static ActionResult SetRightAction(Session session)
        {
            try
            {
                string folder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "digiCamControl");
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
                DirectoryInfo dInfo = new DirectoryInfo(folder);
                DirectorySecurity dSecurity = dInfo.GetAccessControl();
                SecurityIdentifier everyone = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                dSecurity.AddAccessRule(new FileSystemAccessRule( everyone, FileSystemRights.FullControl, InheritanceFlags.ObjectInherit | InheritanceFlags.ContainerInherit, PropagationFlags.InheritOnly, AccessControlType.Allow));
                dInfo.SetAccessControl(dSecurity);    

            }
            catch (Exception ex)
            {
                MessageBox.Show("Set right error "+ex.Message);
            }
            return ActionResult.Success;
        }
    }
}
