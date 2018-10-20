using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RuntimeSwitcher
{
    public partial class MainWindow : Form
    {
        private static readonly Func<string, bool> STEAMVR_TEST = s => s.Contains("SteamVR");

        private ConfigReader config;
        private string ocRuntimePath;
        private string ocBinPath;
        private string revisionFilePath;

        private SemaphoreSlim dllDownloadMutex = new SemaphoreSlim(1);

        public MainWindow()
        {
            ocRuntimePath = Directory.GetParent(System.Reflection.Assembly.GetEntryAssembly().Location).FullName;
            ocRuntimePath += Path.DirectorySeparatorChar + "Runtime";
            ocBinPath = ocRuntimePath + Path.DirectorySeparatorChar + "bin";
            revisionFilePath = ocRuntimePath + Path.DirectorySeparatorChar + "revision.txt";

            if (!Directory.Exists(ocRuntimePath)) {
                Directory.CreateDirectory(ocRuntimePath);
            }

            if (!Directory.Exists(ocBinPath))
            {
                Directory.CreateDirectory(ocBinPath);
            }

            config = new ConfigReader();

            InitializeComponent();

            UpdateStatus();

            if(File.Exists(revisionFilePath))
            {
                CheckForUpdate();
            }
        }

        private void UpdateStatus()
        {
            string runtime = config.Runtimes.Count == 0 ? null : config.Runtimes.First();
            useOpenComposite.Enabled = true;

            if (runtime == null)
            {
                statusLabel.Text = "None";
            }
            else if (STEAMVR_TEST.Invoke(runtime))
            {
                statusLabel.Text = "SteamVR";
            }
            else if (runtime == ocRuntimePath)
            {
                statusLabel.Text = "OpenComposite";
                useOpenComposite.Enabled = false;
            }
            else
            {
                statusLabel.Text = "Other";
            }
        }

        private async void CheckForUpdate()
        {
            string currentRevision = File.ReadAllText(revisionFilePath).Trim();

            updatesLabel.Visible = true;
            updatesLabel.Text = "Checking for updates...";

            string serverRevision = await UpdateChecker.GetLatestHash();

            if(serverRevision != currentRevision)
            {
                updatesLabel.Text = "Update Found!";
                doUpdate.Visible = true;
            } else
            {
                updatesLabel.Text = "Already up-to-date";
            }
        }

        private async void doUpdate_Click(object sender, EventArgs e)
        {
            doUpdate.Enabled = false;
            updatesLabel.Text = "Updating...";

            File.Delete(ocBinPath + Path.DirectorySeparatorChar + "vrclient.dll");
            File.Delete(ocBinPath + Path.DirectorySeparatorChar + "vrclient_x64.dll");

            await UpdateDLLs();

            doUpdate.Visible = false;
            updatesLabel.Text = "Update Complete";
        }

        private void SwitchToOpenComposite()
        {
            List<string> rts = new List<string>(config.Runtimes);
            rts.RemoveAll(s => s == ocRuntimePath);
            rts.Insert(0, ocRuntimePath);
            config.Runtimes = rts;

            UpdateStatus();
        }

        private async Task<bool> UpdateDLLs()
        {
            await dllDownloadMutex.WaitAsync().ConfigureAwait(false);

            bool downloads = false;

            string vrclient = ocBinPath + Path.DirectorySeparatorChar + "vrclient.dll";
            if (!File.Exists(vrclient))
            {
                downloads = true;
                statusLabel.Text = "Downloading 32-bit DLL";
                await DownloadFile(vrclient, "https://znix.xyz/OpenComposite/download.php?arch=x86");
            }

            string vrclient_x64 = ocBinPath + Path.DirectorySeparatorChar + "vrclient_x64.dll";
            if (!File.Exists(vrclient_x64))
            {
                downloads = true;
                statusLabel.Text = "Downloading 64-bit DLL";
                await DownloadFile(vrclient_x64, "https://znix.xyz/OpenComposite/download.php?arch=x64");
            }

            File.WriteAllText(revisionFilePath, await UpdateChecker.GetLatestHash());

            dllDownloadMutex.Release();

            return downloads;
        }

        private async void useOpenComposite_Click(object sender, EventArgs e)
        {
            useOpenComposite.Enabled = false;

            bool downloads = await UpdateDLLs();

            SwitchToOpenComposite();

            if(downloads)
            {
                statusLabel.Text += " (Download Complete)";
            }
        }

        private async Task DownloadFile(string name, string url)
        {
            using (WebClient wc = new WebClient())
            {
                progressBar.Visible = true;

                wc.DownloadProgressChanged += (object sender, DownloadProgressChangedEventArgs e) =>
                {
                    progressBar.Value = e.ProgressPercentage;
                };

                // Ensure the file gets redownloaded if the program is closed during download
                string temp = name + ".part";

                await wc.DownloadFileTaskAsync(
                    // Param1 = Link of file
                    new System.Uri(url),
                    // Param2 = Path to save
                    temp
                );

                File.Move(temp, name);

                progressBar.Value = 0;
                progressBar.Visible = false;
            }
        }
    }
}
