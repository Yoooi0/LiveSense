﻿using System.Windows.Controls;

namespace LiveSense.Common.Controls;

/// <summary>
/// Interaction logic for ErrorMessageDialog.xaml
/// </summary>
public partial class ErrorMessageDialog : UserControl
{
    public string Message { get; set; }

    public ErrorMessageDialog(string message)
    {
        InitializeComponent();
        Message = message;
    }
}
