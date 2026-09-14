using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using NxDrawingPdfExporter.App.Logging;
using NxDrawingPdfExporter.App.Runtime;
using NxDrawingPdfExporter.App.Runtime.Detection;
using NxDrawingPdfExporter.Contracts;
using NxDrawingPdfExporter.Core.Output;

namespace NxDrawingPdfExporter.App
{
    internal sealed record MainFormNxViewState(
        string StatusText,
        bool ShowCandidateList,
        bool EnableNxActions,
        bool EnableStart);

    /// <summary>
    /// 单窗口 UI：控制器状态的薄绑定层，不含业务规则。
    /// 运行中关闭窗口走安全停止流程，绝不粗暴终止正在替换 PDF 的 Worker。
    /// </summary>
    public sealed partial class MainForm : Form
    {
        private readonly ApplicationController controller;
        private bool closePending;

        public MainForm()
        {
            InitializeComponent();
            controller = new ApplicationController(new ApplicationServices(), new StartupLogSink(), new NxDetectionLog());
            BindController();
            BindEvents();
            UpdateDynamicState();
        }

        private void BindController()
        {
            controller.StateChanged += () =>
            {
                if (InvokeRequired)
                {
                    BeginInvoke(UpdateDynamicState);
                }
                else
                {
                    UpdateDynamicState();
                }
            };
        }

        internal static MainFormNxViewState BuildNxViewState(ApplicationController controller)
        {
            ArgumentNullException.ThrowIfNull(controller);
            return new MainFormNxViewState(
                controller.NxStatusMessage,
                controller.NxDiscovery.Installations.Count > 1,
                !controller.IsBusy && !controller.IsNxDetectionBusy,
                !controller.IsBusy && !controller.IsNxDetectionBusy && controller.SelectedNx is not null);
        }

        internal static NxInstallation? ChooseNxCandidate(NxInstallationDiscoveryResult discovery, string? highlightedRoot) =>
            discovery.Installations.FirstOrDefault(installation => string.Equals(
                installation.RootDirectory, highlightedRoot, StringComparison.OrdinalIgnoreCase))
            ?? discovery.SelectedInstallation
            ?? discovery.Installations.FirstOrDefault();

        internal static string FormatNxCandidate(NxInstallation installation) =>
            $"{installation.RootDirectory} — NX {installation.DetectedVersion}";

        private void BindEvents()
        {
            comboNxInstallations.FormattingEnabled = true;
            comboNxInstallations.Format += (_, args) =>
            {
                if (args.ListItem is NxInstallation installation)
                    args.Value = FormatNxCandidate(installation);
            };
            Shown += async (_, _) => await RefreshNxAsync();
            buttonRefreshNx.Click += async (_, _) => await RefreshNxAsync();
            buttonSelectNx.Click += async (_, _) =>
            {
                if (comboNxInstallations.SelectedItem is NxInstallation selected)
                {
                    await PerformNxActionAsync(() => controller.SelectDetectedNxAsync(selected.RootDirectory));
                }
            };
            buttonBrowseNx.Click += async (_, _) => await BrowseNxAsync();
            radioScanFolder.CheckedChanged += (_, _) => { if (radioScanFolder.Checked) { controller.InputMode = GuiInputMode.ScanFolder; UpdateDynamicState(); } };
            radioManual.CheckedChanged += (_, _) => { if (radioManual.Checked) { controller.InputMode = GuiInputMode.ManualSelection; UpdateDynamicState(); } };
            buttonBrowseScan.Click += (_, _) =>
            {
                using var dialog = new FolderBrowserDialog();
                if (Directory.Exists(textScanFolder.Text))
                {
                    dialog.SelectedPath = textScanFolder.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    textScanFolder.Text = dialog.SelectedPath;
                }
            };
            checkRecursive.CheckedChanged += (_, _) => controller.IncludeSubfolders = checkRecursive.Checked;
            buttonAddFiles.Click += (_, _) =>
            {
                using var dialog = new OpenFileDialog { Multiselect = true, Filter = "NX 部件 (*.prt)|*.prt" };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    var merged = new List<string>(controller.ManualPaths);
                    merged.AddRange(dialog.FileNames);
                    controller.ManualPaths = merged;
                    RefreshManualList();
                }
            };
            buttonRemoveFile.Click += (_, _) =>
            {
                var selected = listManual.SelectedItems.Cast<string>().ToList();
                if (selected.Count == 0)
                {
                    return;
                }

                controller.ManualPaths = controller.ManualPaths.Where(p => !selected.Contains(p, StringComparer.OrdinalIgnoreCase)).ToList();
                RefreshManualList();
            };

