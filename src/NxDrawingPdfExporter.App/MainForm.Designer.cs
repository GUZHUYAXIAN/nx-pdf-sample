using NxDrawingPdfExporter.App;

namespace NxDrawingPdfExporter.App
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private System.Windows.Forms.GroupBox groupNx = null!;
        private System.Windows.Forms.Label labelNxStatus = null!;
        private System.Windows.Forms.ComboBox comboNxInstallations = null!;
        private System.Windows.Forms.Button buttonSelectNx = null!;
        private System.Windows.Forms.Button buttonRefreshNx = null!;
        private System.Windows.Forms.Button buttonBrowseNx = null!;
        private System.Windows.Forms.GroupBox groupInput = null!;
        private System.Windows.Forms.RadioButton radioScanFolder = null!;
        private System.Windows.Forms.RadioButton radioManual = null!;
        private System.Windows.Forms.TableLayoutPanel panelScan = null!;
        private System.Windows.Forms.TextBox textScanFolder = null!;
        private System.Windows.Forms.Button buttonBrowseScan = null!;
        private System.Windows.Forms.CheckBox checkRecursive = null!;
        private System.Windows.Forms.TableLayoutPanel panelManual = null!;
        private System.Windows.Forms.ListBox listManual = null!;
        private System.Windows.Forms.Button buttonAddFiles = null!;
        private System.Windows.Forms.Button buttonRemoveFile = null!;
        private System.Windows.Forms.GroupBox groupOutput = null!;
        private System.Windows.Forms.RadioButton radioBesideSource = null!;
        private System.Windows.Forms.RadioButton radioUnified = null!;
        private System.Windows.Forms.TableLayoutPanel panelUnified = null!;
        private System.Windows.Forms.TextBox textUnified = null!;
        private System.Windows.Forms.Button buttonBrowseUnified = null!;
        private System.Windows.Forms.GroupBox groupPolicy = null!;
        private System.Windows.Forms.RadioButton radioSkipExisting = null!;
        private System.Windows.Forms.RadioButton radioOverwrite = null!;
        private System.Windows.Forms.GroupBox groupPreflight = null!;
        private System.Windows.Forms.ListView listPreflight = null!;
        private System.Windows.Forms.ColumnHeader preflightSource = null!;
        private System.Windows.Forms.ColumnHeader preflightTarget = null!;
        private System.Windows.Forms.ColumnHeader preflightStatus = null!;
        private System.Windows.Forms.Button buttonPreflight = null!;
        private System.Windows.Forms.GroupBox groupRun = null!;
        private System.Windows.Forms.Button buttonStart = null!;
        private System.Windows.Forms.Button buttonCancelRun = null!;
        private System.Windows.Forms.Label labelProgress = null!;
        private System.Windows.Forms.ProgressBar progressBar = null!;
        private System.Windows.Forms.ListView listResults = null!;
        private System.Windows.Forms.ColumnHeader resultSource = null!;
        private System.Windows.Forms.ColumnHeader resultStatus = null!;
        private System.Windows.Forms.ColumnHeader resultMessage = null!;
        private System.Windows.Forms.Label labelSummary = null!;
        private System.Windows.Forms.Button buttonOpenOutput = null!;
        private System.Windows.Forms.Button buttonOpenLog = null!;

        private void InitializeComponent()
        {
            SuspendLayout();
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            Text = "NX 图纸批量导出工具";
            Font = new System.Drawing.Font("Microsoft YaHei UI", 9F);
            ClientSize = new System.Drawing.Size(860, 720);
            MinimumSize = new System.Drawing.Size(720, 560);
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            var layout = new System.Windows.Forms.TableLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new System.Windows.Forms.Padding(8),
                AutoScroll = true
            };
            layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            for (var i = 0; i < 6; i++)
            {
                layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            }

            // NX 状态
            groupNx = new System.Windows.Forms.GroupBox { Text = "NX 环境", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var nxLayout = new System.Windows.Forms.TableLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            labelNxStatus = new System.Windows.Forms.Label { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, Padding = new System.Windows.Forms.Padding(4), Text = "尚未检测 NX。" };
            nxLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            nxLayout.SizeChanged += (_, _) => labelNxStatus.MaximumSize = new System.Drawing.Size(System.Math.Max(1, nxLayout.ClientSize.Width - 12), 0);
            var nxActions = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, WrapContents = true };
            comboNxInstallations = new System.Windows.Forms.ComboBox { DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList, Width = 380, AccessibleName = "NX 安装候选" };
            buttonSelectNx = new System.Windows.Forms.Button { Text = "使用所选 NX", AutoSize = true, AccessibleName = "使用所选 NX" };
            buttonRefreshNx = new System.Windows.Forms.Button { Text = "重新检测", AutoSize = true, AccessibleName = "重新检测 NX" };
            buttonBrowseNx = new System.Windows.Forms.Button { Text = "手动选择 NX 目录", AutoSize = true, AccessibleName = "手动选择 NX 目录" };
            nxActions.Controls.AddRange(new System.Windows.Forms.Control[] { comboNxInstallations, buttonSelectNx, buttonRefreshNx, buttonBrowseNx });
            nxLayout.Controls.Add(labelNxStatus, 0, 0);
            nxLayout.Controls.Add(nxActions, 0, 1);
            groupNx.Controls.Add(nxLayout);
            layout.Controls.Add(groupNx, 0, 0);

            // 输入
            groupInput = new System.Windows.Forms.GroupBox { Text = "输入", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var inputLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 1, RowCount = 6, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            inputLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            inputLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

            radioScanFolder = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "扫描文件夹" };
            radioManual = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "手动多选 PRT" };

            panelScan = new System.Windows.Forms.TableLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 2 };
            panelScan.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelScan.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            textScanFolder = new System.Windows.Forms.TextBox { Dock = System.Windows.Forms.DockStyle.Fill };
            buttonBrowseScan = new System.Windows.Forms.Button { Text = "浏览…", AutoSize = true };
            checkRecursive = new System.Windows.Forms.CheckBox { AutoSize = true, Text = "包含子文件夹" };
            panelScan.Controls.Add(textScanFolder, 0, 0);
            panelScan.Controls.Add(buttonBrowseScan, 1, 0);
            panelScan.Controls.Add(checkRecursive, 0, 1);

            panelManual = new System.Windows.Forms.TableLayoutPanel { Height = 180, Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            panelManual.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelManual.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            listManual = new System.Windows.Forms.ListBox { Dock = System.Windows.Forms.DockStyle.Fill, HorizontalScrollbar = true };
            var manualButtons = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, FlowDirection = System.Windows.Forms.FlowDirection.TopDown };
            buttonAddFiles = new System.Windows.Forms.Button { Text = "添加文件…", AutoSize = true };
            buttonRemoveFile = new System.Windows.Forms.Button { Text = "移除选中", AutoSize = true };
            manualButtons.Controls.AddRange(new System.Windows.Forms.Control[] { buttonAddFiles, buttonRemoveFile });
            panelManual.Controls.Add(listManual, 0, 0);
            panelManual.Controls.Add(manualButtons, 1, 0);

            inputLayout.Controls.Add(radioScanFolder, 0, 0);
            inputLayout.Controls.Add(panelScan, 0, 1);
            inputLayout.Controls.Add(radioManual, 0, 2);
            inputLayout.Controls.Add(panelManual, 0, 3);
            groupInput.Controls.Add(inputLayout);
            layout.Controls.Add(groupInput, 0, 1);

            // 输出
            groupOutput = new System.Windows.Forms.GroupBox { Text = "输出", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var outputLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 1, RowCount = 3, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            outputLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            radioBesideSource = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "跟随制图 PRT 目录" };
            radioUnified = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "统一输出目录" };
            panelUnified = new System.Windows.Forms.TableLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            panelUnified.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            panelUnified.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.AutoSize));
            textUnified = new System.Windows.Forms.TextBox { Dock = System.Windows.Forms.DockStyle.Fill };
            buttonBrowseUnified = new System.Windows.Forms.Button { Text = "浏览…", AutoSize = true };
            panelUnified.Controls.Add(textUnified, 0, 0);
            panelUnified.Controls.Add(buttonBrowseUnified, 1, 0);
            outputLayout.Controls.Add(radioBesideSource, 0, 0);
            outputLayout.Controls.Add(radioUnified, 0, 1);
            outputLayout.Controls.Add(panelUnified, 0, 2);
            groupOutput.Controls.Add(outputLayout);
            layout.Controls.Add(groupOutput, 0, 2);

            // 已有 PDF
            groupPolicy = new System.Windows.Forms.GroupBox { Text = "已有 PDF", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var policyLayout = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            radioSkipExisting = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "跳过已有 PDF" };
            radioOverwrite = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "覆盖已有 PDF（先验证后替换）" };
            policyLayout.Controls.Add(radioSkipExisting);
            policyLayout.Controls.Add(radioOverwrite);
            groupPolicy.Controls.Add(policyLayout);
            layout.Controls.Add(groupPolicy, 0, 3);

            // 预检查
            groupPreflight = new System.Windows.Forms.GroupBox { Text = "预检查", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            listPreflight = new System.Windows.Forms.ListView
            {
                View = System.Windows.Forms.View.Details,
                FullRowSelect = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Height = 110
            };
            preflightSource = new System.Windows.Forms.ColumnHeader { Text = "源 PRT", Width = 320 };
            preflightTarget = new System.Windows.Forms.ColumnHeader { Text = "目标 PDF", Width = 320 };
            preflightStatus = new System.Windows.Forms.ColumnHeader { Text = "状态", Width = 140 };
            listPreflight.Columns.AddRange(new[] { preflightSource, preflightTarget, preflightStatus });
            buttonPreflight = new System.Windows.Forms.Button { Text = "执行预检查", AutoSize = true };
            var preflightLayout = new System.Windows.Forms.TableLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
            preflightLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            preflightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
            preflightLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            preflightLayout.Controls.Add(listPreflight, 0, 0);
            preflightLayout.Controls.Add(buttonPreflight, 0, 1);
            groupPreflight.Controls.Add(preflightLayout);
            layout.Controls.Add(groupPreflight, 0, 4);

            // 运行
            groupRun = new System.Windows.Forms.GroupBox { Text = "运行", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var runLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 1, RowCount = 5, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            runLayout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            runLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            runLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            runLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 24F));
            runLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 150F));
            runLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            buttonStart = new System.Windows.Forms.Button { Text = "开始导出", AutoSize = true };
            buttonCancelRun = new System.Windows.Forms.Button { Text = "取消", AutoSize = true, Enabled = false };
            labelProgress = new System.Windows.Forms.Label { AutoSize = true, Text = "就绪。" };
            progressBar = new System.Windows.Forms.ProgressBar { Dock = System.Windows.Forms.DockStyle.Fill };
            listResults = new System.Windows.Forms.ListView
            {
                View = System.Windows.Forms.View.Details,
                FullRowSelect = true,
                Dock = System.Windows.Forms.DockStyle.Fill,
                Height = 150
            };
            resultSource = new System.Windows.Forms.ColumnHeader { Text = "源 PRT", Width = 300 };
            resultStatus = new System.Windows.Forms.ColumnHeader { Text = "结果", Width = 120 };
            resultMessage = new System.Windows.Forms.ColumnHeader { Text = "说明", Width = 360 };
            listResults.Columns.AddRange(new[] { resultSource, resultStatus, resultMessage });
            labelSummary = new System.Windows.Forms.Label { AutoSize = true, Text = "尚未运行。" };
            buttonOpenOutput = new System.Windows.Forms.Button { Text = "打开输出目录", AutoSize = true };
            buttonOpenLog = new System.Windows.Forms.Button { Text = "打开日志", AutoSize = true };

            var progressFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            progressFlow.Controls.Add(labelProgress);
            var buttonFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true };
            buttonFlow.Controls.Add(buttonStart);
            buttonFlow.Controls.Add(buttonCancelRun);
            runLayout.Controls.Add(buttonFlow, 0, 0);
            runLayout.Controls.Add(progressFlow, 0, 1);
            runLayout.Controls.Add(progressBar, 0, 2);
            runLayout.Controls.Add(listResults, 0, 3);
            var resultButtonFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true };
            resultButtonFlow.Controls.Add(labelSummary);
            resultButtonFlow.Controls.Add(buttonOpenOutput);
            resultButtonFlow.Controls.Add(buttonOpenLog);
            runLayout.Controls.Add(resultButtonFlow, 0, 4);
            groupRun.Controls.Add(runLayout);
            layout.Controls.Add(groupRun, 0, 5);

            comboNxInstallations.TabIndex = 0;
            buttonSelectNx.TabIndex = 1;
            buttonRefreshNx.TabIndex = 2;
            buttonBrowseNx.TabIndex = 3;
            radioScanFolder.TabIndex = 4;
            buttonBrowseScan.TabIndex = 5;
            checkRecursive.TabIndex = 6;
            radioManual.TabIndex = 7;
            buttonAddFiles.TabIndex = 8;
            buttonRemoveFile.TabIndex = 9;
            radioBesideSource.TabIndex = 10;
            radioUnified.TabIndex = 11;
            buttonBrowseUnified.TabIndex = 12;
            radioSkipExisting.TabIndex = 13;
            radioOverwrite.TabIndex = 14;
            buttonPreflight.TabIndex = 15;
            buttonStart.TabIndex = 16;
            buttonCancelRun.TabIndex = 17;
            buttonOpenOutput.TabIndex = 18;
            buttonOpenLog.TabIndex = 19;

            groupNx.TabIndex = 0;
            groupInput.TabIndex = 1;
            groupOutput.TabIndex = 2;
            groupPolicy.TabIndex = 3;
            groupPreflight.TabIndex = 4;
            groupRun.TabIndex = 5;
            radioScanFolder.TabIndex = 0;
            panelScan.TabIndex = 1;
            radioManual.TabIndex = 2;
            panelManual.TabIndex = 3;
            textScanFolder.TabIndex = 0;
            buttonBrowseScan.TabIndex = 1;
            checkRecursive.TabIndex = 2;
            listManual.TabIndex = 0;
            manualButtons.TabIndex = 1;
            radioBesideSource.TabIndex = 0;
            radioUnified.TabIndex = 1;
            panelUnified.TabIndex = 2;
            textUnified.TabIndex = 0;
            buttonBrowseUnified.TabIndex = 1;
            Controls.Add(layout);
            ResumeLayout();
        }
    }
}
