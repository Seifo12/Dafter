using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using CourtDesk.Application;
using CourtDesk.Application.Ports;
using CourtDesk.Domain;
using CourtDesk.Resources;

namespace CourtDesk.UI.Dialogs
{
    public partial class FirstRunDialog : Window
    {
        private readonly SettingsService _settings;

        public FirstRunDialog(SettingsService settings)
        {
            InitializeComponent();
            _settings = settings;
            StationBox.Text = settings.Get(SettingKeys.StationName);
            DirectorateBox.Text = settings.Get(SettingKeys.DirectorateName);
            OperatorBox.Text = settings.Get(SettingKeys.OperatorName);
            GoButton.Focus();
        }

        private void GoClick(object sender, RoutedEventArgs e)
        {
            _settings.Set(SettingKeys.StationName, StationBox.Text.Trim());
            _settings.Set(SettingKeys.DirectorateName, DirectorateBox.Text.Trim());
            _settings.Set(SettingKeys.OperatorName, OperatorBox.Text.Trim());
            _settings.Set(SettingKeys.FirstRunDone, "1");
            DialogResult = true;
        }
    }
}