            radioBesideSource.CheckedChanged += (_, _) => { if (radioBesideSource.Checked) { controller.OutputMode = OutputMode.BesideSource; UpdateDynamicState(); } };
            radioUnified.CheckedChanged += (_, _) => { if (radioUnified.Checked) { controller.OutputMode = OutputMode.UnifiedDirectory; UpdateDynamicState(); } };
            buttonBrowseUnified.Click += (_, _) =>
            {
                using var dialog = new FolderBrowserDialog();
                if (Directory.Exists(textUnified.Text))
                {
                    dialog.SelectedPath = textUnified.Text;
                }

                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    textUnified.Text = dialog.SelectedPath;
                }
            };

            radioSkipExisting.CheckedChanged += (_, _) => { if (radioSkipExisting.Checked) { controller.ExistingPdfPolicy = ExistingPdfPolicy.Skip; } };
            radioOverwrite.CheckedChanged += (_, _) => { if (radioOverwrite.Checked) { controller.ExistingPdfPolicy = ExistingPdfPolicy.Overwrite; } };

            buttonPreflight.Click += (_, _) => RunPreflight();
            buttonStart.Click += (_, _) => StartRun();
            buttonCancelRun.Click += (_, _) => controller.Cancel();
            buttonOpenOutput.Click += (_, _) => OpenOutputDirectory();
            buttonOpenLog.Click += (_, _) => OpenLog();
        }

        private async Task RefreshNxAsync()
            => await PerformNxActionAsync(() => controller.RefreshNxStatusAsync());

        private async Task PerformNxActionAsync(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                MessageBox.Show(this, "NX 操作未完成，请重新检测（" + error.GetType().Name + "）。", "NX 检测失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task BrowseNxAsync()
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "请选择包含 UGII 文件夹的 NX 10.0 根目录"
            };
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            await PerformNxActionAsync(() => controller.SelectManualNxAsync(dialog.SelectedPath));
        }

        private void StartRun()
        {
            controller.ScanFolderPath = textScanFolder.Text;
            controller.UnifiedOutputDirectory = textUnified.Text;
            if (controller.InputMode == GuiInputMode.ScanFolder && !Directory.Exists(controller.ScanFolderPath))
            {
                MessageBox.Show(this, "请先选择存在的扫描文件夹。", "输入不完整", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _ = StartRunAsync();
        }

        private async Task StartRunAsync()
        {
            try
            {
                await controller.RunAsync();
                ShowRunOutcome();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "运行失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (closePending)
                {
                    Close();
                }
            }
        }

        private void ShowRunOutcome()
        {
            if (controller.Summary is { } summary && summary.Failed > 0)
            {
                MessageBox.Show(this, controller.SummaryText, "部分文件失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RunPreflight()
        {
            controller.ScanFolderPath = textScanFolder.Text;
            controller.UnifiedOutputDirectory = textUnified.Text;
            listPreflight.Items.Clear();
            IReadOnlyList<OutputPlanItem> plan;
            try
            {
                plan = controller.Preflight();
            }
            catch (Exception error)
            {
                MessageBox.Show(this, error.Message, "预检查失败", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            foreach (var item in plan)
            {
                listPreflight.Items.Add(new ListViewItem(new[]
                {
                    item.SourcePath,
                    item.FinalOutputPath,
                    item.Status.ToString()
                }));
            }

            if (plan.Count == 0)
            {
                MessageBox.Show(this, "未发现任何候选 PRT 文件。", "预检查", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void OpenOutputDirectory()
        {
            var directory = controller.OutputMode == OutputMode.UnifiedDirectory && Directory.Exists(textUnified.Text)
                ? textUnified.Text
                : Path.GetDirectoryName(listResults.SelectedItems.Count > 0
                    ? controller.Results.Select(r => r.FinalOutputPath).FirstOrDefault() ?? textScanFolder.Text
                    : textScanFolder.Text);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = directory,
                    UseShellExecute = true
                });
            }
        }

        private void OpenLog()
        {
            var runDirectory = controller.CurrentRunDirectory;
            if (runDirectory is null || !Directory.Exists(runDirectory))
            {
                MessageBox.Show(this, "还没有可用的运行日志。", "日志", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = runDirectory,
                UseShellExecute = true
            });
        }

        private void RefreshManualList()
        {
            listManual.BeginUpdate();
            listManual.Items.Clear();
            foreach (var path in controller.ManualPaths)
            {
                listManual.Items.Add(path);
            }

            listManual.EndUpdate();
        }

        private void UpdateDynamicState()
        {
            MainFormNxViewState nxState = BuildNxViewState(controller);
            labelNxStatus.Text = nxState.StatusText;
            comboNxInstallations.Visible = nxState.ShowCandidateList;
            buttonSelectNx.Visible = nxState.ShowCandidateList;
            buttonRefreshNx.Enabled = nxState.EnableNxActions;
            buttonBrowseNx.Enabled = nxState.EnableNxActions;
            buttonSelectNx.Enabled = nxState.EnableNxActions;
            comboNxInstallations.Enabled = nxState.EnableNxActions;
            groupInput.Enabled = nxState.EnableNxActions;
            groupOutput.Enabled = nxState.EnableNxActions;
            groupPolicy.Enabled = nxState.EnableNxActions;
            if (nxState.ShowCandidateList)
            {
                NxInstallation? highlighted = ChooseNxCandidate(controller.NxDiscovery,
                    (comboNxInstallations.SelectedItem as NxInstallation)?.RootDirectory);
                comboNxInstallations.Items.Clear();
                foreach (NxInstallation installation in controller.NxDiscovery.Installations)
                {
                    comboNxInstallations.Items.Add(installation);
                }
                comboNxInstallations.SelectedItem = highlighted;
            }
            else
            {
                comboNxInstallations.Items.Clear();
            }

            panelScan.Enabled = controller.InputMode == GuiInputMode.ScanFolder;
            panelManual.Enabled = controller.InputMode == GuiInputMode.ManualSelection;
            panelUnified.Enabled = controller.OutputMode == OutputMode.UnifiedDirectory;

            radioScanFolder.Checked = controller.InputMode == GuiInputMode.ScanFolder;
            radioManual.Checked = controller.InputMode == GuiInputMode.ManualSelection;
            radioBesideSource.Checked = controller.OutputMode == OutputMode.BesideSource;
            radioUnified.Checked = controller.OutputMode == OutputMode.UnifiedDirectory;
            radioSkipExisting.Checked = controller.ExistingPdfPolicy == ExistingPdfPolicy.Skip;
            radioOverwrite.Checked = controller.ExistingPdfPolicy == ExistingPdfPolicy.Overwrite;

            labelProgress.Text = controller.ProgressText;
            labelSummary.Text = controller.SummaryText;
            buttonStart.Enabled = nxState.EnableStart;
            buttonCancelRun.Enabled = controller.IsBusy;
            buttonPreflight.Enabled = !controller.IsBusy;
            progressBar.Style = controller.IsBusy ? ProgressBarStyle.Marquee : ProgressBarStyle.Continuous;
            if (!controller.IsBusy)
            {
                progressBar.Value = 0;
            }

            listResults.Items.Clear();
            foreach (var result in controller.Results)
            {
                listResults.Items.Add(new ListViewItem(new[]
                {
                    result.SourcePath,
                    StatusText(result.Status),
                    result.Message
                }));
            }
        }

        private static string StatusText(FileResultStatus status)
        {
            return status switch
            {
                FileResultStatus.Success => "成功",
                FileResultStatus.Overwritten => "已覆盖",
                FileResultStatus.SkippedExisting => "已存在，跳过",
                FileResultStatus.PureModel => "纯模型，跳过",
                FileResultStatus.NoValidSheets => "无有效图纸页",
                FileResultStatus.NameConflict => "名称冲突",
                FileResultStatus.Cancelled => "已取消",
                FileResultStatus.Failed => "失败",
                _ => status.ToString()
            };
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            if (e.CloseReason == CloseReason.UserClosing && controller.IsBusy && !closePending)
            {
                // 安全停止：请求取消并等待当前原子操作完成，不杀 Worker。
                closePending = true;
                controller.Cancel();
                labelProgress.Text = "正在等待当前文件安全完成，随后关闭…";
                e.Cancel = true;
            }
        }

        /// <summary>启动阶段（尚无运行目录）的日志去向：内存缓冲即可。</summary>
        private sealed class StartupLogSink : ILogSink
        {
            public void Write(string message)
            {
                System.Diagnostics.Debug.WriteLine(message);
            }
        }
    }
}
