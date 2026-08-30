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
        private System.Windows.Forms.GroupBox groupInput = null!;
        private System.Windows.Forms.RadioButton radioScanFolder = null!;
        private System.Windows.Forms.RadioButton radioManual = null!;
        private System.Windows.Forms.Panel panelScan = null!;
        private System.Windows.Forms.TextBox textScanFolder = null!;
        private System.Windows.Forms.Button buttonBrowseScan = null!;
        private System.Windows.Forms.CheckBox checkRecursive = null!;
        private System.Windows.Forms.Panel panelManual = null!;
        private System.Windows.Forms.ListBox listManual = null!;
        private System.Windows.Forms.Button buttonAddFiles = null!;
        private System.Windows.Forms.Button buttonRemoveFile = null!;
        private System.Windows.Forms.GroupBox groupOutput = null!;
        private System.Windows.Forms.RadioButton radioBesideSource = null!;
        private System.Windows.Forms.RadioButton radioUnified = null!;
        private System.Windows.Forms.Panel panelUnified = null!;
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
            StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

            var layout = new System.Windows.Forms.TableLayoutPanel
            {
                Dock = System.Windows.Forms.DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 6,
                Padding = new System.Windows.Forms.Padding(8)
            };
            for (var i = 0; i < 6; i++)
            {
                layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
            }

            // NX 状态
            groupNx = new System.Windows.Forms.GroupBox { Text = "NX 环境", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            labelNxStatus = new System.Windows.Forms.Label { AutoSize = true, Padding = new System.Windows.Forms.Padding(4), Text = "正在检测 NX…" };
            groupNx.Controls.Add(labelNxStatus);
            layout.Controls.Add(groupNx, 0, 0);

            // 输入
            groupInput = new System.Windows.Forms.GroupBox { Text = "输入", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var inputLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 1, RowCount = 6, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            inputLayout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));

            radioScanFolder = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "扫描文件夹" };
            radioManual = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "手动多选 PRT" };

            panelScan = new System.Windows.Forms.Panel { AutoSize = true, Height = 64 };
            textScanFolder = new System.Windows.Forms.TextBox { Width = 640, Left = 4, Top = 6 };
            buttonBrowseScan = new System.Windows.Forms.Button { Text = "浏览…", Left = 656, Top = 4, Width = 90 };
            checkRecursive = new System.Windows.Forms.CheckBox { AutoSize = true, Text = "包含子文件夹", Left = 8, Top = 34 };
            panelScan.Controls.Add(textScanFolder);
            panelScan.Controls.Add(buttonBrowseScan);
            panelScan.Controls.Add(checkRecursive);

            panelManual = new System.Windows.Forms.Panel { Height = 180, Width = 760 };
            listManual = new System.Windows.Forms.ListBox { Left = 4, Top = 4, Width = 640, Height = 170, HorizontalScrollbar = true };
            buttonAddFiles = new System.Windows.Forms.Button { Text = "添加文件…", Left = 656, Top = 8, Width = 100 };
            buttonRemoveFile = new System.Windows.Forms.Button { Text = "移除选中", Left = 656, Top = 40, Width = 100 };
            panelManual.Controls.Add(listManual);
            panelManual.Controls.Add(buttonAddFiles);
            panelManual.Controls.Add(buttonRemoveFile);

            inputLayout.Controls.Add(radioScanFolder, 0, 0);
            inputLayout.Controls.Add(panelScan, 0, 1);
            inputLayout.Controls.Add(radioManual, 0, 2);
            inputLayout.Controls.Add(panelManual, 0, 3);
            groupInput.Controls.Add(inputLayout);
            layout.Controls.Add(groupInput, 0, 1);

            // 输出
            groupOutput = new System.Windows.Forms.GroupBox { Text = "输出", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var outputLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 1, RowCount = 3, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            radioBesideSource = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "跟随制图 PRT 目录" };
            radioUnified = new System.Windows.Forms.RadioButton { AutoSize = true, Text = "统一输出目录" };
            panelUnified = new System.Windows.Forms.Panel { AutoSize = true, Height = 34 };
            textUnified = new System.Windows.Forms.TextBox { Width = 640, Left = 4, Top = 4 };
            buttonBrowseUnified = new System.Windows.Forms.Button { Text = "浏览…", Left = 656, Top = 2, Width = 90 };
            panelUnified.Controls.Add(textUnified);
            panelUnified.Controls.Add(buttonBrowseUnified);
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
                Size = new System.Drawing.Size(800, 110)
            };
            preflightSource = new System.Windows.Forms.ColumnHeader { Text = "源 PRT", Width = 320 };
            preflightTarget = new System.Windows.Forms.ColumnHeader { Text = "目标 PDF", Width = 320 };
            preflightStatus = new System.Windows.Forms.ColumnHeader { Text = "状态", Width = 140 };
            listPreflight.Columns.AddRange(new[] { preflightSource, preflightTarget, preflightStatus });
            buttonPreflight = new System.Windows.Forms.Button { Text = "执行预检查", AutoSize = true };
            var preflightLayout = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill, FlowDirection = System.Windows.Forms.FlowDirection.TopDown };
            preflightLayout.Controls.Add(listPreflight);
            preflightLayout.Controls.Add(buttonPreflight);
            groupPreflight.Controls.Add(preflightLayout);
            layout.Controls.Add(groupPreflight, 0, 4);

            // 运行
            groupRun = new System.Windows.Forms.GroupBox { Text = "运行", AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            var runLayout = new System.Windows.Forms.TableLayoutPanel { ColumnCount = 2, RowCount = 4, AutoSize = true, Dock = System.Windows.Forms.DockStyle.Fill };
            buttonStart = new System.Windows.Forms.Button { Text = "开始导出", AutoSize = true };
            buttonCancelRun = new System.Windows.Forms.Button { Text = "取消", AutoSize = true, Enabled = false };
            labelProgress = new System.Windows.Forms.Label { AutoSize = true, Text = "就绪。" };
            progressBar = new System.Windows.Forms.ProgressBar { Width = 700, Height = 18 };
            listResults = new System.Windows.Forms.ListView
            {
                View = System.Windows.Forms.View.Details,
                FullRowSelect = true,
                Size = new System.Drawing.Size(800, 150)
            };
            resultSource = new System.Windows.Forms.ColumnHeader { Text = "源 PRT", Width = 300 };
            resultStatus = new System.Windows.Forms.ColumnHeader { Text = "结果", Width = 120 };
            resultMessage = new System.Windows.Forms.ColumnHeader { Text = "说明", Width = 360 };
            listResults.Columns.AddRange(new[] { resultSource, resultStatus, resultMessage });
            labelSummary = new System.Windows.Forms.Label { AutoSize = true, Text = "尚未运行。" };
            buttonOpenOutput = new System.Windows.Forms.Button { Text = "打开输出目录", AutoSize = true };
            buttonOpenLog = new System.Windows.Forms.Button { Text = "打开日志", AutoSize = true };

            var progressFlow = new System.Windows.Forms.FlowLayoutPanel { AutoSize = true, Width = 820 };
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
            runLayout.Controls.Add(resultButtonFlow, 1, 0);
            groupRun.Controls.Add(runLayout);
            layout.Controls.Add(groupRun, 0, 5);

            Controls.Add(layout);
            ResumeLayout();
        }
    }
}
