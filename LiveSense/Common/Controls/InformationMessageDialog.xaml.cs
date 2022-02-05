﻿using MaterialDesignThemes.Wpf;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace LiveSense.Common.Controls;

/// <summary>
/// Interaction logic for InformationMessageDialog.xaml
/// </summary>
public partial class InformationMessageDialog : UserControl
{
    private readonly bool _showCheckbox;

    public string VersionText => $"v{Assembly.GetEntryAssembly().GetName().Version}";
    public bool ShowCheckbox => _showCheckbox;
    public bool DontShowAgain { get; set; }

    public InformationMessageDialog(bool showCheckbox)
    {
        _showCheckbox = showCheckbox;

        InitializeComponent();
    }

    public void OnDismiss()
    {
        DialogHost.CloseDialogCommand.Execute(ShowCheckbox ? DontShowAgain : null, null);
    }

    public void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo()
        {
            FileName = e.Uri.AbsoluteUri,
            UseShellExecute = true
        });
        e.Handled = true;
    }
}
